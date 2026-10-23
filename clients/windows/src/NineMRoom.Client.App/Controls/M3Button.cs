using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Button = System.Windows.Controls.Button;
using Point = System.Windows.Point;
using Canvas = System.Windows.Controls.Canvas;
using Ellipse = System.Windows.Shapes.Ellipse;
using Brushes = System.Windows.Media.Brushes;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// M3 按钮基类：loading（busy）状态、统一圆角，以及点击时的波纹反馈。
/// </summary>
public class M3Button : Button
{
    public static readonly DependencyProperty IsBusyProperty = DependencyProperty.Register(
        nameof(IsBusy),
        typeof(bool),
        typeof(M3Button),
        new PropertyMetadata(false));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(M3Button),
        new PropertyMetadata(new CornerRadius(8)));

    private Canvas? _rippleHost;

    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _rippleHost = GetTemplateChild("RippleHost") as Canvas;
        UpdateRippleClip();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateRippleClip();
    }

    protected override void OnClick()
    {
        // busy 时屏蔽重复提交，但保留焦点与可读状态。
        if (IsBusy)
        {
            return;
        }

        base.OnClick();
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        PlayRipple(e.GetPosition(this));
    }

    private void UpdateRippleClip()
    {
        if (_rippleHost is null)
        {
            return;
        }

        var width = _rippleHost.ActualWidth;
        var height = _rippleHost.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var radius = Math.Max(
            Math.Max(CornerRadius.TopLeft, CornerRadius.TopRight),
            Math.Max(CornerRadius.BottomLeft, CornerRadius.BottomRight));

        _rippleHost.Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius);
    }

    private void PlayRipple(Point point)
    {
        if (_rippleHost is null || !IsEnabled)
        {
            return;
        }

        var width = _rippleHost.ActualWidth;
        var height = _rippleHost.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var origin = TranslatePoint(point, _rippleHost);
        var radius = Math.Max(
            Math.Max(origin.X, width - origin.X),
            Math.Max(origin.Y, height - origin.Y)) + 4;

        var ripple = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = Foreground ?? Brushes.White,
            Opacity = 0.18,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.2, 0.2)
        };

        Canvas.SetLeft(ripple, origin.X - radius);
        Canvas.SetTop(ripple, origin.Y - radius);
        _rippleHost.Children.Add(ripple);

        var duration = TimeSpan.FromMilliseconds(420);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var transform = (ScaleTransform)ripple.RenderTransform;

        transform.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, duration) { EasingFunction = ease });
        transform.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, duration) { EasingFunction = ease });

        var fade = new DoubleAnimation(0, duration)
        {
            BeginTime = TimeSpan.FromMilliseconds(140)
        };
        fade.Completed += (_, _) => _rippleHost.Children.Remove(ripple);
        ripple.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
