using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
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
    private HwndSource? _hotkeySource;
    private bool _exiting;

    public MainWindow(AppSettings settings, bool startMinimized)
    {
        _settings = settings;
        InitializeComponent();

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

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ToggleConnectionAsync();
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton { Tag: string mode } && _viewModel is not null)
        {
            _viewModel.SetManualMode(mode);
        }
    }

    private void MemberRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MemberItem member })
        {
            _viewModel.OpenMemberDrawer(member);
        }
    }

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
        if (MaximizeButton is not null)
        {
            MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }
    }

    private void CloseDrawer_Click(object sender, RoutedEventArgs e) => _viewModel.CloseMemberDrawer();

    private async void PokeMember_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.PokeSelectedAsync();
    }

    private void MemberDrawerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, MemberDrawerOverlay))
        {
            _viewModel.CloseMemberDrawer();
        }
    }

    private void AnimateMemberDrawer(bool open)
    {
        var duration = TimeSpan.FromMilliseconds(180);
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

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsAppEditorOpen))
        {
            AnimateAppEditor();
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

    private async void LeaveRoom_Click(object sender, RoutedEventArgs e) => await _viewModel.LeaveRoomAsync();

    private void RoomName_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsInRoom || string.IsNullOrWhiteSpace(_viewModel.Room))
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(_viewModel.Room);
            _viewModel.ShowRoomCodeNotice("房间码复制成功");
        }
        catch
        {
            _viewModel.ShowRoomCodeNotice("复制失败，请检查剪贴板权限。");
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

    private void AppEditorOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, sender))
        {
            _viewModel.CloseAppEditor();
        }
    }

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

        _viewModel.CloseMemberDrawer();
    }

    private void TimerStart_Click(object sender, RoutedEventArgs e) => _viewModel.StartTimer();

    private void TimerPause_Click(object sender, RoutedEventArgs e) => _viewModel.PauseTimer();

    private void TimerReset_Click(object sender, RoutedEventArgs e) => _viewModel.ResetTimer();

    private void TimerMinutes_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string minutes } &&
            int.TryParse(minutes, out var value))
        {
            _viewModel.SetTimerMinutes(value);
        }
    }

    private void RoomNav_Checked(object sender, RoutedEventArgs e) => ShowPage(RoomPage);

    private void StateTab_Checked(object sender, RoutedEventArgs e)
    {
        ShowRoomTab(StateTabContent);
        MoveTabIndicator(StateTabButton, animate: true);
    }

    private void RoomTab_Checked(object sender, RoutedEventArgs e)
    {
        ShowRoomTab(RoomTabContent);
        MoveTabIndicator(RoomTabButton, animate: true);
    }

    private void LogTab_Checked(object sender, RoutedEventArgs e)
    {
        ShowRoomTab(LogTabContent);
        MoveTabIndicator(LogTabButton, animate: true);
    }

    private void ChatTab_Checked(object sender, RoutedEventArgs e)
    {
        ShowRoomTab(ChatTabContent);
        MoveTabIndicator(ChatTabButton, animate: true);
    }

    private void TabStrip_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var selected = StateTabButton.IsChecked == true ? StateTabButton
                : RoomTabButton.IsChecked == true ? RoomTabButton
                : LogTabButton.IsChecked == true ? LogTabButton
                : ChatTabButton;
            MoveTabIndicator(selected, animate: false);
        });
    }

    private void MoveTabIndicator(System.Windows.Controls.RadioButton? tab, bool animate)
    {
        if (tab is null || !tab.IsLoaded || TabStrip is null || TabIndicator is null)
        {
            return;
        }

        var position = tab.TransformToAncestor(TabStrip).Transform(new System.Windows.Point(0, 0));
        if (tab.ActualWidth <= 0)
        {
            return;
        }

        if (TabIndicator.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            TabIndicator.RenderTransform = translate;
        }

        if (!animate)
        {
            translate.X = position.X;
            TabIndicator.Width = tab.ActualWidth;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(220);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        translate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(position.X, duration) { EasingFunction = easing });
        TabIndicator.BeginAnimation(
            WidthProperty,
            new DoubleAnimation(tab.ActualWidth, duration) { EasingFunction = easing });
    }

    private void ShowRoomTab(UIElement tab)
    {
        if (StateTabContent is null || RoomTabContent is null ||
            LogTabContent is null || ChatTabContent is null)
        {
            return;
        }

        StateTabContent.Visibility = ReferenceEquals(tab, StateTabContent) ? Visibility.Visible : Visibility.Collapsed;
        RoomTabContent.Visibility = ReferenceEquals(tab, RoomTabContent) ? Visibility.Visible : Visibility.Collapsed;
        LogTabContent.Visibility = ReferenceEquals(tab, LogTabContent) ? Visibility.Visible : Visibility.Collapsed;
        ChatTabContent.Visibility = ReferenceEquals(tab, ChatTabContent) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FocusNav_Checked(object sender, RoutedEventArgs e) => ShowPage(FocusPage);

    private void TodoNav_Checked(object sender, RoutedEventArgs e) => ShowPage(TodoPage);

    private void LayoutNav_Checked(object sender, RoutedEventArgs e) => ShowPage(LayoutPage);

    private void AppNav_Checked(object sender, RoutedEventArgs e) => ShowPage(AppPage);

    private void SettingsNav_Checked(object sender, RoutedEventArgs e) => ShowPage(SettingsPage);

    private void ShowPage(UIElement page)
    {
        if (RoomPage is null || FocusPage is null || TodoPage is null ||
            LayoutPage is null || AppPage is null || SettingsPage is null)
        {
            return;
        }

        RoomPage.Visibility = ReferenceEquals(page, RoomPage) ? Visibility.Visible : Visibility.Collapsed;
        FocusPage.Visibility = ReferenceEquals(page, FocusPage) ? Visibility.Visible : Visibility.Collapsed;
        TodoPage.Visibility = ReferenceEquals(page, TodoPage) ? Visibility.Visible : Visibility.Collapsed;
        LayoutPage.Visibility = ReferenceEquals(page, LayoutPage) ? Visibility.Visible : Visibility.Collapsed;
        AppPage.Visibility = ReferenceEquals(page, AppPage) ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = ReferenceEquals(page, SettingsPage) ? Visibility.Visible : Visibility.Collapsed;
    }
}
