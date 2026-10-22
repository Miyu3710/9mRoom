using System.Text.Json;
using System.Text.RegularExpressions;

namespace NineMRoom.Client.Core;

/// <summary>
/// 状态块渲染器，规则见 shared/status-blocks/README.md。
/// 渲染是纯函数：给定 status、当前时间与 appId 解析器，输出展示文本。
/// </summary>
public static class StatusRenderer
{
    private const int MaxBlockLength = 80;
    private const int MaxLineLength = 200;

    private static readonly Regex PlaceholderRegex = new(@"\{([a-zA-Z_]+)\}", RegexOptions.Compiled);
    private static readonly Regex ControlInline = new(@"[\u0000-\u001F]", RegexOptions.Compiled);
    private static readonly Regex ControlKeepNewline = new(@"[\u0000-\u0009\u000B-\u001F]", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"[ \t]+", RegexOptions.Compiled);

    public static string Render(JsonElement? status, long now, AppCatalog catalog) =>
        Render(status, now, catalog.DisplayName);

    public static string Render(JsonElement? status, long now, Func<string, string> resolveApp)
    {
        if (status is not { } root || root.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var stacked = root.TryGetProperty("layout", out var layout) &&
                      layout.ValueKind == JsonValueKind.String &&
                      layout.GetString() == "stacked";

        var separator = root.TryGetProperty("separator", out var separatorElement) &&
                        separatorElement.ValueKind == JsonValueKind.String
            ? separatorElement.GetString() ?? string.Empty
            : string.Empty;

        var parts = new List<string>();
        foreach (var block in blocks.EnumerateArray())
        {
            var text = RenderBlock(block, now, resolveApp, stacked);
            if (text.Length > 0)
            {
                parts.Add(text);
            }
        }

        var output = stacked ? string.Join("\n", parts) : string.Join(separator, parts);
        return output.Length > MaxLineLength ? output[..(MaxLineLength - 1)] + "…" : output;
    }

    private static string RenderBlock(JsonElement block, long now, Func<string, string> resolveApp, bool stacked)
    {
        if (block.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (!block.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        if (!block.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var template = block.TryGetProperty("template", out var templateElement) &&
                       templateElement.ValueKind == JsonValueKind.String
            ? templateElement.GetString() ?? string.Empty
            : string.Empty;

        if (template.Length == 0)
        {
            return string.Empty;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        switch (typeElement.GetString())
        {
            case "app":
            {
                var appId = Str(data, "appId");
                if (appId.Length == 0)
                {
                    return string.Empty;
                }

                var idle = Long(data, "idleSeconds");
                values["app"] = resolveApp(appId);
                values["category"] = Str(data, "category");
                values["title"] = Str(data, "title");
                values["idle"] = idle >= 60 ? $"空闲{idle / 60}分" : string.Empty;
                break;
            }

            case "media":
            {
                var title = Str(data, "title");
                if (title.Length == 0)
                {
                    return string.Empty;
                }

                values["title"] = title;
                values["artist"] = Str(data, "artist");
                values["progress"] = FormatProgress(data, now);
                break;
            }

            case "todo":
                var todo = Str(data, "text");
                if (todo.Length == 0)
                {
                    return string.Empty;
                }

                values["todo"] = todo;
                break;

            case "timer":
            {
                var started = Long(data, "startedAt");
                if (started <= 0)
                {
                    return string.Empty;
                }

                values["elapsed"] = FormatClock(now - started);
                var remaining = string.Empty;
                if (Str(data, "mode") == "countdown")
                {
                    var minutes = Long(data, "durationMinutes");
                    if (minutes > 0)
                    {
                        remaining = FormatClock(Math.Max(0, started + minutes * 60000 - now));
                    }
                }

                values["remaining"] = remaining;
                break;
            }

            case "uptime":
            {
                var boot = Long(data, "startedAt");
                if (boot <= 0)
                {
                    return string.Empty;
                }

                values["uptime"] = FormatUptime(now - boot);
                break;
            }

            case "text":
                var text = Str(data, "text");
                if (text.Length == 0)
                {
                    return string.Empty;
                }

                values["text"] = text;
                break;

            default:
                return string.Empty;
        }

        var output = PlaceholderRegex.Replace(
            template,
            match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : string.Empty);

        output = (stacked ? ControlKeepNewline : ControlInline).Replace(output, string.Empty);
        output = Spaces.Replace(output, " ").Trim();

        return output.Length > MaxBlockLength ? output[..(MaxBlockLength - 1)] + "…" : output;
    }

    private static string FormatProgress(JsonElement data, long now)
    {
        if (!Bool(data, "isPlaying"))
        {
            return string.Empty;
        }

        var duration = Long(data, "durationMs");
        if (duration <= 0)
        {
            return string.Empty;
        }

        var position = Long(data, "positionMs");
        var sampledAt = Long(data, "sampledAt");
        if (sampledAt > 0 && now > sampledAt)
        {
            position += now - sampledAt;
        }

        position = Math.Clamp(position, 0, duration);
        return $"[{FormatClock(position)}/{FormatClock(duration)}]";
    }

    private static string FormatClock(long milliseconds)
    {
        var total = Math.Max(0, milliseconds / 1000);
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes}:{seconds:00}";
    }

    private static string FormatUptime(long milliseconds)
    {
        var total = Math.Max(0, milliseconds / 1000);
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;
        if (hours > 0)
        {
            return $"{hours}小时{minutes}分{seconds}秒";
        }

        return minutes > 0 ? $"{minutes}分{seconds}秒" : $"{seconds}秒";
    }

    private static string Str(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long Long(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt64(out var number)
            ? number
            : 0;

    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
