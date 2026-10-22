using System.IO;

namespace NineMRoom.Client.App;

public static class LogService
{
    private const long MaxFileBytes = 4 * 1024 * 1024;

    private static bool _cleaned;

    public static string DirectoryPath { get; } = ResolveDirectory();

    public static string FilePath =>
        Path.Combine(DirectoryPath, $"client-{DateTime.Now:yyyyMMdd}.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);

            if (!_cleaned)
            {
                _cleaned = true;
                Cleanup();
            }

            var path = FilePath;
            var info = new FileInfo(path);
            if (info.Exists && info.Length > MaxFileBytes)
            {
                File.Move(
                    path,
                    Path.Combine(DirectoryPath, $"client-{DateTime.Now:yyyyMMdd-HHmmss}.log"),
                    overwrite: true);
            }

            File.AppendAllText(
                FilePath,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }

    private static void Cleanup()
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);

        try
        {
            foreach (var file in Directory.GetFiles(DirectoryPath, "client-*.log"))
            {
                TryDelete(file, cutoff);
            }

            TryDelete(Path.Combine(DirectoryPath, "client.log"), cutoff);
        }
        catch
        {
            // 清理失败不影响写入
        }
    }

    private static void TryDelete(string file, DateTime cutoff)
    {
        try
        {
            if (File.Exists(file) && File.GetLastWriteTimeUtc(file) < cutoff)
            {
                File.Delete(file);
            }
        }
        catch
        {
            // 单个文件失败忽略
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
                // 尝试下一个候选目录
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "logs");
    }

    private static IEnumerable<string> Candidates()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
        {
            yield return Path.Combine(local, "9mRoom", "logs");
        }

        yield return Path.Combine(AppContext.BaseDirectory, "logs");
    }
}
