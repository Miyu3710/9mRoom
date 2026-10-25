using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using UserControl = System.Windows.Controls.UserControl;
using ToolTip = System.Windows.Controls.ToolTip;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// 设置项旁的问号说明：悬停显示提示，点击也能主动打开。
/// </summary>
public partial class InfoHint : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(InfoHint),
        new PropertyMetadata(string.Empty, OnTextChanged));

    private ToolTip? _toolTip;

    public InfoHint()
    {
        InitializeComponent();
        Loaded += (_, _) => EnsureToolTip();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InfoHint hint)
        {
            hint.UpdateToolTip();
        }
    }

    private void HintButton_Click(object sender, RoutedEventArgs e)
    {
        EnsureToolTip();
        if (_toolTip is null)
        {
            return;
        }

        _toolTip.IsOpen = false;
        _toolTip.IsOpen = true;
    }

    private void EnsureToolTip()
    {
        if (_toolTip is not null)
        {
            return;
        }

        _toolTip = new ToolTip
        {
            Placement = PlacementMode.Bottom,
            PlacementTarget = HintButton,
            StaysOpen = false,
            Content = new TextBlock
            {
                Text = Text,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 320
            }
        };

        HintButton.ToolTip = _toolTip;
    }

    private void UpdateToolTip()
    {
        if (_toolTip?.Content is TextBlock text)
        {
            text.Text = Text;
        }
    }
}
