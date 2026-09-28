using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using Newtonsoft.Json;

namespace VideoTimelineApp.Services
{
    public class StorageSettingsModel
    {
        public string StoragePath { get; set; }
        public List<string> FallbackPaths { get; set; } = new List<string>();
    }

    public static class LocalStorageService
    {
        private const string DefaultStorageSubdir = "~/App_Data/Videos";
        private static readonly object _syncLock = new object();

        private static string GetSettingsFilePath()
        {
            string appData = HostingEnvironment.MapPath("~/App_Data")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            try
            {
                if (!Directory.Exists(appData))
                {
                    Directory.CreateDirectory(appData);
                }
            }
            catch { }
            return Path.Combine(appData, "storage_settings.json");
        }

        private static StorageSettingsModel LoadSettings()
        {
            lock (_syncLock)
            {
                try
                {
                    string file = GetSettingsFilePath();
                    if (File.Exists(file))
                    {
                        string json = File.ReadAllText(file);
                        var settings = JsonConvert.DeserializeObject<StorageSettingsModel>(json);
                        if (settings != null && !string.IsNullOrWhiteSpace(settings.StoragePath))
                        {
                            return settings;
                        }
                    }
                }
                catch
                {
                    // Bỏ qua lỗi đọc file, dùng mặc định
                }

                string configPath = WebConfigurationManager.AppSettings["LocalStoragePath"];
                if (string.IsNullOrWhiteSpace(configPath))
                {
                    configPath = DefaultStorageSubdir;
                }

                return new StorageSettingsModel
                {
                    StoragePath = configPath,
                    FallbackPaths = new List<string> { configPath }
                };
            }
        }

        private static void SaveSettings(StorageSettingsModel settings)
        {
            lock (_syncLock)
            {
                try
                {
                    string file = GetSettingsFilePath();
                    string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                    File.WriteAllText(file, json);
                }
                catch
                {
                    // Bỏ qua lỗi ghi file
                }
            }
        }

        /// <summary>
        /// Chuyển đổi đường dẫn tương đối (~) hoặc tuyệt đối thành đường dẫn vật lý đầy đủ
        /// </summary>
        public static string ResolveDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            if (path.StartsWith("~") || !Path.IsPathRooted(path))
            {
                return HostingEnvironment.MapPath(path)
                    ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.TrimStart('~', '/', '\\'));
            }

