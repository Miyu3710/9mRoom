using UserControl = System.Windows.Controls.UserControl;

using System.Windows;

using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// M3 Snackbar：只用于真实业务事件的短暂反馈，不承载持续状态。
/// 同一时间最多一条；新消息替换旧消息。
/// </summary>
public partial class M3Snackbar : UserControl
{
    private readonly DispatcherTimer _timer = new();
    private Action? _pendingAction;

    public M3Snackbar()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Hide();
    }

    public void Show(string message, string? actionText = null, Action? action = null, double seconds = 4)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => Show(message, actionText, action, seconds));
            return;
        }

        MessageText.Text = message;
        _pendingAction = action;

        if (!string.IsNullOrEmpty(actionText) && action is not null)
        {
            ActionButton.Content = actionText;
            ActionButton.Visibility = Visibility.Visible;
        }
        else
        {
            ActionButton.Visibility = Visibility.Collapsed;
            _pendingAction = null;
        }

        _timer.Stop();
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(2, seconds));

        Visibility = Visibility.Visible;
        IsHitTestVisible = true;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(180);
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        Slide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(12, 0, duration) { EasingFunction = ease });

        _timer.Start();
    }

    public void Hide()
    {
        _timer.Stop();

        if (Visibility != Visibility.Visible)
        {
            return;
        }

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) =>
        {
            if (!_timer.IsEnabled)
            {
                Visibility = Visibility.Collapsed;
                IsHitTestVisible = false;
            }
        };

        Root.BeginAnimation(OpacityProperty, fade);
        Slide.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(0, 8, TimeSpan.FromMilliseconds(140)));
    }

    private void Action_Click(object sender, RoutedEventArgs e)
    {
        var action = _pendingAction;
        _pendingAction = null;
        Hide();
        action?.Invoke();
    }
}
