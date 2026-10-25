using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MaterialDesignThemes.Wpf;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using RadioButton = System.Windows.Controls.RadioButton;
using Separator = System.Windows.Controls.Separator;
using WpfApplication = System.Windows.Application;

namespace NineMRoom.Client.App;

public partial class MainWindow : Window
{
    private const int WmHotkey = 0x0312;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int GwlStyle = -16;
    private const int WsCaption = 0x00C00000;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint MonitorDefaultToNearest = 2;
    private const int ModAlt = 0x0001;
    private const int ModControl = 0x0002;
    private const int ModShift = 0x0004;
    private const int ModNoRepeat = 0x4000;
    private const int HotkeyTimer = 0x9A01;
    private const int HotkeyOverlay = 0x9A02;
    private const int VkT = 0x54;
    private const int VkH = 0x48;


    private readonly AppSettings _settings;
    private readonly MainViewModel _viewModel;
    private readonly TrayIcon _tray;
    private readonly OverlayWindow _overlay;
    private UIElement? _visiblePage;
    private HwndSource? _hotkeySource;
    private bool _exiting;
    private bool _suppressMemberSelection;

    public MainWindow(AppSettings settings, bool startMinimized)
    {
        _settings = settings;
        InitializeComponent();
        _visiblePage = RoomPage;

        _viewModel = new MainViewModel(settings);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        _tray = new TrayIcon();
        _tray.ShowRequested += ShowFromTray;
        _tray.ExitRequested += ExitApplication;
        _tray.OverlayToggled += enabled => _viewModel.OverlayEnabled = enabled;
        _tray.ClickThroughToggled += enabled => _viewModel.OverlayClickThrough = enabled;
        _tray.PauseToggled += paused => _viewModel.SetPaused(paused);
        _tray.InvisibleToggled += invisible => _viewModel.SetManualMode(invisible ? "invisible" : "auto");
        _viewModel.SystemNotificationRequested += (title, text) => _tray.ShowBalloon(title, text);
        _tray.SetPauseChecked(_viewModel.IsPaused);
        _tray.SetInvisibleChecked(_viewModel.ManualMode == "invisible");
        _tray.SetOverlayChecked(_settings.OverlayEnabled);
        _tray.SetClickThroughChecked(_settings.OverlayClickThrough);
        _viewModel.TrayAvailable = _tray.IsAvailable;

        if (!_tray.IsAvailable)
        {
            LogService.Write("tray unavailable: restricted integrity");
            _viewModel.ShowPersistentNotice("托盘图标不可用：当前进程以受限权限运行，关闭窗口将直接退出。请从桌面运行 9mRoom.cmd。");
        }

        _overlay = new OverlayWindow(_viewModel, _settings);
        _viewModel.PropertyChanged += (_, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(MainViewModel.TrayText):
                    _tray.UpdateTooltip(_viewModel.TrayText);
                    break;
                case nameof(MainViewModel.OverlayEnabled):
                    _tray.SetOverlayChecked(_viewModel.OverlayEnabled);
                    break;
                case nameof(MainViewModel.OverlayClickThrough):
                    _tray.SetClickThroughChecked(_viewModel.OverlayClickThrough);
                    break;
                case nameof(MainViewModel.IsPaused):
                    _tray.SetPauseChecked(_viewModel.IsPaused);
                    break;
                case nameof(MainViewModel.ManualMode):
                    _tray.SetInvisibleChecked(_viewModel.ManualMode == "invisible");
                    break;
                case nameof(MainViewModel.IsMemberDrawerOpen):
                    AnimateMemberDrawer(_viewModel.IsMemberDrawerOpen);
                    break;
            }
        };

        _viewModel.OverlayVisibilityChanged += () =>
        {
            if (_viewModel.OverlayEnabled)
            {
                _overlay.Show();
            }
            else
            {
                _overlay.Hide();
            }
        };
        _viewModel.OverlayClickThroughChanged += () => _overlay.ClickThrough = _viewModel.OverlayClickThrough;
        _viewModel.OverlayPositionResetRequested += () => _overlay.ResetPosition();
        _viewModel.OverlayThemeChanged += () => _overlay.ApplyTheme(_viewModel.GetEffectiveTheme());

        Closing += OnClosing;
        Closed += (_, _) => UnregisterHotKeys();
        SourceInitialized += (_, _) =>
        {
            RegisterHotKeys();
            WindowBackdrop.Apply(this);
            EnsureCaptionStyle();
        };

