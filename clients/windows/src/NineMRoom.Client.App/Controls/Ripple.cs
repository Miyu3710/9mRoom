using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Control = System.Windows.Controls.Control;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Ellipse = System.Windows.Shapes.Ellipse;
using Canvas = System.Windows.Controls.Canvas;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// 通用波纹反馈：控件模板里放一个名为 RippleHost 的 Canvas，
/// 点击时从落点扩散一个圆形波纹。用于菜单项、标签、分段选择等非 M3Button 控件。
/// </summary>
public static class Ripple
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(Ripple),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly ConditionalWeakTable<Control, Hook> Hooks = new();

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Control control && e.NewValue is true)
        {
            Hooks.GetValue(control, value => new Hook(value));
        }
    }

    private sealed class Hook
    {
        private readonly Control _control;

        public Hook(Control control)
        {
            _control = control;
            _control.PreviewMouseLeftButtonDown += OnMouseDown;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_control.IsEnabled)
            {
                return;
            }

            var host = _control.Template?.FindName("RippleHost", _control) as Canvas
                       ?? FindDescendant(_control, "RippleHost") as Canvas;
            if (host is null)
            {
                return;
            }

            Play(host, e.GetPosition(host), _control.Foreground);
        }

        private static FrameworkElement? FindDescendant(DependencyObject parent, string name)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement element && element.Name == name)
                {
                    return element;
                }

                var found = FindDescendant(child, name);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }
    }

    public static void Play(Canvas host, Point origin, Brush? brush)
    {
        var width = host.ActualWidth;
        var height = host.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var radius = Math.Max(
            Math.Max(origin.X, width - origin.X),
            Math.Max(origin.Y, height - origin.Y)) + 4;

        var ripple = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = brush ?? Brushes.White,
            Opacity = 0.16,
            IsHitTestVisible = false,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.2, 0.2)
        };

        Canvas.SetLeft(ripple, origin.X - radius);
        Canvas.SetTop(ripple, origin.Y - radius);
        host.Children.Add(ripple);

        var duration = TimeSpan.FromMilliseconds(360);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var transform = (ScaleTransform)ripple.RenderTransform;

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, duration) { EasingFunction = ease });
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, duration) { EasingFunction = ease });

        var fade = new DoubleAnimation(0, duration) { BeginTime = TimeSpan.FromMilliseconds(100) };
        fade.Completed += (_, _) => host.Children.Remove(ripple);
        ripple.BeginAnimation(UIElement.OpacityProperty, fade);
    }
}
