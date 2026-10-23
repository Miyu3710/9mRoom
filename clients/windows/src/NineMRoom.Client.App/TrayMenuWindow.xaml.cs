using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace NineMRoom.Client.App;

public partial class TrayMenuWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private bool _suppress;
    private DateTime _shownAt;

    public TrayMenuWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? string.Empty;
        Deactivated += (_, _) =>
        {
            if ((DateTime.UtcNow - _shownAt).TotalMilliseconds > 120)
            {
                Close();
            }
        };
    }

    public event Action? ShowRequested;

    public event Action? ExitRequested;

    public event Action<bool>? PauseToggled;

    public event Action<bool>? InvisibleToggled;

    public event Action<bool>? OverlayToggled;

    public event Action<bool>? ClickThroughToggled;

    public void SetState(bool paused, bool invisible, bool overlay, bool clickThrough)
    {
        _suppress = true;
        PauseToggle.IsChecked = paused;
        InvisibleToggle.IsChecked = invisible;
        OverlayToggle.IsChecked = overlay;
        ClickThroughToggle.IsChecked = clickThrough;
        _suppress = false;
    }

    public void SetStatus(string text)
    {
        text = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        const string prefix = "9mRoom · ";
        if (text.StartsWith(prefix, StringComparison.Ordinal))
        {
            text = text[prefix.Length..];
        }

        StatusTextBlock.Text = string.IsNullOrWhiteSpace(text) ? "正在运行" : text;
    }

    public void ShowAt(System.Drawing.Point point)
    {
        if (!IsVisible)
        {
            Opacity = 0;
            Show();
        }

        UpdateLayout();

        var dpi = VisualTreeHelper.GetDpi(this);
        var screen = Forms.Screen.FromPoint(point);
        var work = screen.WorkingArea;

        var workLeft = work.Left / dpi.DpiScaleX;
        var workTop = work.Top / dpi.DpiScaleY;
        var workRight = work.Right / dpi.DpiScaleX;
        var workBottom = work.Bottom / dpi.DpiScaleY;

        var x = point.X / dpi.DpiScaleX + 8;
        if (x + ActualWidth > workRight - 8)
        {
            x = point.X / dpi.DpiScaleX - ActualWidth - 8;
        }

        var y = point.Y / dpi.DpiScaleY + 8;
        if (y + ActualHeight > workBottom - 8)
        {
            y = point.Y / dpi.DpiScaleY - ActualHeight - 8;
        }

        Left = Math.Max(workLeft + 8, Math.Min(x, workRight - ActualWidth - 8));
        Top = Math.Max(workTop + 8, Math.Min(y, workBottom - ActualHeight - 8));

        _shownAt = DateTime.UtcNow;
        Activate();
        Focus();
        SetForegroundWindow(new WindowInteropHelper(this).Handle);

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, fade);

        var scale = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    private void ShowButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
        ShowRequested?.Invoke();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
        ExitRequested?.Invoke();
    }

    private void PauseToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_suppress)
        {
            PauseToggled?.Invoke(PauseToggle.IsChecked == true);
        }
    }

    private void InvisibleToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_suppress)
        {
            InvisibleToggled?.Invoke(InvisibleToggle.IsChecked == true);
        }
    }

    private void OverlayToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_suppress)
        {
            OverlayToggled?.Invoke(OverlayToggle.IsChecked == true);
        }
    }

    private void ClickThroughToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_suppress)
        {
            ClickThroughToggled?.Invoke(ClickThroughToggle.IsChecked == true);
        }
    }
}
