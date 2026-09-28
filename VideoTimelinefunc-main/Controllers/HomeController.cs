using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using VideoTimelineApp.Data;
using VideoTimelineApp.Models;
using VideoTimelineApp.Services;

namespace VideoTimelineApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db = new AppDbContext();

        // GET /
        public async Task<ActionResult> Index(int? videoId = null)
        {
            var videos = await _db.VideoSessions
                .Include(v => v.Segments)
                .OrderByDescending(v => v.UploadedAt)
                .ToListAsync();

            ViewBag.LocalStoragePath = LocalStorageService.GetStorageDirectory();
            ViewBag.InitialVideoId = videoId;
            return View(videos);
        }

        // POST /Home/Upload (Standard Form Submit)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Upload(HttpPostedFileBase file, string title)
        {
            if (file == null || file.ContentLength == 0)
            {
                TempData["Error"] = "Vui lòng chọn file video!";
                return RedirectToAction("Index");
            }

            int maxMB = int.Parse(WebConfigurationManager.AppSettings["MaxFileSizeMB"] ?? "500");
            if (file.ContentLength > (long)maxMB * 1024 * 1024)
            {
                TempData["Error"] = string.Format("File quá lớn! Tối đa {0} MB.", maxMB);
                return RedirectToAction("Index");
            }

            string videoTitle = string.IsNullOrWhiteSpace(title)
                ? Path.GetFileNameWithoutExtension(file.FileName)
                : title.Trim();

            try
            {
                string storedName = LocalStorageService.SaveUploadedVideo(file);

                var session = new VideoSession
                {
                    Title            = videoTitle,
                    OriginalFileName = file.FileName,
                    StoredFileName   = storedName,
                    FileSize         = file.ContentLength,
                    UploadedAt       = DateTime.UtcNow
                };

                _db.VideoSessions.Add(session);
                await _db.SaveChangesAsync();

                TempData["Success"] = string.Format("Đã lưu video \"{0}\" vào thư mục lưu trữ thành công!", session.Title);
                return RedirectToAction("Index", "Home", new { videoId = session.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi khi lưu video vào máy: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST /Home/UploadAjax (Upload qua AJAX với thanh tiến trình tải lên)
        [HttpPost]
        public async Task<JsonResult> UploadAjax()
        {
            try
            {
                if (Request.Files.Count == 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn file video!" });
                }

                var file = Request.Files[0];
                if (file == null || file.ContentLength == 0)
                {
                    return Json(new { success = false, message = "File video rỗng hoặc không hợp lệ!" });
                }

                int maxMB = int.Parse(WebConfigurationManager.AppSettings["MaxFileSizeMB"] ?? "500");
                if (file.ContentLength > (long)maxMB * 1024 * 1024)
                {
                    return Json(new { success = false, message = string.Format("File quá lớn! Tối đa {0} MB.", maxMB) });
                }

                string title = Request.Form["title"];
                string videoTitle = string.IsNullOrWhiteSpace(title)
                    ? Path.GetFileNameWithoutExtension(file.FileName)
                    : title.Trim();

                string storedName = LocalStorageService.SaveUploadedVideo(file);

                var session = new VideoSession
                {
                    Title            = videoTitle,
                    OriginalFileName = file.FileName,
                    StoredFileName   = storedName,
                    FileSize         = file.ContentLength,
                    UploadedAt       = DateTime.UtcNow
                };

                _db.VideoSessions.Add(session);
                await _db.SaveChangesAsync();

                TempData["Success"] = string.Format("Đã lưu video \"{0}\" thành công!", session.Title);
                return Json(new
                {
                    success     = true,
                    redirectUrl = Url.Action("Index", "Home", new { videoId = session.Id })
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi lưu file: " + ex.Message });
            }
        }

        // POST /Home/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            var session = await _db.VideoSessions.FindAsync(id);
            if (session == null) return HttpNotFound();

            // Xóa file vật lý trong thư mục lưu trữ ngoài
            if (!string.IsNullOrEmpty(session.StoredFileName))
            {
                LocalStorageService.DeleteVideoFile(session.StoredFileName);
            }

            _db.VideoSessions.Remove(session);
            await _db.SaveChangesAsync();

            TempData["Success"] = string.Format("Đã xoá video \"{0}\".", session.Title);
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _db.Dispose();
            base.Dispose(disposing);
        }
    }
}
