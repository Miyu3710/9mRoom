using System.Runtime.InteropServices;
using System.Text;

namespace NineMRoom.Client.Core;

internal static class Native
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int MaxPath = 1024;

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    private delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumChildProc callback, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder exeName, ref uint size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    internal static string ProcessName(uint processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            return "protected";
        }

        try
        {
            var buffer = new StringBuilder(MaxPath);
            uint size = MaxPath;
            return QueryFullProcessImageName(handle, 0, buffer, ref size)
                ? Path.GetFileNameWithoutExtension(buffer.ToString())
                : "unknown";
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    internal static ForegroundInfo? GetForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(hwnd, out var processId);
        var name = ProcessName(processId);

        if (name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
        {
            var childProcess = FindChildProcess(hwnd, processId);
            if (childProcess != 0)
            {
                processId = childProcess;
                name = ProcessName(processId);
            }
        }

        var title = new StringBuilder(512);
        GetWindowText(hwnd, title, title.Capacity);
        return new ForegroundInfo(name, title.ToString(), hwnd, processId);
    }

    private static uint FindChildProcess(IntPtr parent, uint exclude)
    {
        uint found = 0;
        EnumChildWindows(parent, (child, _) =>
        {
            GetWindowThreadProcessId(child, out var childProcess);
            if (childProcess == 0 || childProcess == exclude)
            {
                return true;
            }

            var name = ProcessName(childProcess);
            if (name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            found = childProcess;
            return false;
        }, IntPtr.Zero);

        return found;
    }

    internal static TimeSpan IdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var now = unchecked((uint)GetTickCount64());
        return TimeSpan.FromMilliseconds(unchecked(now - info.Time));
    }

    internal static long SystemBootTimeMs() =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)GetTickCount64();
}