        if (startMinimized)
        {
            _ = _viewModel.InitializeAsync();
            _ = _viewModel.CheckForUpdatesOnStartupAsync();
        }
        else
        {
            Loaded += async (_, _) =>
            {
                await _viewModel.InitializeAsync();
                await _viewModel.CheckForUpdatesOnStartupAsync();
            };
        }

        if (_settings.OverlayEnabled)
        {
            _overlay.Show();
        }
    }

    public bool TrayAvailable => _tray.IsAvailable;

    public void ShowFromTray()
    {
        Show();

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting)
        {
            return;
        }

        if (!_tray.IsAvailable)
        {
            ExitApplication();
            return;
        }

        if (!_settings.CloseToTray)
        {
            ExitApplication();
            return;
        }

        e.Cancel = true;
        Hide();

        if (!_settings.CloseToTrayHintShown)
        {
            _settings.CloseToTrayHintShown = true;
            SettingsStore.Save(_settings);

            if (_settings.NotifyOnClose)
            {
                _tray.ShowBalloon("9mRoom 仍在后台运行", "双击托盘图标重新打开，右键菜单可以退出。");
            }
        }
    }

    private void RegisterHotKeys()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _hotkeySource = HwndSource.FromHwnd(handle);
        _hotkeySource?.AddHook(HotkeyWndProc);

        if (!RegisterHotKey(handle, HotkeyTimer, ModControl | ModAlt | ModShift | ModNoRepeat, VkT))
        {
            LogService.Write($"hotkey register failed: Ctrl+Alt+Shift+T (error {Marshal.GetLastWin32Error()})");
        }

        if (!RegisterHotKey(handle, HotkeyOverlay, ModControl | ModAlt | ModNoRepeat, VkH))
        {
            LogService.Write("hotkey register failed: Ctrl+Alt+H");
        }
    }

    private void UnregisterHotKeys()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            UnregisterHotKey(handle, HotkeyTimer);
            UnregisterHotKey(handle, HotkeyOverlay);
        }

        _hotkeySource?.RemoveHook(HotkeyWndProc);
        _hotkeySource = null;
    }

    private IntPtr HotkeyWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            ApplyMaximizeBounds(lParam);
            return IntPtr.Zero;
        }

        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        switch (wParam.ToInt32())
        {
            case HotkeyTimer:
                _viewModel.ToggleTimer();
                handled = true;
                break;
            case HotkeyOverlay:
                _viewModel.OverlayEnabled = !_viewModel.OverlayEnabled;
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void EnsureCaptionStyle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlStyle);
        if ((style & WsCaption) == WsCaption)
        {
            return;
        }

        SetWindowLong(handle, GwlStyle, style | WsCaption);
        SetWindowPos(
            handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    private void ApplyMaximizeBounds(IntPtr lParam)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        minMax.MaxPosition.X = info.Work.Left - info.Monitor.Left;
        minMax.MaxPosition.Y = info.Work.Top - info.Monitor.Top;
        minMax.MaxSize.X = info.Work.Right - info.Work.Left;
        minMax.MaxSize.Y = info.Work.Bottom - info.Work.Top;
        Marshal.StructureToPtr(minMax, lParam, false);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    private void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        SettingsStore.Save(_settings);
        _overlay.Close();
        _viewModel.Dispose();
        _tray.Dispose();
        WpfApplication.Current.Shutdown();
    }

    // ==================== 应用栏 ====================

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // 最大化状态下拖动可能失败
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        SystemCommands.MinimizeWindow(this);

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (MaximizeIcon is not null)
        {
            MaximizeIcon.Kind = WindowState == WindowState.Maximized
                ? PackIconKind.WindowRestore
                : PackIconKind.WindowMaximize;
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 导航栏展开/收起由 rail 顶部的切换按钮控制，不再随窗口宽度自动切换。
    }



    private void Overflow_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = OverflowButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        var hide = new MenuItem
        {
            Header = "隐藏到系统托盘",
            Icon = new PackIcon { Kind = PackIconKind.TrayArrowDown, Width = 20, Height = 20 },
            IsEnabled = _tray.IsAvailable,
            InputGestureText = "关闭窗口"
        };
        hide.Click += (_, _) => Hide();

        var exit = new MenuItem
        {
            Header = "退出 9mRoom",
            Icon = new PackIcon { Kind = PackIconKind.ExitToApp, Width = 20, Height = 20 }
        };
        exit.Click += (_, _) => ExitApplication();

        menu.Items.Add(hide);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        menu.IsOpen = true;
    }

    // ==================== 导航 ====================

    private void NavRail_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavRail.SelectedItem is not Controls.NavDestination destination)
        {
            return;
        }

        ShowPage(destination.Tag switch
        {
            "focus" => FocusPage,
            "todo" => TodoPage,
            "layout" => LayoutPage,
            "apps" => AppPage,
            "settings" => SettingsPage,
            _ => RoomPage
        });
    }

    private void ShowPage(UIElement page)
    {
        if (RoomPage is null || FocusPage is null || TodoPage is null ||
            LayoutPage is null || AppPage is null || SettingsPage is null)
        {
            return;
        }

        if (ReferenceEquals(_visiblePage, page))
        {
            return;
        }

        var previous = _visiblePage;
        _visiblePage = page;

        if (previous is null)
        {
            // 首次切换时把其他页面全部收起，避免旧页面残留在窗口里。
            foreach (var candidate in new UIElement[] { RoomPage, FocusPage, TodoPage, LayoutPage, AppPage, SettingsPage })
            {
                if (!ReferenceEquals(candidate, page))
                {
                    candidate.Visibility = Visibility.Collapsed;
                    candidate.Opacity = 1;
                    candidate.IsHitTestVisible = true;
                }
            }

            page.Visibility = Visibility.Visible;
            page.Opacity = 1;
            return;
        }

        // 新页面淡入，旧页面同时淡出，避免切换瞬间硬切。
        page.Visibility = Visibility.Visible;
        page.IsHitTestVisible = true;
        page.BeginAnimation(OpacityProperty, null);
        page.Opacity = 0;
        FadePage(page, 1, 180);

        previous.IsHitTestVisible = false;
        if (previous.Visibility != Visibility.Visible)
        {
            return;
        }

        FadePage(previous, 0, 140, () => CollapsePage(previous));
    }

    private void CollapsePage(UIElement page)
    {
        if (ReferenceEquals(_visiblePage, page))
        {
            return;
        }

        page.BeginAnimation(OpacityProperty, null);
        page.Opacity = 1;
        page.Visibility = Visibility.Collapsed;
        page.IsHitTestVisible = true;
    }

    private static void FadePage(UIElement page, double to, int milliseconds, Action? completed = null)
    {
        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };

        if (completed is not null)
        {
            animation.Completed += (_, _) => completed();
        }

        page.BeginAnimation(OpacityProperty, animation);
    }

    // ==================== 房间页签 ====================

    private void RoomTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StateTabContent is null || RoomTabContent is null ||
            LogTabContent is null || ChatTabContent is null)
        {
            return;
        }

        var index = RoomTabs.SelectedIndex;
        StateTabContent.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        RoomTabContent.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        LogTabContent.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        ChatTabContent.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ==================== 连接与房间 ====================

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        // 断开服务器会离开房间并停止同步：房间内必须先确认。
        if (_viewModel.IsServerConnected && _viewModel.IsInRoom)
        {
            var confirmed = await DialogHost.ConfirmAsync(
                "断开服务器？",
                "你正在房间中。断开后会离开房间并停止同步状态，其他成员将看到你离线。",
                "断开服务器",
                "取消",
                destructive: true);

            if (!confirmed)
            {
                return;
            }
        }

        await _viewModel.ToggleConnectionAsync();
    }

    private async void LeaveRoom_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = await DialogHost.ConfirmAsync(
            "离开房间？",
            "离开后会停止向房间同步状态，房间中的其他成员将看到你离线。你可以随时重新加入。",
            "离开房间",
            "取消",
            destructive: true);

        if (confirmed)
        {
            await _viewModel.LeaveRoomAsync();
        }
    }

    private void OpenJoin_Click(object sender, RoutedEventArgs e) => _viewModel.OpenJoinDialog();

    private void JoinOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, sender))
        {
            _viewModel.CloseJoinDialog();
        }
    }

    private void CopyRoomCode_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsInRoom || string.IsNullOrWhiteSpace(_viewModel.Room))
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(_viewModel.Room);
            _viewModel.ShowRoomCodeNotice("房间码已复制到剪贴板");
        }
        catch
        {
            _viewModel.ShowRoomCodeNotice("复制失败：无法访问剪贴板。");
        }
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string mode } && _viewModel is not null)
        {
            _viewModel.SetManualMode(mode);
        }
    }

    // ==================== 成员详情 ====================

    private void MemberList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressMemberSelection || MemberList.SelectedItem is not MemberItem member)
        {
            return;
        }

        _viewModel.OpenMemberDrawer(member);
    }

    private void CloseDrawer_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CloseMemberDrawer();
        ClearMemberSelection();
    }

    private void MemberDrawerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ReferenceEquals(e.OriginalSource, sender))
        {
            return;
        }

        _viewModel.CloseMemberDrawer();
        ClearMemberSelection();
    }

    private void ClearMemberSelection()
    {
        if (MemberList is null)
        {
            return;
        }

        _suppressMemberSelection = true;
        MemberList.SelectedIndex = -1;
        _suppressMemberSelection = false;
    }

    private async void PokeMember_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.PokeSelectedAsync();
    }

    private void AnimateMemberDrawer(bool open)
    {
        var duration = TimeSpan.FromMilliseconds(200);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        if (open)
        {
            MemberDrawerOverlay.Visibility = Visibility.Visible;
            var openFade = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
            openFade.Completed += (_, _) =>
            {
                if (!_viewModel.IsMemberDrawerOpen)
                {
                    return;
                }

                MemberDrawerOverlay.BeginAnimation(OpacityProperty, null);
                MemberDrawerOverlay.Opacity = 1;

                if (MemberDrawerPanel.RenderTransform is TranslateTransform completed)
                {
                    completed.BeginAnimation(TranslateTransform.XProperty, null);
                    completed.X = 0;
                }
            };
            MemberDrawerOverlay.BeginAnimation(OpacityProperty, openFade);

            if (MemberDrawerPanel.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(
                    TranslateTransform.XProperty,
                    new DoubleAnimation(400, 0, duration) { EasingFunction = ease });
            }

            return;
        }

        if (MemberDrawerOverlay.Visibility != Visibility.Visible)
        {
            return;
        }

        var fade = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        fade.Completed += (_, _) =>
        {
            if (!_viewModel.IsMemberDrawerOpen)
            {
                MemberDrawerOverlay.Visibility = Visibility.Collapsed;
            }
        };
        MemberDrawerOverlay.BeginAnimation(OpacityProperty, fade);

        if (MemberDrawerPanel.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(0, 400, duration) { EasingFunction = ease });
        }
    }

    // ==================== 应用规则面板 ====================

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsAppEditorOpen):
                AnimateAppEditor();
                break;
            case nameof(MainViewModel.NoticeText) when !string.IsNullOrWhiteSpace(_viewModel.NoticeText):
                Snackbar.Show(_viewModel.NoticeText);
                break;
            case nameof(MainViewModel.RoomCodeNotice) when !string.IsNullOrWhiteSpace(_viewModel.RoomCodeNotice):
                Snackbar.Show(_viewModel.RoomCodeNotice, seconds: 3);
                break;
        }
    }

    private void AnimateAppEditor()
    {
        if (_viewModel.IsAppEditorOpen)
        {
            AppEditorOverlay.Visibility = Visibility.Visible;
            AppEditorOverlay.BeginAnimation(OpacityProperty, null);
            AppEditorOverlay.Opacity = 0;

            var openFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            openFade.Completed += (_, _) =>
            {
                if (!_viewModel.IsAppEditorOpen)
                {
                    return;
                }

                AppEditorOverlay.BeginAnimation(OpacityProperty, null);
                AppEditorOverlay.Opacity = 1;
                AppEditorSlide.BeginAnimation(TranslateTransform.XProperty, null);
                AppEditorSlide.X = 0;
            };
            AppEditorOverlay.BeginAnimation(OpacityProperty, openFade);

            AppEditorSlide.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            return;
        }

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) =>
        {
            if (_viewModel.IsAppEditorOpen)
            {
                return;
            }

            AppEditorOverlay.BeginAnimation(OpacityProperty, null);
            AppEditorSlide.BeginAnimation(TranslateTransform.XProperty, null);
            AppEditorOverlay.Opacity = 0;
            AppEditorSlide.X = 28;
            AppEditorOverlay.Visibility = Visibility.Collapsed;
        };

        AppEditorOverlay.BeginAnimation(OpacityProperty, fade);
        AppEditorSlide.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(0, 28, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
    }

    private void AppEditorOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, sender))
        {
            _viewModel.CloseAppEditor();
        }
    }

    // ==================== 键盘 ====================

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_viewModel.IsJoinDialogOpen)
        {
            _viewModel.CloseJoinDialog();
            return;
        }

        if (_viewModel.IsAppEditorOpen)
        {
            _viewModel.CloseAppEditor();
            return;
        }

        if (_viewModel.IsMemberDrawerOpen)
        {
            _viewModel.CloseMemberDrawer();
            ClearMemberSelection();
        }
    }
}