            return path;
        }

        /// <summary>
        /// Thư mục an toàn nằm ngay trong project (App_Data/Videos), luôn luôn khả dụng trên mọi máy khi clone git
        /// </summary>
        public static string GetSafeFallbackDirectory()
        {
            string safePath = HostingEnvironment.MapPath("~/App_Data/Videos")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Videos");
            try
            {
                if (!Directory.Exists(safePath))
                {
                    Directory.CreateDirectory(safePath);
                }
                return safePath;
            }
            catch
            {
                // Dự phòng cuối cùng: thư mục Temp của Windows
                try
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "VideoTimelineApp", "Videos");
                    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);
                    return tempDir;
                }
                catch
                {
                    return safePath;
                }
            }
        }

        /// <summary>
        /// Lấy đường dẫn thư mục lưu trữ media từ cấu hình người dùng (storage_settings.json),
        /// hoặc Web.config, hoặc mặc định (~/App_Data/Videos).
        /// Tự động kiểm tra tính khả dụng của ổ đĩa và fallback an toàn về thư mục nội bộ nếu đường dẫn trên máy người khác không tồn tại.
        /// </summary>
        public static string GetStorageDirectory()
        {
            var settings = LoadSettings();
            string configPath = settings.StoragePath;
            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = WebConfigurationManager.AppSettings["LocalStoragePath"];
            }
            if (string.IsNullOrWhiteSpace(configPath))
            {
                configPath = DefaultStorageSubdir;
            }

            string fullPath = ResolveDirectory(configPath);

            // Kiểm tra xem ổ đĩa/đường dẫn có hợp lệ và khả dụng trên máy này không
            try
            {
                if (!string.IsNullOrEmpty(fullPath))
                {
                    string root = Path.GetPathRoot(fullPath);
                    // Nếu ổ đĩa tồn tại trên máy hiện tại
                    if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
                    {
                        if (!Directory.Exists(fullPath))
                        {
                            Directory.CreateDirectory(fullPath);
                        }
                        return fullPath;
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi ổ đĩa/phân quyền (ví dụ máy người khác không có ổ D:\ hoặc không có user C:\Users\LENOVO)
            }

            // Tự động fallback an toàn về thư mục nội bộ trong project
            return GetSafeFallbackDirectory();
        }

        /// <summary>
        /// Lấy danh sách tất cả các thư mục fallback đã từng lưu trữ
        /// </summary>
        public static List<string> GetFallbackDirectories()
        {
            var settings = LoadSettings();
            var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (settings.FallbackPaths != null)
            {
                foreach (var p in settings.FallbackPaths)
                {
                    try
                    {
                        string resolved = ResolveDirectory(p);
                        if (!string.IsNullOrWhiteSpace(resolved))
                        {
                            string root = Path.GetPathRoot(resolved);
                            if (!string.IsNullOrEmpty(root) && Directory.Exists(root) && Directory.Exists(resolved))
                            {
                                list.Add(resolved);
                            }
                        }
                    }
                    catch { }
                }
            }

            string configPath = WebConfigurationManager.AppSettings["LocalStoragePath"];
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                try
                {
                    string resolved = ResolveDirectory(configPath);
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        string root = Path.GetPathRoot(resolved);
                        if (!string.IsNullOrEmpty(root) && Directory.Exists(root) && Directory.Exists(resolved))
                        {
                            list.Add(resolved);
                        }
                    }
                }
                catch { }
            }

            try
            {
                string safe = GetSafeFallbackDirectory();
                if (Directory.Exists(safe)) list.Add(safe);
            }
            catch { }

            return list.ToList();
        }

        /// <summary>
        /// Cập nhật thư mục lưu trữ media, hỗ trợ tùy chọn di chuyển file hiện có
        /// </summary>
        public static object UpdateStorageDirectory(string newPath, bool moveExistingFiles)
        {
            if (string.IsNullOrWhiteSpace(newPath))
            {
                newPath = "D:\\VideoTimelineData\\Videos";
            }

            newPath = newPath.Trim();

            if (newPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return new { success = false, message = "Đường dẫn chứa ký tự không hợp lệ!" };
            }

            string resolvedNewPath = ResolveDirectory(newPath);

            string currentDir = GetStorageDirectory();

            try
            {
                string root = Path.GetPathRoot(resolvedNewPath);
                if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
                {
                    return new { success = false, message = "Ổ đĩa \"" + root + "\" không tồn tại trên máy tính này!" };
                }

                if (!Directory.Exists(resolvedNewPath))
                {
                    Directory.CreateDirectory(resolvedNewPath);
                }

                // Kiểm tra quyền ghi (write permission)
                string testFile = Path.Combine(resolvedNewPath, ".perm_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                return new { success = false, message = "Không thể tạo hoặc ghi vào thư mục: " + ex.Message };
            }

            // Nếu cùng đường dẫn
            if (string.Equals(Path.GetFullPath(currentDir), Path.GetFullPath(resolvedNewPath), StringComparison.OrdinalIgnoreCase))
            {
                return new { success = true, newPath = resolvedNewPath, movedCount = 0, message = "Thư mục không thay đổi." };
            }

            int movedCount = 0;
            if (moveExistingFiles && Directory.Exists(currentDir))
            {
                try
                {
                    var files = Directory.GetFiles(currentDir);
                    foreach (var srcFile in files)
                    {
                        string fileName = Path.GetFileName(srcFile);
                        string destFile = Path.Combine(resolvedNewPath, fileName);
                        if (!File.Exists(destFile))
                        {
                            File.Move(srcFile, destFile);
                            movedCount++;
                        }
                    }
                }
                catch (Exception)
                {
                    // Bỏ qua lỗi nếu có file đang bị lock
                }
            }

            // Lưu cài đặt mới
            var settings = LoadSettings();
            if (settings.FallbackPaths == null) settings.FallbackPaths = new List<string>();

            if (!settings.FallbackPaths.Contains(currentDir, StringComparer.OrdinalIgnoreCase))
            {
                settings.FallbackPaths.Add(currentDir);
            }
            settings.StoragePath = resolvedNewPath;
            SaveSettings(settings);

            string msg = movedCount > 0
                ? string.Format("Đã chuyển thư mục lưu trữ sang \"{0}\" và di chuyển {1} file video thành công!", resolvedNewPath, movedCount)
                : string.Format("Đã chuyển thư mục lưu trữ sang \"{0}\" thành công!", resolvedNewPath);

            return new { success = true, newPath = resolvedNewPath, movedCount = movedCount, message = msg };
        }

        /// <summary>
        /// Tìm đường dẫn file vật lý của video theo tên file lưu trữ.
        /// Hỗ trợ kiểm tra cả thư mục cấu hình mới, các thư mục fallback và thư mục uploads/videos cũ.
        /// </summary>
        public static string ResolvePhysicalPath(string storedFileName)
        {
            if (string.IsNullOrWhiteSpace(storedFileName)) return null;

            string safeFileName = Path.GetFileName(storedFileName);

            // 1. Kiểm tra trong thư mục cấu hình chính hiện tại
            string primaryDir = GetStorageDirectory();
            try
            {
                string primaryPath = Path.Combine(primaryDir, safeFileName);
                if (File.Exists(primaryPath))
                {
                    return primaryPath;
                }
            }
            catch { }

            // 2. Kiểm tra trong các thư mục fallback đã từng lưu
            var fallbackDirs = GetFallbackDirectories();
            foreach (var fbDir in fallbackDirs)
            {
                if (string.Equals(fbDir, primaryDir, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    string fbPath = Path.Combine(fbDir, safeFileName);
                    if (File.Exists(fbPath))
                    {
                        return fbPath;
                    }
                }
                catch { }
            }

            // 3. Fallback: Kiểm tra thư mục uploads/videos cũ trong root nếu có
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
            return Path.Combine(primaryDir, safeFileName);
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
