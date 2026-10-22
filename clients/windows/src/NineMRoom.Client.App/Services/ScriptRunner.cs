using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NineMRoom.Client.App;

public sealed class ScriptRunner
{
    private const int CacheMs = 15_000;
    private const int TimeoutMs = 2_500;
    private const int MaxLength = 120;

    private static readonly Regex Expression = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private readonly ConcurrentDictionary<string, Entry> _cache = new(StringComparer.Ordinal);

    public string Expand(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("${", StringComparison.Ordinal))
        {
            return text;
        }

        return Expression.Replace(text, match => Value(match.Groups[1].Value));
    }

    private string Value(string expression)
    {
        var entry = _cache.GetOrAdd(expression, _ => new Entry());
        var now = Environment.TickCount64;

        lock (entry)
        {
            if (now - entry.UpdatedAt < CacheMs)
            {
                return entry.Value;
            }

            if (!entry.Running)
            {
                entry.Running = true;
                _ = Task.Run(() => Execute(expression, entry));
            }

            return entry.Value;
        }
    }

    private static void Execute(string expression, Entry entry)
    {
        var output = RunPowerShell(expression);
        lock (entry)
        {
            entry.Value = output;
            entry.UpdatedAt = Environment.TickCount64;
            entry.Running = false;
        }
    }

    private static string RunPowerShell(string expression)
    {
        try
        {
            var script =
                "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; " +
                "$OutputEncoding = [System.Text.Encoding]::UTF8; " +
                expression;
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var start = new ProcessStartInfo(
                "powershell.exe",
                "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(start);
            if (process is null)
            {
                return string.Empty;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(TimeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 忽略终止失败
                }

                return string.Empty;
            }

            var output = outputTask.GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(output))
            {
                output = errorTask.GetAwaiter().GetResult();
            }

            output = Whitespace.Replace(output, " ").Trim();
            return output.Length > MaxLength ? output[..(MaxLength - 1)] + "…" : output;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class Entry
    {
        public string Value { get; set; } = string.Empty;
        public long UpdatedAt { get; set; }
        public bool Running { get; set; }
    }
}
