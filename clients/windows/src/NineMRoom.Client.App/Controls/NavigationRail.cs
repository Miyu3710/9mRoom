using System.Windows;
using System.Windows.Automation;
using System.Windows.Media.Animation;
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
        }
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
