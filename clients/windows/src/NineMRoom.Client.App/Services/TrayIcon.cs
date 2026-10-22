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

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr region, bool redraw);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly Icon _icon;
    private readonly IntPtr _iconHandle;
    private readonly HwndSource _source;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _overlayItem;
    private readonly ToolStripMenuItem _clickThroughItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _invisibleItem;
    private readonly uint _id = 1;

    private bool _suppressEvents;
    private bool _added;
    private bool _disposed;

    public TrayIcon()
    {
        _icon = CreateIcon(out _iconHandle);

        _menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            BackColor = Color.FromArgb(28, 28, 32),
            ForeColor = Color.FromArgb(238, 238, 242),
            Font = new Font("Microsoft YaHei UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point),
            ShowImageMargin = false,
            ShowCheckMargin = true,
            Padding = new Padding(5, 5, 5, 5),
            ImageScalingSize = new Size(16, 16)
        };
        _menu.Opened += (_, _) => ApplyRoundedRegion();

        var showItem = new ToolStripMenuItem("显示主窗");
        showItem.Click += (_, _) => ShowRequested?.Invoke();

        _overlayItem = new ToolStripMenuItem("显示叠加层") { CheckOnClick = true };
        _overlayItem.CheckedChanged += (_, _) =>
        {
            if (!_suppressEvents)
            {
                OverlayToggled?.Invoke(_overlayItem.Checked);
            }
        };

        _clickThroughItem = new ToolStripMenuItem("叠加层点击穿透") { CheckOnClick = true };
        _clickThroughItem.CheckedChanged += (_, _) =>
        {
            if (!_suppressEvents)
            {
                ClickThroughToggled?.Invoke(_clickThroughItem.Checked);
            }
        };

        _pauseItem = new ToolStripMenuItem("暂停上报") { CheckOnClick = true };
        _pauseItem.CheckedChanged += (_, _) =>
        {
            if (!_suppressEvents)
            {
                PauseToggled?.Invoke(_pauseItem.Checked);
            }
        };

        _invisibleItem = new ToolStripMenuItem("隐身") { CheckOnClick = true };
        _invisibleItem.CheckedChanged += (_, _) =>
        {
            if (!_suppressEvents)
            {
                InvisibleToggled?.Invoke(_invisibleItem.Checked);
            }
        };

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        foreach (var item in new[]
                 {
                     showItem, _pauseItem, _invisibleItem, _overlayItem, _clickThroughItem, exitItem
                 })
        {
            item.Padding = new Padding(2, 5, 8, 5);
        }

        _menu.Items.Add(showItem);
        _menu.Items.Add(CreateSeparator());
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add(_invisibleItem);
        _menu.Items.Add(CreateSeparator());
        _menu.Items.Add(_overlayItem);
        _menu.Items.Add(_clickThroughItem);
        _menu.Items.Add(CreateSeparator());
        _menu.Items.Add(exitItem);

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

    private static ToolStripSeparator CreateSeparator() => new()
    {
        AutoSize = false,
        Height = 7,
        Margin = new Padding(0, 2, 0, 2)
    };

    private void ApplyRoundedRegion()
    {
        if (_menu.Width <= 0 || _menu.Height <= 0)
        {
            return;
        }

        var region = CreateRoundRectRgn(0, 0, _menu.Width + 1, _menu.Height + 1, 12, 12);
        if (region != IntPtr.Zero)
        {
            SetWindowRgn(_menu.Handle, region, true);
        }
    }

    public void SetOverlayChecked(bool value)
    {
        _suppressEvents = true;
        _overlayItem.Checked = value;
        _suppressEvents = false;
    }

    public void SetClickThroughChecked(bool value)
    {
        _suppressEvents = true;
        _clickThroughItem.Checked = value;
        _suppressEvents = false;
    }

    public void SetPauseChecked(bool value)
    {
        _suppressEvents = true;
        _pauseItem.Checked = value;
        _suppressEvents = false;
    }

    public void SetInvisibleChecked(bool value)
    {
        _suppressEvents = true;
        _invisibleItem.Checked = value;
        _suppressEvents = false;
    }

    public void UpdateTooltip(string text)
    {
        var data = CreateData(NifTip);
        data.szTip = Truncate(text, 127);
        Shell_NotifyIcon(NimModify, ref data);
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
                if (_menu.Visible)
                {
                    _menu.Close();
                    break;
                }

                SetForegroundWindow(_source.Handle);
                _menu.Show(Cursor.Position);
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
        _menu.Dispose();
        _icon.Dispose();

        if (_iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
        }
    }
}
