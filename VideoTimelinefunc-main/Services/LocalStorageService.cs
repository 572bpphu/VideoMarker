using System;
using System.IO;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;

namespace VideoTimelineApp.Services
{
    public static class LocalStorageService
    {
        private const string DefaultStorageSubdir = "D:\\VideoTimelineData\\Videos";

        /// <summary>
        /// Lấy đường dẫn thư mục lưu trữ media từ Web.config hoặc mặc định.
        /// Tự động tạo thư mục nếu chưa tồn tại.
        /// </summary>
        public static string GetStorageDirectory()
        {
            string configPath = WebConfigurationManager.AppSettings["LocalStoragePath"];
            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = DefaultStorageSubdir;
            }

            string fullPath;
            if (Path.IsPathRooted(configPath))
            {
                fullPath = configPath;
            }
            else
            {
                fullPath = HostingEnvironment.MapPath(configPath) 
                    ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configPath.TrimStart('~', '/', '\\'));
            }

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }

            return fullPath;
        }

        /// <summary>
        /// Tìm đường dẫn file vật lý của video theo tên file lưu trữ.
        /// Hỗ trợ kiểm tra cả thư mục cấu hình mới và thư mục fallback cũ (~/uploads/videos).
        /// </summary>
        public static string ResolvePhysicalPath(string storedFileName)
        {
            if (string.IsNullOrWhiteSpace(storedFileName)) return null;

            string safeFileName = Path.GetFileName(storedFileName);

            // 1. Kiểm tra trong thư mục cấu hình độc lập mới
            string primaryDir = GetStorageDirectory();
            string primaryPath = Path.Combine(primaryDir, safeFileName);
            if (File.Exists(primaryPath))
            {
                return primaryPath;
            }

            // 2. Fallback: Kiểm tra thư mục uploads/videos cũ trong root nếu có
            try
            {
                string legacyDir = HostingEnvironment.MapPath("~/uploads/videos");
                if (!string.IsNullOrEmpty(legacyDir))
                {
                    string legacyPath = Path.Combine(legacyDir, safeFileName);
                    if (File.Exists(legacyPath))
                    {
                        return legacyPath;
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi fallback
            }

            // Nếu chưa tồn tại, trả về đường dẫn mục tiêu trong thư mục lưu trữ mới
            return primaryPath;
        }

        /// <summary>
        /// Lưu file video tải lên vào thư mục lưu trữ độc lập
        /// </summary>
        public static string SaveUploadedVideo(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength == 0)
                throw new ArgumentException("File không hợp lệ hoặc rỗng");

            string dir = GetStorageDirectory();
            string ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".mp4";

            string storedFileName = Guid.NewGuid().ToString("N") + ext;
            string targetPath = Path.Combine(dir, storedFileName);

            file.SaveAs(targetPath);
            return storedFileName;
        }

        /// <summary>
        /// Xóa file vật lý khỏi ổ đĩa
        /// </summary>
        public static bool DeleteVideoFile(string storedFileName)
        {
            if (string.IsNullOrWhiteSpace(storedFileName)) return false;

            try
            {
                string path = ResolvePhysicalPath(storedFileName);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            catch
            {
                // Log hoặc bỏ qua nếu file đang bị lock/không xóa được
            }

            return false;
        }

        /// <summary>
        /// Trả về MIME type thích hợp cho video
        /// </summary>
        public static string GetMimeType(string filePathOrName)
        {
            string ext = Path.GetExtension(filePathOrName)?.ToLowerInvariant();
            switch (ext)
            {
                case ".mp4":
                    return "video/mp4";
                case ".webm":
                    return "video/webm";
                case ".ogg":
                case ".ogv":
                    return "video/ogg";
                case ".mov":
                    return "video/quicktime";
                case ".mkv":
                    return "video/x-matroska";
                case ".avi":
                    return "video/x-msvideo";
                default:
                    return "video/mp4";
            }
        }
    }
}
