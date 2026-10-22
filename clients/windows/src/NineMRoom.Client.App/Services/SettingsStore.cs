using System.IO;
using System.Text.Json;

namespace NineMRoom.Client.App;

public sealed class AppSettings
{
    public string ServerBase { get; set; } = "http://127.0.0.1:5080";
    public string Room { get; set; } = "demo";
    public string RoomName { get; set; } = string.Empty;
    public string HomeRoom { get; set; } = string.Empty;
    public string Name { get; set; } = Environment.UserName;
    public string ClientId { get; set; } = string.Empty;
    public bool AutoStart { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool CloseToTrayHintShown { get; set; }
    public bool AutoConnect { get; set; } = true;
    public bool ShareMusic { get; set; } = true;
    public bool ShareWindowTitles { get; set; }
    public string TitleWhitelistJson { get; set; } = string.Empty;
    public bool NotifyOnClose { get; set; } = true;
    public bool NotifyRoomEvents { get; set; } = true;
    public string TodoText { get; set; } = string.Empty;
    public string TodosJson { get; set; } = string.Empty;
    public string StatusLayoutJson { get; set; } = string.Empty;
    public string AppTemplatesJson { get; set; } = string.Empty;
    public string AppRulesJson { get; set; } = string.Empty;
    public string AppCategoriesJson { get; set; } = string.Empty;
    public string AppSortMode { get; set; } = "recent";
    public bool StatsEnabled { get; set; } = true;
    public string StatsHistoryJson { get; set; } = string.Empty;
    public int DailyGoalMinutes { get; set; } = 120;
    public bool CommandEnabled { get; set; }
    public string CommandExpression { get; set; } = string.Empty;
    public bool OverlayEnabled { get; set; } = true;
    public bool OverlayClickThrough { get; set; }
    public string OverlayPreset { get; set; } = "card";
    public string OverlayThemeJson { get; set; } = string.Empty;
    public double? OverlayLeft { get; set; }
    public double? OverlayTop { get; set; }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly Dictionary<string, bool> WritableDirectories = new(StringComparer.OrdinalIgnoreCase);

    public static string CurrentPath { get; private set; } = string.Empty;

    public static AppSettings Load()
    {
        foreach (var path in Candidates())
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory) || !CanWriteDirectory(directory))
                {
                    continue;
                }

                if (!File.Exists(path))
                {
                    continue;
                }

                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                if (settings is not null)
                {
                    CurrentPath = path;
                    return settings;
                }
            }
            catch
            {
                // 损坏的配置直接跳过，使用默认值
            }
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        foreach (var path in Candidates())
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory) || !CanWriteDirectory(directory))
                {
                    continue;
                }

                Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonSerializer.Serialize(settings, Json));
                CurrentPath = path;
                return;
            }
            catch
            {
                // 尝试下一个候选位置
            }
        }
    }

    private static bool CanWriteDirectory(string directory)
    {
        if (WritableDirectories.TryGetValue(directory, out var cached))
        {
            return cached;
        }

        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".write-test");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            WritableDirectories[directory] = true;
            return true;
        }
        catch
        {
            WritableDirectories[directory] = false;
            return false;
        }
    }

    private static IEnumerable<string> Candidates()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            yield return Path.Combine(appData, "9mRoom", "settings.json");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            yield return Path.Combine(localAppData, "9mRoom", "settings.json");
        }

        yield return Path.Combine(AppContext.BaseDirectory, "settings.json");
    }
}
