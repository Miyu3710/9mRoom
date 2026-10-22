using System.Text;
using System.Text.Json;
using NineMRoom.Client.Core;

var options = CliOptions.Parse(args);
Console.OutputEncoding = Encoding.UTF8;

if (!string.IsNullOrEmpty(options.RenderCheck))
{
    Environment.Exit(RunRenderCheck(options.RenderCheck));
}

var catalogPath = Path.Combine(AppContext.BaseDirectory, "catalog.json");
var catalog = AppCatalog.Load(catalogPath);
var bootAt = SystemInfo.BootTimeMs();
var gate = new object();

ForegroundInfo? foreground = null;
MediaInfo? media = null;

using var foregroundWatcher = new ForegroundWatcher();
foregroundWatcher.Changed += info =>
{
    lock (gate)
    {
        foreground = info;
    }
};
lock (gate)
{
    foreground = foregroundWatcher.Current;
}

await using var mediaWatcher = await MediaWatcher.CreateAsync();
mediaWatcher.Changed += info =>
{
    lock (gate)
    {
        media = info;
    }
};
lock (gate)
{
    media = mediaWatcher.Current;
}

Console.WriteLine("9mRoom · Windows client probe");
Console.WriteLine($"catalog   : {catalog.Count} apps ({catalogPath})");
Console.WriteLine($"server    : {options.Server}");
Console.WriteLine($"room/name : {options.Room} / {options.Name}");
Console.WriteLine($"send      : {!options.NoSend}");
Console.WriteLine();

PresenceClient? presence = null;
if (!options.NoSend)
{
    var uri = BuildUri(options);
    presence = new PresenceClient(uri, () =>
    {
        lock (gate)
        {
            var current = foreground;
            var match = current is null
                ? new AppMatch("unknown", "unknown", "other")
                : catalog.Match(current.ProcessName);
            var idle = SystemInfo.IdleTime();
            return new
            {
                mode = StatusComposer.Mode(idle),
                status = StatusComposer.Compose(match, media, idle, bootAt)
            };
        }
    });

    presence.StateChanged += () => Console.WriteLine($"[state] connected={presence.IsConnected}");
    presence.Message += text => Console.WriteLine($"[server] {text}");

    try
    {
        await presence.ConnectAsync(CancellationToken.None);
        Console.WriteLine("[state] connected=true");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[state] connect failed: {ex.Message}");
    }
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var started = DateTimeOffset.UtcNow;
var lastPrint = DateTimeOffset.MinValue;

while (!cancellation.IsCancellationRequested)
{
    if (options.Seconds > 0 && (DateTimeOffset.UtcNow - started).TotalSeconds >= options.Seconds)
    {
        break;
    }

    if ((DateTimeOffset.UtcNow - lastPrint).TotalSeconds >= 3)
    {
        lastPrint = DateTimeOffset.UtcNow;
        var line = Describe();
        Console.WriteLine(line);
        if (!string.IsNullOrEmpty(options.Log))
        {
            File.AppendAllText(options.Log, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    try
    {
        await Task.Delay(200, cancellation.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

if (presence is not null)
{
    await presence.DisposeAsync();
}

Console.WriteLine("[state] stopped");

string Describe()
{
    lock (gate)
    {
        var current = foreground;
        var track = media;
        var idle = SystemInfo.IdleTime();
        var match = current is null ? null : catalog.Match(current.ProcessName);
        var appText = match is null ? "-" : $"{match.Name} ({match.Category})";
        var mediaText = track is null || string.IsNullOrEmpty(track.Title)
            ? "-"
            : $"{track.Title} - {track.Artist} {(track.IsPlaying ? "playing" : "paused")}";
        var title = options.ShowTitle && current is not null ? $" title=\"{current.WindowTitle}\"" : string.Empty;
        var online = presence is { IsConnected: true } ? presence.OnlineCount.ToString() : "-";
        return $"[{DateTime.Now:HH:mm:ss}] app={appText}{title} | media={mediaText} | idle={(int)idle.TotalSeconds}s | online={online}";
    }
}

static int RunRenderCheck(string path)
{
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    if (!document.RootElement.TryGetProperty("cases", out var cases))
    {
        Console.WriteLine("no cases found");
        return 2;
    }

    var total = 0;
    var failures = 0;

    foreach (var testCase in cases.EnumerateArray())
    {
        total++;
        var name = testCase.GetProperty("name").GetString() ?? "?";
        var now = testCase.GetProperty("now").GetInt64();

        var catalog = new Dictionary<string, string>(StringComparer.Ordinal);
        if (testCase.TryGetProperty("catalog", out var catalogElement) && catalogElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in catalogElement.EnumerateObject())
            {
                catalog[property.Name] = property.Value.GetString() ?? property.Name;
            }
        }

        var status = new Dictionary<string, object?>
        {
            ["layout"] = testCase.TryGetProperty("layout", out var layout) ? layout.GetString() : "inline",
            ["separator"] = testCase.TryGetProperty("separator", out var separator) ? separator.GetString() : string.Empty,
            ["blocks"] = testCase.GetProperty("blocks")
        };

        var statusJson = JsonSerializer.Serialize(status);
        using var statusDocument = JsonDocument.Parse(statusJson);
        var expected = testCase.GetProperty("expected").GetString() ?? string.Empty;
        var actual = StatusRenderer.Render(
            statusDocument.RootElement,
            now,
            appId => catalog.TryGetValue(appId, out var displayName) ? displayName : appId);

        if (actual == expected)
        {
            Console.WriteLine($"PASS  {name}");
        }
        else
        {
            failures++;
            Console.WriteLine($"FAIL  {name}");
            Console.WriteLine($"  expected: {expected}");
            Console.WriteLine($"  actual  : {actual}");
        }
    }

    Console.WriteLine($"TOTAL={total} FAILURES={failures}");
    return failures == 0 ? 0 : 1;
}

static Uri BuildUri(CliOptions options)
{
    var builder = new UriBuilder(options.Server) { Path = "/v1/ws" };
    builder.Scheme = builder.Scheme == "https" ? "wss" : "ws";
    var query = new Dictionary<string, string>
    {
        ["room"] = options.Room,
        ["name"] = options.Name,
        ["device"] = "windows",
        ["role"] = "member"
    };
    builder.Query = string.Join("&", query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
    return builder.Uri;
}

internal sealed record CliOptions(
    Uri Server,
    string Room,
    string Name,
    int Seconds,
    bool NoSend,
    bool ShowTitle,
    string? Log,
    string? RenderCheck)
{
    public static CliOptions Parse(string[] args)
    {
        var server = "http://127.0.0.1:5080";
        var room = "demo";
        var name = Environment.UserName;
        var seconds = 0;
        var noSend = false;
        var showTitle = false;
        string? log = null;
        string? renderCheck = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--server" when i + 1 < args.Length:
                    server = args[++i];
                    break;
                case "--room" when i + 1 < args.Length:
                    room = args[++i];
                    break;
                case "--name" when i + 1 < args.Length:
                    name = args[++i];
                    break;
                case "--seconds" when i + 1 < args.Length:
                    int.TryParse(args[++i], out seconds);
                    break;
                case "--log" when i + 1 < args.Length:
                    log = args[++i];
                    break;
                case "--render-check" when i + 1 < args.Length:
                    renderCheck = args[++i];
                    break;
                case "--no-send":
                    noSend = true;
                    break;
                case "--show-title":
                    showTitle = true;
                    break;
            }
        }

        return new CliOptions(new Uri(server), room, name, seconds, noSend, showTitle, log, renderCheck);
    }
}
