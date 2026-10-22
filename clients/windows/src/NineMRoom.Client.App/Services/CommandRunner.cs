using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NineMRoom.Client.App;

public static class CommandRunner
{
    public static async Task<string?> RunAsync(string expression, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command -",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            process.Start();
            await process.StandardInput.WriteLineAsync(expression);
            process.StandardInput.Close();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 进程已退出
                }

                return null;
            }

            var output = await outputTask;
            var error = await errorTask;
            var text = string.IsNullOrWhiteSpace(output) ? error : output;
            return string.IsNullOrWhiteSpace(text) ? null : Clean(text);
        }
        catch
        {
            return null;
        }
    }

    private static string Clean(string text)
    {
        var line = text.Replace('\r', ' ').Replace('\n', ' ');
        line = Regex.Replace(line, @"\s+", " ").Trim();
        line = Regex.Replace(line, @"[\u0000-\u001F]", string.Empty);
        return line.Length <= 80 ? line : line[..80];
    }
}
