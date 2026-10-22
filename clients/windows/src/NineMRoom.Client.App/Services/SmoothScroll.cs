using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace NineMRoom.Client.App;

public static class SmoothScroll
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(SmoothScroll),
        new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty OffsetProperty = DependencyProperty.RegisterAttached(
        "Offset",
        typeof(double),
        typeof(SmoothScroll),
        new PropertyMetadata(0d, OnOffsetChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            viewer.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        else
        {
            viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer || viewer.ScrollableHeight <= 0 || e.Handled)
        {
            return;
        }

        const double pixelsPerNotch = 200;
        var target = Math.Clamp(
            viewer.VerticalOffset - e.Delta / 120.0 * pixelsPerNotch,
            0,
            viewer.ScrollableHeight);

        viewer.BeginAnimation(OffsetProperty, null);
        viewer.SetValue(OffsetProperty, viewer.VerticalOffset);

        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        viewer.BeginAnimation(OffsetProperty, animation);
        e.Handled = true;
    }

    private static void OnOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer viewer)
        {
            viewer.ScrollToVerticalOffset((double)e.NewValue);
        }
    }
}
