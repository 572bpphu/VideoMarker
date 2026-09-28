using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using VideoTimelineApp.Data;
using VideoTimelineApp.Services;
using VideoTimelineApp.ViewModels;

namespace VideoTimelineApp.Controllers
{
    public class VideoController : Controller
    {
        private readonly AppDbContext _db = new AppDbContext();

        // GET /Video/Player/7
        public async Task<ActionResult> Player(int id)
        {
            var session = await _db.VideoSessions.FindAsync(id);
            if (session == null) return HttpNotFound();

            // Chuyển tiếp sang trang tổng hợp có DevExtreme Tabs
            return RedirectToAction("Index", "Home", new { videoId = id });
        }

        // GET /Video/GetSessionJson/7
        [HttpGet]
        public async Task<ActionResult> GetSessionJson(int id)
        {
            var session = await _db.VideoSessions
                .Include(v => v.Segments)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (session == null)
                return Json(new { success = false, message = "Không tìm thấy video." }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                session = new
                {
                    id = session.Id,
                    title = session.Title,
                    originalFileName = session.OriginalFileName,
                    duration = session.Duration,
                    fileSize = session.FileSize,
                    videoUrl = Url.Action("Stream", "Video", new { id = session.Id }),
                    segments = session.Segments
                        .OrderBy(s => s.StartTime)
                        .Select(s => new
                        {
                            id = s.Id,
                            label = s.Label,
                            startTime = s.StartTime,
                            endTime = s.EndTime,
                            duration = s.EndTime - s.StartTime,
                            type = s.Type,
                            color = s.Color
                        }).ToList()
                }
            }, JsonRequestBehavior.AllowGet);
        }

        // GET /Video/Stream/7
        // Phục vụ phát video với hỗ trợ HTTP Range Requests (206 Partial Content) để tua timeline mượt mà
        [HttpGet]
        public async Task<ActionResult> Stream(int id)
        {
            var session = await _db.VideoSessions.FindAsync(id);
            if (session == null) return HttpNotFound();

            // Nếu là video cũ đã lưu trên Cloudinary và còn URL tuyệt đối, redirect về CDN
            if (!string.IsNullOrEmpty(session.VideoUrl) && Uri.IsWellFormedUriString(session.VideoUrl, UriKind.Absolute))
            {
                return Redirect(session.VideoUrl);
            }

            if (string.IsNullOrEmpty(session.StoredFileName))
            {
                return HttpNotFound("Không tìm thấy thông tin file video.");
            }

            string physicalPath = LocalStorageService.ResolvePhysicalPath(session.StoredFileName);
            if (!System.IO.File.Exists(physicalPath))
            {
                return HttpNotFound("File video không tồn tại trong thư mục lưu trữ cục bộ: " + physicalPath);
            }

            return StreamVideoFile(physicalPath);
        }

        private ActionResult StreamVideoFile(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            long totalLength = fileInfo.Length;
            string mimeType = LocalStorageService.GetMimeType(filePath);

            string rangeHeader = Request.Headers["Range"];
            if (string.IsNullOrEmpty(rangeHeader))
            {
                Response.AddHeader("Accept-Ranges", "bytes");
                return File(filePath, mimeType);
            }

            // Phân tích header: Range: bytes=start-end
            long start = 0;
            long end = totalLength - 1;

            string rangeValue = rangeHeader.Replace("bytes=", "").Trim();
            string[] rangeParts = rangeValue.Split('-');

            if (rangeParts.Length > 0 && !string.IsNullOrEmpty(rangeParts[0]))
            {
                long.TryParse(rangeParts[0], out start);
            }
            if (rangeParts.Length > 1 && !string.IsNullOrEmpty(rangeParts[1]))
            {
                long.TryParse(rangeParts[1], out end);
            }

            if (end >= totalLength)
            {
                end = totalLength - 1;
            }

            long length = end - start + 1;
            if (length <= 0)
            {
                Response.StatusCode = 416; // Range Not Satisfiable
                Response.AddHeader("Content-Range", string.Format("bytes */{0}", totalLength));
                return new EmptyResult();
            }

            Response.StatusCode = 206;
            Response.StatusDescription = "Partial Content";
            Response.BufferOutput = false;
            Response.AddHeader("Accept-Ranges", "bytes");
            Response.AddHeader("Content-Range", string.Format("bytes {0}-{1}/{2}", start, end, totalLength));
            Response.AddHeader("Content-Length", length.ToString());
            Response.ContentType = mimeType;

            // Stream từng phần theo khối 64KB
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Seek(start, SeekOrigin.Begin);
                byte[] buffer = new byte[65536];
                long bytesRemaining = length;

                while (bytesRemaining > 0 && Response.IsClientConnected)
                {
                    int bytesToRead = (int)Math.Min(buffer.Length, bytesRemaining);
                    int bytesRead = stream.Read(buffer, 0, bytesToRead);
                    if (bytesRead <= 0) break;

                    Response.OutputStream.Write(buffer, 0, bytesRead);
                    Response.OutputStream.Flush();
                    bytesRemaining -= bytesRead;
                }
            }

            return new EmptyResult();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
