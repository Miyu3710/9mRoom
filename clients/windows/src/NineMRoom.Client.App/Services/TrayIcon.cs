using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;

namespace NineMRoom.Client.App;

public sealed class TrayIcon : IDisposable
{
    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;
    private const int NifInfo = 0x00000010;

    private const int NimAdd = 0x00000000;
    private const int NimModify = 0x00000001;
    private const int NimDelete = 0x00000002;

    private const int WmApp = 0x8000;
    private const int CallbackMessage = WmApp + 1;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly Icon _icon;
    private readonly IntPtr _iconHandle;
    private readonly HwndSource _source;
    private readonly uint _id = 1;

    private TrayMenuWindow? _menu;
    private string _statusText = "正在运行";
    private bool _pauseChecked;
    private bool _invisibleChecked;
    private bool _overlayChecked;
    private bool _clickThroughChecked;
    private bool _added;
    private bool _disposed;

    public TrayIcon()
    {
        _icon = CreateIcon(out _iconHandle);

        var parameters = new HwndSourceParameters("9mRoomTray")
        {
            ParentWindow = IntPtr.Zero,
            WindowStyle = 0,
            ExtendedWindowStyle = 0x00000080, // WS_EX_TOOLWINDOW
            Width = 0,
            Height = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        _added = AddIcon();
    }

    public event Action? ShowRequested;

    public event Action? ExitRequested;

    public event Action<bool>? OverlayToggled;

    public event Action<bool>? ClickThroughToggled;

    public event Action<bool>? PauseToggled;

    public event Action<bool>? InvisibleToggled;

    public bool IsAvailable => _added;

    public void SetOverlayChecked(bool value)
    {
        _overlayChecked = value;
        if (_menu is not null)
        {
            _menu.SetState(_pauseChecked, _invisibleChecked, _overlayChecked, _clickThroughChecked);
        }
    }

    public void SetClickThroughChecked(bool value)
    {
        _clickThroughChecked = value;
        if (_menu is not null)
        {
            _menu.SetState(_pauseChecked, _invisibleChecked, _overlayChecked, _clickThroughChecked);
        }
    }

    public void SetPauseChecked(bool value)
    {
        _pauseChecked = value;
        if (_menu is not null)
        {
            _menu.SetState(_pauseChecked, _invisibleChecked, _overlayChecked, _clickThroughChecked);
        }
    }

    public void SetInvisibleChecked(bool value)
    {
        _invisibleChecked = value;
        if (_menu is not null)
        {
            _menu.SetState(_pauseChecked, _invisibleChecked, _overlayChecked, _clickThroughChecked);
        }
    }

    public void UpdateTooltip(string text)
    {
        _statusText = string.IsNullOrWhiteSpace(text) ? "正在运行" : text;
        _menu?.SetStatus(_statusText);

        var data = CreateData(NifTip);
        data.szTip = Truncate(text, 127);
        Shell_NotifyIcon(NimModify, ref data);
    }

    private void ShowMenu()
    {
        SetForegroundWindow(_source.Handle);

        _menu ??= CreateMenu();
        _menu.SetState(_pauseChecked, _invisibleChecked, _overlayChecked, _clickThroughChecked);
        _menu.SetStatus(_statusText);
        _menu.ShowAt(Cursor.Position);
    }

    private TrayMenuWindow CreateMenu()
    {
        var menu = new TrayMenuWindow();
        menu.ShowRequested += () => ShowRequested?.Invoke();
        menu.ExitRequested += () => ExitRequested?.Invoke();
        menu.PauseToggled += value =>
        {
            _pauseChecked = value;
            PauseToggled?.Invoke(value);
        };
        menu.InvisibleToggled += value =>
        {
            _invisibleChecked = value;
            InvisibleToggled?.Invoke(value);
        };
        menu.OverlayToggled += value =>
        {
            _overlayChecked = value;
            OverlayToggled?.Invoke(value);
        };
        menu.ClickThroughToggled += value =>
        {
            _clickThroughChecked = value;
            ClickThroughToggled?.Invoke(value);
        };
        return menu;
    }

    public void ShowBalloon(string title, string text)
    {
        var data = CreateData(NifInfo);
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(text, 255);
        data.dwInfoFlags = 0x00000001; // NIIF_INFO
        Shell_NotifyIcon(NimModify, ref data);
    }

    private bool AddIcon()
    {
        var data = CreateData(NifMessage | NifIcon | NifTip);
        return Shell_NotifyIcon(NimAdd, ref data);
    }

    private NotifyIconData CreateData(int flags) => new()
    {
        cbSize = Marshal.SizeOf<NotifyIconData>(),
        hWnd = _source.Handle,
        uID = _id,
        uFlags = (uint)flags,
        uCallbackMessage = CallbackMessage,
        hIcon = _iconHandle,
        szTip = "9mRoom",
        szInfo = string.Empty,
        szInfoTitle = string.Empty
    };

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != CallbackMessage)
        {
            return IntPtr.Zero;
        }

        var mouseMessage = lParam.ToInt64() & 0xFFFF;
        switch (mouseMessage)
        {
            case WmLButtonUp:
            case WmLButtonDblClk:
                ShowRequested?.Invoke();
                break;
            case WmRButtonUp:
                if (_menu is { IsVisible: true })
                {
                    _menu.Close();
                    break;
                }

                ShowMenu();
                break;
        }

        handled = true;
        return IntPtr.Zero;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static Icon CreateIcon(out IntPtr handle)
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(executable))
            {
                using var associated = Icon.ExtractAssociatedIcon(executable);
                if (associated is not null)
                {
                    using var bitmap = associated.ToBitmap();
                    handle = bitmap.GetHicon();
                    return Icon.FromHandle(handle);
                }
            }
        }
        catch
        {
            // 图标提取失败时使用内置图标
        }

        using var fallback = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(fallback))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(0x7C, 0x9C, 0xFF));
            graphics.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            TextRenderer.DrawText(
                graphics,
                "9",
                font,
                new Rectangle(0, 0, 32, 32),
                Color.FromArgb(0x0F, 0x11, 0x15),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        handle = fallback.GetHicon();
        return Icon.FromHandle(handle);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIcon(int message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_added)
        {
            var data = CreateData(0);
            Shell_NotifyIcon(NimDelete, ref data);
            _added = false;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
        _menu?.Close();
        _icon.Dispose();

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
        }
    }
}
