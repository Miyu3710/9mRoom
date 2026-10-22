using System.Text.Json;

namespace NineMRoom.Client.Core;

public sealed record AppMatch(string Id, string Name, string Category);

public sealed class AppCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, AppMatch> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _displayNames = new(StringComparer.OrdinalIgnoreCase);

    public int Count { get; private set; }

    public static AppCatalog Load(string path)
    {
        var catalog = new AppCatalog();
        if (!File.Exists(path))
        {
            return catalog;
        }

        var file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path), JsonOptions);
        if (file is null)
        {
            return catalog;
        }

        foreach (var app in file.Apps)
        {
            var match = new AppMatch(app.Id, app.Name, string.IsNullOrWhiteSpace(app.Category) ? "other" : app.Category);
            catalog._displayNames[app.Id] = app.Name;
            foreach (var alias in app.Aliases.Windows.Concat(app.Aliases.Android))
            {
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    catalog._aliases[alias] = match;
                }
            }

            catalog.Count++;
        }

        return catalog;
    }

    public string DisplayName(string appId) =>
        _displayNames.TryGetValue(appId, out var name) ? name : appId;

    public AppMatch Match(string processName)
    {
        var key = processName.Trim();
        if (_aliases.TryGetValue(key, out var match))
        {
            return match;
        }

        if (!key.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && _aliases.TryGetValue(key + ".exe", out match))
        {
            return match;
        }

        return new AppMatch(key.ToLowerInvariant(), key, "other");
    }

    private sealed class CatalogFile
    {
        public int Version { get; set; }
        public List<AppFile> Apps { get; set; } = new();
    }

    private sealed class AppFile
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "other";
        public AliasFile Aliases { get; set; } = new();
    }

    private sealed class AliasFile
    {
        public string[] Windows { get; set; } = Array.Empty<string>();
        public string[] Android { get; set; } = Array.Empty<string>();
    }
}
