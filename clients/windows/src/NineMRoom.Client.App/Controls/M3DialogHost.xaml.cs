using UserControl = System.Windows.Controls.UserControl;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Brush = System.Windows.Media.Brush;

using System.Windows;

using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// M3 基本对话框宿主：用于需要用户明确回应的确认（破坏性操作、需要解释的动作）。
/// 返回 Task&lt;bool&gt;，Esc = 取消，Enter = 确认。
/// </summary>
public partial class M3DialogHost : UserControl
{
    private TaskCompletionSource<bool>? _pending;
    private IInputElement? _previousFocus;

    public M3DialogHost()
    {
        InitializeComponent();
    }

    public Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmText = "确定",
        string cancelText = "取消",
        bool destructive = false)
    {
        if (_pending is not null)
        {
            return Task.FromResult(false);
        }

        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Content = cancelText;
        ConfirmButton.Foreground = destructive
            ? (Brush)FindResource("BrushError")
            : (Brush)FindResource("BrushPrimary");

        _previousFocus = Keyboard.FocusedElement;
        Visibility = Visibility.Visible;
        var scrimOpacity = (double)FindResource("StateScrim");

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(160);
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        Scrim.BeginAnimation(OpacityProperty, new DoubleAnimation(0, scrimOpacity, duration) { EasingFunction = ease });
        DialogScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });
        DialogScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.96, 1, duration) { EasingFunction = ease });

        FocusManager.SetFocusedElement(this, ConfirmButton);
        ConfirmButton.Focus();

        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _pending.Task;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => Complete(true);

    private void Cancel_Click(object sender, RoutedEventArgs e) => Complete(false);

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Complete(false);
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Complete(true);
        }
    }

    private void Complete(bool result)
    {
        var pending = _pending;
        if (pending is null)
        {
            return;
        }

        _pending = null;

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
        fade.Completed += (_, _) =>
        {
            Visibility = Visibility.Collapsed;
            Root.BeginAnimation(OpacityProperty, null);
            Scrim.BeginAnimation(OpacityProperty, null);
            if (_previousFocus is not null)
            {
                _previousFocus.Focus();
                _previousFocus = null;
            }
        };

        Root.BeginAnimation(OpacityProperty, fade);
        Scrim.BeginAnimation(OpacityProperty, fade);

        pending.TrySetResult(result);
    }
}
