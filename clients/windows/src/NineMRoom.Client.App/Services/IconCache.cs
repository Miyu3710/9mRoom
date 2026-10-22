using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NineMRoom.Client.App;

public static class IconCache
{
    private static readonly object Gate = new();
    private static readonly string CacheDirectory = ResolveDirectory();
    private static readonly Dictionary<string, string> Index = LoadIndex();

    public static ImageSource? Load(string appId)
    {
        try
        {
            var file = FilePath(appId);
            return File.Exists(file) ? LoadFile(file) : null;
        }
        catch
        {
            return null;
        }
    }

    public static ImageSource? LoadFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetExecutablePath(uint processId, string processName) =>
        TryGetExecutablePath(processId, processName);

    public static string? SaveCustomIcon(string appId, string sourcePath)
    {
        try
        {
            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(appId)))[..16];
            var file = Path.Combine(CacheDirectory, "custom-" + hash + extension.ToLowerInvariant());
            if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }

            File.Copy(sourcePath, file, overwrite: true);
            return file;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsImageFile(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".ico";

    public static bool IsManagedCustomIcon(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            return string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(CacheDirectory), StringComparison.OrdinalIgnoreCase) &&
                   Path.GetFileName(full).StartsWith("custom-", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string? ExtractCustomIcon(string appId, string sourcePath)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(sourcePath);
            if (icon is null)
            {
                return null;
            }

            var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(appId)))[..16];
            var file = Path.Combine(CacheDirectory, "custom-" + hash + ".png");
            using var bitmap = icon.ToBitmap();
            bitmap.Save(file, ImageFormat.Png);
            return file;
        }
        catch
        {
            return null;
        }
    }

    public static ImageSource? GetOrCreate(string appId, uint processId, string processName)
    {
        lock (Gate)
        {
            try
            {
                var file = FilePath(appId);
                var source = TryGetExecutablePath(processId, processName);
                if (source is not null &&
                    (!File.Exists(file) || File.GetLastWriteTimeUtc(file) < File.GetLastWriteTimeUtc(source)))
                {
                    Extract(source, file);
                    UpdateIndex(appId, source);
                }
            }
            catch
            {
                // 图标失败时回退到文字
            }

            return Load(appId);
        }
    }

    private static string? TryGetExecutablePath(uint processId, string processName)
    {
        if (processId != 0)
        {
            try
            {
                using var process = Process.GetProcessById((int)processId);
                var path = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return path;
                }
            }
            catch
            {
                // 受保护进程无法读取
            }
        }

        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        try
        {
            foreach (var process in Process.GetProcessesByName(processName))
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        return path;
                    }
                }
                catch
                {
                    // 跳过受保护进程
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch
        {
            // 枚举失败
        }

        return null;
    }

    private static void Extract(string source, string file)
    {
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(source);
        if (icon is null)
        {
            return;
        }

        using var bitmap = icon.ToBitmap();
        bitmap.Save(file, ImageFormat.Png);
    }

    private static string FilePath(string appId)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(appId));
        return Path.Combine(CacheDirectory, Convert.ToHexString(hash)[..16] + ".png");
    }

    private static void UpdateIndex(string appId, string source)
    {
        try
        {
            Index[appId] = source;
            File.WriteAllText(
                Path.Combine(CacheDirectory, "icons.json"),
                JsonSerializer.Serialize(Index, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 索引失败不影响图标读取
        }
    }

    private static Dictionary<string, string> LoadIndex()
    {
        try
        {
            var path = Path.Combine(CacheDirectory, "icons.json");
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new()
                : new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static string ResolveDirectory()
    {
        foreach (var candidate in Candidates())
        {
            try
            {
                Directory.CreateDirectory(candidate);
                var probe = Path.Combine(candidate, ".write-test");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return candidate;
            }
            catch
            {
                // 尝试下一个位置
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "icons");
    }

    private static IEnumerable<string> Candidates()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
        {
            yield return Path.Combine(local, "9mRoom", "icons");
        }

        yield return Path.Combine(AppContext.BaseDirectory, "icons");
    }
}
