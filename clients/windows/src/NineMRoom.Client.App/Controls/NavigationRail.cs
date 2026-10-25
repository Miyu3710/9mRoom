using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
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

        _pressedIcon = FindIcon(e.OriginalSource as DependencyObject);
        if (_pressedIcon?.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        var press = new DoubleAnimation(0.82, TimeSpan.FromMilliseconds(80))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, press);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, press);
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        ReleasePressedIcon();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        ReleasePressedIcon();
    }

    private PackIcon? FindIcon(DependencyObject? source)
    {
        if (source is null)
        {
            return null;
        }

        return ItemsControl.ContainerFromElement(this, source) is ListBoxItem container
            ? FindNamed<PackIcon>(container, "NavIcon")
            : null;
    }

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
