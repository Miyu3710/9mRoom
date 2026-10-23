using RadioButton = System.Windows.Controls.RadioButton;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Panel = System.Windows.Controls.Panel;

using System.Windows;

using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NineMRoom.Client.App.Controls;

public enum M3SegmentPosition
{
    Single = 0,
    First = 1,
    Middle = 2,
    Last = 3
}

/// <summary>
/// M3 分段按钮的一段（单选语义）。同一父容器内的分段共用一个 GroupName，
/// 支持 ←/→、Home/End 在段间移动并选中。
/// </summary>
public class M3SegmentedButton : RadioButton
{
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position),
        typeof(M3SegmentPosition),
        typeof(M3SegmentedButton),
        new PropertyMetadata(M3SegmentPosition.Single));

    public static readonly DependencyProperty ShowCheckIconProperty = DependencyProperty.Register(
        nameof(ShowCheckIcon),
        typeof(bool),
        typeof(M3SegmentedButton),
        new PropertyMetadata(true));

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(M3SegmentedButton),
        new PropertyMetadata(new CornerRadius(20)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public M3SegmentPosition Position
    {
        get => (M3SegmentPosition)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public bool ShowCheckIcon
    {
        get => (bool)GetValue(ShowCheckIconProperty);
        set => SetValue(ShowCheckIconProperty, value);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Up)
        {
            JumpTo(SiblingIndex() - 1);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Right or Key.Down)
        {
            JumpTo(SiblingIndex() + 1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Home)
        {
            JumpTo(0);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.End)
        {
            JumpTo(int.MaxValue);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void JumpTo(int index)
    {
        var siblings = Siblings();
        if (siblings.Count == 0)
        {
            return;
        }

        var next = siblings[Math.Clamp(index, 0, siblings.Count - 1)];
        if (ReferenceEquals(next, this))
        {
            return;
        }

        next.IsChecked = true;
        next.Focus();
    }

    private int SiblingIndex()
    {
        var siblings = Siblings();
        for (var i = 0; i < siblings.Count; i++)
        {
            if (ReferenceEquals(siblings[i], this))
            {
                return i;
            }
        }

        return 0;
    }

    private List<M3SegmentedButton> Siblings()
    {
        DependencyObject? current = this;
        while (current is not null && current is not Panel)
        {
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }

        return current is Panel panel
            ? panel.Children.OfType<M3SegmentedButton>().ToList()
            : new List<M3SegmentedButton>();
    }
}
