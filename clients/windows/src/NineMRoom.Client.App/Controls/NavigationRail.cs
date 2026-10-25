using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using MaterialDesignThemes.Wpf;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// M3 导航栏（expanded / collapsed）：展开时图标 + 文字，收起时仅图标、悬停显示名称。
/// 顶部的切换按钮在两种状态间切换，宽度与条目模板由样式随 IsCollapsed 切换。
/// </summary>
public class NavigationRail : ListBox
{
    public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(
        nameof(IsCollapsed),
        typeof(bool),
        typeof(NavigationRail),
        new PropertyMetadata(false, OnIsCollapsedChanged));

    private Button? _toggleButton;
    private PackIcon? _toggleIcon;
    private PackIcon? _pressedIcon;
    private ListBoxItem? _pressContainer;
    private Border? _pressPill;
    private Border? _pressLayer;
    private Canvas? _pressRippleHost;
    private bool _pressCanceled;

    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public override void OnApplyTemplate()
    {
        if (_toggleButton is not null)
        {
            _toggleButton.Click -= Toggle_Click;
        }

        base.OnApplyTemplate();

        _toggleButton = GetTemplateChild("PART_ToggleButton") as Button;
        _toggleIcon = GetTemplateChild("PART_ToggleIcon") as PackIcon;

        if (_toggleButton is not null)
        {
            _toggleButton.Click += Toggle_Click;
        }

        UpdateToggleVisuals();
    }

    private static void OnIsCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationRail rail)
        {
            rail.UpdateToggleVisuals();
            rail.AnimateWidth();
            rail.AnimateItems(e.NewValue is true);
        }
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        if (e.ChangedButton != MouseButton.Left ||
            e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(this, source) is not ListBoxItem container)
        {
            return;
        }

        var pill = FindNamed<Border>(container, "PillBg");
        if (pill is null)
        {
            return;
        }

        _pressContainer = container;
        _pressPill = pill;
        _pressLayer = FindNamed<Border>(container, "PressBg");
        _pressRippleHost = FindNamed<Canvas>(container, "RippleHost");
        _pressedIcon = FindNamed<PackIcon>(container, "NavIcon");
        _pressCanceled = false;

        // 点击波纹仍画在原来的 RippleHost 上。
        if (_pressRippleHost is { ActualWidth: > 0, ActualHeight: > 0 })
        {
            Ripple.Play(_pressRippleHost, e.GetPosition(_pressRippleHost), container.Foreground);
        }

        FadeLayer(_pressLayer, 0.12, 80);
        PressIcon(0.82, 80, new CubicEase { EasingMode = EasingMode.EaseOut });

        CaptureMouse();
        // 延迟到松开且未移出胶囊时才真正选中，避免按下即切页。
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_pressPill is null || _pressCanceled)
        {
            return;
        }

        if (!IsInside(_pressPill, e.GetPosition(_pressPill)))
        {
            CancelPress();
        }
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);

        if (_pressPill is null)
        {
            return;
        }

        var container = _pressContainer;
        var inside = !_pressCanceled && IsInside(_pressPill, e.GetPosition(_pressPill));

        FadeLayer(_pressLayer, 0, 140);
        ReleasePressedIcon();
        _pressContainer = null;
        _pressPill = null;
        _pressLayer = null;
        _pressRippleHost = null;
        _pressCanceled = false;

        if (Mouse.Captured == this)
        {
            ReleaseMouseCapture();
        }

        if (inside && container?.DataContext is { } item)
        {
            SelectedItem = item;
            container.Focus();
        }

        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        CancelPress();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        CancelPress();
    }

    private void CancelPress()
    {
        if (_pressPill is null)
        {
            return;
        }

        _pressCanceled = true;
        FadeLayer(_pressLayer, 0, 120);
        ReleasePressedIcon();
    }

    private void PressIcon(double to, int milliseconds, IEasingFunction easing)
    {
        if (_pressedIcon?.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        var animation = new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.HoldEnd
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static void FadeLayer(Border? layer, double to, int milliseconds)
    {
        if (layer is null)
        {
            return;
        }

        layer.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(to, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    private static bool IsInside(FrameworkElement element, Point point) =>
        point.X >= 0 && point.Y >= 0 &&
        point.X <= element.ActualWidth && point.Y <= element.ActualHeight;

    private void ReleasePressedIcon()
    {
        if (_pressedIcon?.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        // 松开后带一点回弹，形成按下再弹起的点击反馈。
        var release = new DoubleAnimation(1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, release);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, release);
        _pressedIcon = null;
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        IsCollapsed = !IsCollapsed;
    }

    private void AnimateWidth()
    {
        var target = IsCollapsed
            ? ResolveWidth("SizeNavRailCollapsed", 80)
            : ResolveWidth("SizeNavRailExpanded", 240);

        BeginAnimation(
            WidthProperty,
            new DoubleAnimation(target, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            });
    }

    private void AnimateItems(bool collapsed)
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(220);

        for (var index = 0; index < Items.Count; index++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container)
            {
                continue;
            }

            var pill = FindNamed<Border>(container, "PillBg");
            var label = FindNamed<TextBlock>(container, "NavLabel");

            if (pill is not null)
            {
                // 与侧边栏宽度动画同时开始、同时结束，避免侧栏先收回、背景再收缩。
                pill.BeginAnimation(
                    WidthProperty,
                    new DoubleAnimation
                    {
                        From = collapsed ? 96 : 48,
                        To = collapsed ? 48 : 96,
                        Duration = duration,
                        EasingFunction = ease,
                        FillBehavior = FillBehavior.HoldEnd
                    });
            }

            if (label is not null)
            {
                if (collapsed)
                {
                    label.BeginAnimation(
                        OpacityProperty,
                        new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = TimeSpan.FromMilliseconds(100),
                            FillBehavior = FillBehavior.HoldEnd
                        });
                }
                else
                {
                    var animation = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.HoldEnd };
                    animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
                    animation.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(duration)) { EasingFunction = ease });
                    label.BeginAnimation(OpacityProperty, animation);
                }
            }
        }
    }

    private static T? FindNamed<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            if (FindNamed<T>(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private double ResolveWidth(string key, double fallback) =>
        TryFindResource(key) is double value ? value : fallback;

    private void UpdateToggleVisuals()
    {
        var label = IsCollapsed ? "展开导航" : "收起导航";

        if (_toggleIcon is not null)
        {
            _toggleIcon.Kind = IsCollapsed ? PackIconKind.Menu : PackIconKind.MenuOpen;
        }

        if (_toggleButton is not null)
        {
            _toggleButton.ToolTip = label;
            AutomationProperties.SetName(_toggleButton, label);
        }
    }
}
