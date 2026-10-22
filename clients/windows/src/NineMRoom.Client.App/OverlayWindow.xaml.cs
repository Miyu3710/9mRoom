using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Media = System.Windows.Media;

namespace NineMRoom.Client.App;

public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly AppSettings _settings;
    private bool _clickThrough;
    private bool _userMoved;
    private string _position = "bottomRight";

    public OverlayWindow(MainViewModel viewModel, AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        DataContext = viewModel;

        _clickThrough = settings.OverlayClickThrough;
        WindowStartupLocation = WindowStartupLocation.Manual;

        if (settings.OverlayLeft is double left && settings.OverlayTop is double top)
        {
            Left = left;
            Top = top;
        }
        else
        {
            var area = MonitorWorkArea.Get(this);
            Left = Math.Max(area.Left + 16, area.Right - 400);
            Top = Math.Max(area.Top + 16, area.Bottom - 140);
        }

        SourceInitialized += (_, _) => ApplyExtendedStyles();
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        Loaded += (_, _) => ClampToWorkArea();
        SizeChanged += (_, _) => ClampToWorkArea();

        ApplyTheme(
            viewModel.GetEffectiveTheme(),
            reposition: settings.OverlayLeft is null || settings.OverlayTop is null);
    }

    public void ApplyTheme(OverlayTheme theme, bool reposition = true)
    {
        Resources["OverlayTextBrush"] = new Media.SolidColorBrush(ParseColor(theme.Text, Media.Colors.White));
        Resources["OverlayMutedBrush"] = new Media.SolidColorBrush(ParseColor(theme.Muted, Media.Colors.LightGray));
        Resources["OverlayFontSize"] = theme.FontSize;
        Resources["OverlayNameWeight"] = ParseWeight(theme.FontWeight);
        Resources["OverlayFontFamily"] = new Media.FontFamily(
            string.IsNullOrWhiteSpace(theme.FontFamily) ? "Segoe UI" : theme.FontFamily);
        Resources["OverlayOneLineWrap"] = theme.WrapInOneLine
            ? System.Windows.TextWrapping.Wrap
            : System.Windows.TextWrapping.NoWrap;

        RootBorder.Background = new Media.SolidColorBrush(ParseColor(theme.Background, Media.Colors.Transparent));
        RootBorder.BorderBrush = new Media.SolidColorBrush(ParseColor(theme.Border, Media.Colors.Transparent));
        RootBorder.BorderThickness = new Thickness(theme.BorderThickness);
        RootBorder.CornerRadius = new CornerRadius(theme.CornerRadius);
        RootBorder.Padding = new Thickness(theme.Padding);

        HeaderText.Visibility = theme.ShowHeader ? Visibility.Visible : Visibility.Collapsed;
        MembersList.ItemTemplate = (DataTemplate)FindResource(
            theme.Layout == "oneLine" ? "OneLineTemplate" : "TwoLineTemplate");
        Width = theme.Width;

        _position = theme.Position == "bottomCenter" ? "bottomCenter" : "bottomRight";

        if (!reposition)
        {
            return;
        }

        _userMoved = false;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            PositionByTheme();
            SavePosition();
        }), DispatcherPriority.Loaded);
    }

    private void PositionByTheme()
    {
        var area = MonitorWorkArea.Get(this);
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : 120;

        if (_position == "bottomCenter")
        {
            Left = area.Left + (area.Width - width) / 2;
            Top = area.Bottom - height - 24;
        }
        else
        {
            Left = area.Right - width - 12;
            Top = area.Bottom - height - 12;
        }
    }

    private void SavePosition()
    {
        _settings.OverlayLeft = Left;
        _settings.OverlayTop = Top;
        SettingsStore.Save(_settings);
    }

    public void ResetPosition()
    {
        _userMoved = false;
        PositionByTheme();
        SavePosition();
    }

    private void ClampToWorkArea()
    {
        if (_userMoved || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var area = MonitorWorkArea.Get(this);
        const double margin = 12;

        if (Left + ActualWidth > area.Right - margin)
        {
            Left = area.Right - ActualWidth - margin;
        }

        if (Top + ActualHeight > area.Bottom - margin)
        {
            Top = area.Bottom - ActualHeight - margin;
        }

        if (Left < area.Left + margin)
        {
            Left = area.Left + margin;
        }

        if (Top < area.Top + margin)
        {
            Top = area.Top + margin;
        }
    }

    public bool ClickThrough
    {
        get => _clickThrough;
        set
        {
            if (_clickThrough == value)
            {
                return;
            }

            _clickThrough = value;
            ApplyExtendedStyles();
        }
    }

    private void ApplyExtendedStyles()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlExStyle);
        style |= WsExToolWindow | WsExNoActivate;

        if (_clickThrough)
        {
            style |= WsExTransparent;
        }
        else
        {
            style &= ~WsExTransparent;
        }

        SetWindowLong(handle, GwlExStyle, style);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_clickThrough)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // 窗口句柄异常时忽略拖动
        }

        _settings.OverlayLeft = Left;
        _settings.OverlayTop = Top;
        _userMoved = true;
        SettingsStore.Save(_settings);
    }

    private static Media.Color ParseColor(string value, Media.Color fallback)
    {
        try
        {
            return (Media.Color)Media.ColorConverter.ConvertFromString(value);
        }
        catch
        {
            return fallback;
        }
    }

    private static FontWeight ParseWeight(string value) => value switch
    {
        "Normal" => FontWeights.Normal,
        "Medium" => FontWeights.Medium,
        "Bold" => FontWeights.Bold,
        _ => FontWeights.SemiBold
    };

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int newStyle);
}
