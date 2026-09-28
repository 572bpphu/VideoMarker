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
        private const string DefaultStorageSubdir = "D:\\VideoTimelineData\\Videos";
        private static readonly object _syncLock = new object();

        private static string GetSettingsFilePath()
        {
            string appData = HostingEnvironment.MapPath("~/App_Data")
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            if (!Directory.Exists(appData))
            {
                Directory.CreateDirectory(appData);
            }
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
                    // Fallback to default/config if file read fails
                }

                string configPath = WebConfigurationManager.AppSettings["LocalStoragePath"];
                if (string.IsNullOrWhiteSpace(configPath))
                {
                    configPath = DefaultStorageSubdir;
                }

                return new StorageSettingsModel
                {
                    StoragePath = configPath,
                    FallbackPaths = new List<string> { configPath, DefaultStorageSubdir }
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
                    // Ignore or log error
                }
            }
        }

        /// <summary>
        /// Lấy đường dẫn thư mục lưu trữ media từ cấu hình người dùng (storage_settings.json),
        /// hoặc Web.config, hoặc mặc định. Tự động tạo thư mục nếu chưa tồn tại.
        /// </summary>
        public static string GetStorageDirectory()
        {
            var settings = LoadSettings();
            string configPath = settings.StoragePath;
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
                    if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p)) list.Add(p);
                }
            }

            string configPath = WebConfigurationManager.AppSettings["LocalStoragePath"];
            if (!string.IsNullOrWhiteSpace(configPath) && Directory.Exists(configPath)) list.Add(configPath);
            if (Directory.Exists(DefaultStorageSubdir)) list.Add(DefaultStorageSubdir);

            return list.ToList();
        }

        /// <summary>
        /// Cập nhật thư mục lưu trữ media, hỗ trợ tùy chọn di chuyển file hiện có
        /// </summary>
        public static object UpdateStorageDirectory(string newPath, bool moveExistingFiles)
        {
            if (string.IsNullOrWhiteSpace(newPath))
            {
                return new { success = false, message = "Đường dẫn thư mục không được để trống!" };
            }

            newPath = newPath.Trim();

            if (newPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return new { success = false, message = "Đường dẫn chứa ký tự không hợp lệ!" };
            }

            if (!Path.IsPathRooted(newPath))
            {
                return new { success = false, message = "Vui lòng nhập đường dẫn tuyệt đối đầy đủ (ví dụ: D:\\Videos hoặc E:\\MediaStorage)!" };
            }

            string currentDir = GetStorageDirectory();

            try
            {
                if (!Directory.Exists(newPath))
                {
                    Directory.CreateDirectory(newPath);
                }

                // Kiểm tra quyền ghi (write permission)
                string testFile = Path.Combine(newPath, ".perm_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                return new { success = false, message = "Không thể tạo hoặc ghi vào thư mục: " + ex.Message };
            }

            // Nếu cùng đường dẫn
            if (string.Equals(Path.GetFullPath(currentDir), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase))
            {
                return new { success = true, newPath = newPath, movedCount = 0, message = "Thư mục không thay đổi." };
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
                        string destFile = Path.Combine(newPath, fileName);
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
            settings.StoragePath = newPath;
            SaveSettings(settings);

            string msg = movedCount > 0
                ? string.Format("Đã chuyển thư mục lưu trữ sang \"{0}\" và di chuyển {1} file video thành công!", newPath, movedCount)
                : string.Format("Đã chuyển thư mục lưu trữ sang \"{0}\" thành công!", newPath);

            return new { success = true, newPath = newPath, movedCount = movedCount, message = msg };
        }

        /// <summary>
        /// Tìm đường dẫn file vật lý của video theo tên file lưu trữ.
        /// Hỗ trợ kiểm tra cả thư mục cấu hình mới, các thư mục fallback và thư mục uploads/videos cũ.
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

            // 2. Kiểm tra trong các thư mục fallback đã từng lưu
            var fallbackDirs = GetFallbackDirectories();
            foreach (var fbDir in fallbackDirs)
            {
                if (string.Equals(fbDir, primaryDir, StringComparison.OrdinalIgnoreCase)) continue;
                string fbPath = Path.Combine(fbDir, safeFileName);
                if (File.Exists(fbPath))
                {
                    return fbPath;
                }
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
