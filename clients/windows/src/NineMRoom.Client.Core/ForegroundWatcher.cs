namespace NineMRoom.Client.Core;

public sealed record ForegroundInfo(string ProcessName, string WindowTitle, IntPtr Handle, uint ProcessId);

public sealed class ForegroundWatcher : IDisposable
{
    private readonly Timer _timer;
    private readonly string _selfProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
    private ForegroundInfo? _last;
    private bool _disposed;

    public ForegroundWatcher(int intervalMs = 1000)
    {
        _timer = new Timer(_ => Poll(), null, 0, intervalMs);
    }

    public ForegroundInfo? Current => _last;

    public event Action<ForegroundInfo>? Changed;

    private void Poll()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var current = Native.GetForeground();
            if (current is null || current == _last)
            {
                return;
            }

            // 自己的窗口（包括叠加层与主窗）不参与状态，避免“正在用 9mRoom”覆盖真实应用
            if (current.ProcessName.Equals(_selfProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _last = current;
            Changed?.Invoke(current);
        }
        catch
        {
            // 采集失败不应影响后续轮询
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }
}
