namespace NineMRoom.Client.Core;

public sealed record FocusTimer(string Mode, long StartedAt, int DurationMinutes);

public sealed class StatusBlockRule
{
    public string Type { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string Template { get; set; } = string.Empty;
}

public static class StatusLayouts
{
    public static List<StatusBlockRule> Default() => new()
    {
        new() { Type = "app", Enabled = true, Template = StatusComposer.AppTemplate },
        new() { Type = "media", Enabled = true, Template = StatusComposer.MediaTemplate },
        new() { Type = "timer", Enabled = true, Template = StatusComposer.TimerTemplate },
        new() { Type = "todo", Enabled = true, Template = StatusComposer.TodoTemplate }
    };

    public static string Label(string type) => type switch
    {
        "app" => "应用",
        "media" => "音乐",
        "timer" => "计时",
        "todo" => "待办",
        "text" => "自定义",
        _ => type
    };
}

public static class StatusComposer
{
    public const string AppTemplate = "📡 {app}";
    public const string MediaTemplate = "🎶{title} - {artist} {progress}";
    public const string TimerTemplate = "⏱ {remaining}";
    public const string TodoTemplate = "📚 {todo}";

    public static object Compose(
        AppMatch app,
        MediaInfo? media,
        TimeSpan idle,
        long bootTimeMs,
        FocusTimer? timer = null,
        string? todo = null,
        string? commandOutput = null,
        IReadOnlyList<StatusBlockRule>? layout = null)
    {
        var rules = layout is { Count: > 0 } ? layout : StatusLayouts.Default();
        var blocks = new List<object>();

        foreach (var rule in rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            var block = BuildBlock(rule, app, media, idle, bootTimeMs, timer, todo, commandOutput);
            if (block is not null)
            {
                blocks.Add(block);
            }
        }

        return new
        {
            layout = "inline",
            separator = " | ",
            blocks
        };
    }

    private static object? BuildBlock(
        StatusBlockRule rule,
        AppMatch app,
        MediaInfo? media,
        TimeSpan idle,
        long bootTimeMs,
        FocusTimer? timer,
        string? todo,
        string? commandOutput)
    {
        var template = string.IsNullOrWhiteSpace(rule.Template) ? null : rule.Template;

        switch (rule.Type)
        {
            case "app":
                return new
                {
                    id = "app",
                    type = "app",
                    template = template ?? AppTemplate,
                    data = new
                    {
                        appId = app.Id,
                        category = app.Category,
                        idleSeconds = (int)idle.TotalSeconds
                    }
                };

            case "media" when media is { Title.Length: > 0 }:
                return new
                {
                    id = "media",
                    type = "media",
                    template = template ?? MediaTemplate,
                    data = new
                    {
                        title = media.Title,
                        artist = media.Artist,
                        isPlaying = media.IsPlaying,
                        positionMs = (long)media.PositionMs,
                        durationMs = (long)media.DurationMs,
                        sampledAt = media.SampledAt
                    }
                };

            case "timer" when timer is not null:
                return new
                {
                    id = "timer",
                    type = "timer",
                    template = template ?? TimerTemplate,
                    data = new
                    {
                        mode = timer.Mode,
                        startedAt = timer.StartedAt,
                        durationMinutes = timer.DurationMinutes
                    }
                };

            case "todo" when !string.IsNullOrWhiteSpace(todo):
                return new
                {
                    id = "todo",
                    type = "todo",
                    template = template ?? TodoTemplate,
                    data = new { text = todo.Trim() }
                };

            case "text" when !string.IsNullOrWhiteSpace(rule.Template):
                return new
                {
                    id = "text",
                    type = "text",
                    template = rule.Template,
                    data = new { text = rule.Template }
                };

            default:
                return null;
        }
    }

    public static string Mode(TimeSpan idle) =>
        idle >= TimeSpan.FromMinutes(5) ? "away" : "focusing";
}
