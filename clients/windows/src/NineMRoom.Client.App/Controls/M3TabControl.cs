using TabControl = System.Windows.Controls.TabControl;
using TabItem = System.Windows.Controls.TabItem;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Point = System.Windows.Point;

using System.Windows;

using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// M3 primary tabs 容器：页签头 + 共享的 3dp 活动指示器（带位移动画）。
/// 内容区由页面自行承载，TabItem 只提供页签头。
/// </summary>
public class M3TabControl : TabControl
{
    private StackPanel? _panel;
    private Border? _indicator;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _panel = GetTemplateChild("PART_TabPanel") as StackPanel;
        _indicator = GetTemplateChild("PART_Indicator") as Border;

        if (_panel is not null)
        {
            _panel.SizeChanged += (_, _) => UpdateIndicator(false);
        }

        ItemContainerGenerator.StatusChanged += (_, _) =>
        {
            if (ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                QueueUpdate(animate: false);
            }
        };

        Loaded += (_, _) => QueueUpdate(animate: false);
        QueueUpdate(animate: false);
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        QueueUpdate(animate: true);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        QueueUpdate(animate: false);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (Items.Count > 0 && Keyboard.FocusedElement is TabItem or null)
        {
            var next = e.Key switch
            {
                Key.Left => SelectedIndex - 1,
                Key.Right => SelectedIndex + 1,
                Key.Home => 0,
                Key.End => Items.Count - 1,
                _ => int.MinValue
            };

            if (next != int.MinValue)
            {
                SelectedIndex = Math.Clamp(next, 0, Items.Count - 1);
                (ItemContainerGenerator.ContainerFromIndex(SelectedIndex) as TabItem)?.Focus();
                e.Handled = true;
                return;
            }
        }

        base.OnPreviewKeyDown(e);
    }

    /// <summary>
    /// 布局完成前页签宽度为 0，指示器必须等一次布局之后再定位；
    /// 否则初始选中项没有下划线。
    /// </summary>
    private void QueueUpdate(bool animate) =>
        Dispatcher.BeginInvoke(new Action(() => UpdateIndicator(animate)), DispatcherPriority.Loaded);

    private void UpdateIndicator(bool animate)
    {
        if (_indicator is null || _panel is null)
        {
            return;
        }

        if (SelectedItem is not TabItem item || item.ActualWidth <= 0)
        {
            _indicator.Visibility = Visibility.Collapsed;
            return;
        }

        double offset;
        try
        {
            offset = item.TransformToAncestor(_panel).Transform(new Point(0, 0)).X;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        _indicator.Visibility = Visibility.Visible;

        if (_indicator.RenderTransform is not TranslateTransform translate || translate.IsFrozen)
        {
            // 模板里的 Freezable 会被冻结，必须换成可动画的实例。
            translate = new TranslateTransform();
            _indicator.RenderTransform = translate;
        }

        if (!animate)
        {
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            _indicator.BeginAnimation(WidthProperty, null);
            translate.X = offset;
            _indicator.Width = item.ActualWidth;
            return;
        }

        var duration = new Duration(TimeSpan.FromMilliseconds(200));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var startWidth = _indicator.ActualWidth > 0 ? _indicator.ActualWidth : item.ActualWidth;

        translate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(offset, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
        translate.X = offset;

        _indicator.BeginAnimation(
            WidthProperty,
            new DoubleAnimation(item.ActualWidth, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop,
                From = startWidth
            });
        _indicator.Width = item.ActualWidth;
    }
}
