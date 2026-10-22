using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;

namespace NineMRoom.Client.App;

public partial class BorderDialog : Window
{
    private readonly string _fontFamily;
    private readonly double _fontSize;
    private readonly string _fontWeight;
    private bool _loading = true;
    private bool _syncingOpacity;

    public BorderDialog(
        string background,
        string text,
        string muted,
        string border,
        double radius,
        double padding,
        double width,
        string layout,
        string position,
        bool showHeader,
        bool wrapInOneLine,
        string fontFamily,
        double fontSize,
        string fontWeight)
    {
        InitializeComponent();

        _fontFamily = fontFamily;
        _fontSize = fontSize;
        _fontWeight = fontWeight;

        BackgroundPicker.Hex = background;
        TextColorPicker.Hex = text;
        MutedColorPicker.Hex = muted;
        BorderColorPicker.Hex = border;
        OpacitySlider.Value = AlphaPercent(background);
        OpacityLabel.Text = ((int)OpacitySlider.Value) + "%";
        RadiusBox.Text = radius.ToString("0.#", CultureInfo.InvariantCulture);
        PaddingBox.Text = padding.ToString("0.#", CultureInfo.InvariantCulture);
        WidthBox.Text = width.ToString("0.#", CultureInfo.InvariantCulture);
        LayoutOne.IsChecked = layout == "oneLine";
        LayoutTwo.IsChecked = layout != "oneLine";
        PositionCenter.IsChecked = position == "bottomCenter";
        PositionRight.IsChecked = position != "bottomCenter";
        ShowHeaderCheck.IsChecked = showHeader;
        WrapOneLineCheck.IsChecked = wrapInOneLine;

        BackgroundPicker.HexChanged += BackgroundChanged;
        TextColorPicker.HexChanged += UpdatePreview;
        MutedColorPicker.HexChanged += UpdatePreview;
        BorderColorPicker.HexChanged += UpdatePreview;
        OpacitySlider.ValueChanged += OpacityChanged;
        RadiusBox.TextChanged += (_, _) => UpdatePreview();
        PaddingBox.TextChanged += (_, _) => UpdatePreview();
        WidthBox.TextChanged += (_, _) => UpdatePreview();
        LayoutOne.Checked += (_, _) => UpdatePreview();
        LayoutTwo.Checked += (_, _) => UpdatePreview();
        PositionRight.Checked += (_, _) => UpdatePreview();
        PositionCenter.Checked += (_, _) => UpdatePreview();
        ShowHeaderCheck.Checked += (_, _) => UpdatePreview();
        ShowHeaderCheck.Unchecked += (_, _) => UpdatePreview();
        WrapOneLineCheck.Checked += (_, _) => UpdatePreview();
        WrapOneLineCheck.Unchecked += (_, _) => UpdatePreview();

        _loading = false;
        UpdatePreview();
    }

    private void BackgroundChanged()
    {
        if (_loading)
        {
            return;
        }

        _syncingOpacity = true;
        OpacitySlider.Value = AlphaPercent(BackgroundPicker.Hex);
        OpacityLabel.Text = ((int)OpacitySlider.Value) + "%";
        _syncingOpacity = false;
        UpdatePreview();
    }

    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || _syncingOpacity)
        {
            return;
        }

        BackgroundPicker.Hex = WithAlpha(BackgroundPicker.Hex, e.NewValue);
        OpacityLabel.Text = ((int)e.NewValue) + "%";
        UpdatePreview();
    }

    private static double AlphaPercent(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return color.A * 100.0 / 255.0;
        }
        catch
        {
            return 85;
        }
    }

    private static string WithAlpha(string hex, double percent)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var alpha = (byte)Math.Round(Math.Clamp(percent, 0, 100) * 255 / 100);
            return $"#{alpha:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }
        catch
        {
            return hex;
        }
    }

    public string ResultBackground { get; private set; } = "#D9232327";

    public string ResultText { get; private set; } = "#ECECF0";

    public string ResultMuted { get; private set; } = "#9B9BA4";

    public string ResultBorder { get; private set; } = "#8034343A";

    public double ResultRadius { get; private set; } = 10;

    public double ResultPadding { get; private set; } = 12;

    public double ResultWidth { get; private set; } = 420;

    public string ResultLayout { get; private set; } = "twoLine";

    public string ResultPosition { get; private set; } = "bottomRight";

    public bool ResultShowHeader { get; private set; } = true;

    public bool ResultWrapInOneLine { get; private set; }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    public OverlayTheme ResultTheme => BuildTheme();

    public event Action? PreviewChanged;

    public OverlayTheme BuildTheme() => new()
    {
        Name = "自定义",
        Background = BackgroundPicker.Hex.Trim(),
        Text = TextColorPicker.Hex.Trim(),
        Muted = MutedColorPicker.Hex.Trim(),
        Border = BorderColorPicker.Hex.Trim(),
        CornerRadius = Parse(RadiusBox.Text, 10, 0, 30),
        Padding = Parse(PaddingBox.Text, 12, 0, 32),
        Width = Parse(WidthBox.Text, 420, 280, 1000),
        Layout = LayoutOne.IsChecked == true ? "oneLine" : "twoLine",
        Position = PositionCenter.IsChecked == true ? "bottomCenter" : "bottomRight",
        ShowHeader = ShowHeaderCheck.IsChecked == true,
        WrapInOneLine = WrapOneLineCheck.IsChecked == true,
        BorderThickness = 1,
        FontFamily = _fontFamily,
        FontSize = _fontSize,
        FontWeight = _fontWeight
    };

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var theme = BuildTheme();
        ResultBackground = theme.Background;
        ResultText = theme.Text;
        ResultMuted = theme.Muted;
        ResultBorder = theme.Border;
        ResultRadius = theme.CornerRadius;
        ResultPadding = theme.Padding;
        ResultWidth = theme.Width;
        ResultLayout = theme.Layout;
        ResultPosition = theme.Position;
        ResultShowHeader = theme.ShowHeader;
        ResultWrapInOneLine = theme.WrapInOneLine;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void UpdatePreview()
    {
        if (PreviewBorder is null)
        {
            return;
        }

        var family = new FontFamily(string.IsNullOrWhiteSpace(_fontFamily) ? "Segoe UI" : _fontFamily);
        var weight = _fontWeight switch
        {
            "Normal" => FontWeights.Normal,
            "Medium" => FontWeights.Medium,
            _ => FontWeights.SemiBold
        };

        PreviewBorder.Width = Math.Clamp(Parse(WidthBox.Text, 420, 280, 1000), 260, 340);
        PreviewBorder.Background = ParseBrush(BackgroundPicker.Hex, Colors.Transparent);
        PreviewBorder.BorderBrush = ParseBrush(BorderColorPicker.Hex, Colors.Transparent);
        PreviewBorder.CornerRadius = new CornerRadius(Parse(RadiusBox.Text, 10, 0, 30));
        PreviewBorder.Padding = new Thickness(Parse(PaddingBox.Text, 12, 0, 32));

        PreviewTitle.FontFamily = family;
        PreviewTitle.Foreground = ParseBrush(MutedColorPicker.Hex, Color.FromRgb(0x9B, 0x9B, 0xA4));
        PreviewTitle.Visibility = ShowHeaderCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PreviewTitle.FontSize = Math.Max(9, _fontSize - 2);

        var wrap = LayoutTwo.IsChecked == true || WrapOneLineCheck.IsChecked == true;
        var twoLine = LayoutTwo.IsChecked == true;

        PreviewLines.Orientation = twoLine
            ? System.Windows.Controls.Orientation.Vertical
            : System.Windows.Controls.Orientation.Horizontal;
        PreviewStatus.Margin = twoLine
            ? new Thickness(0, 2, 0, 0)
            : new Thickness(8, 0, 0, 0);

        PreviewName.FontFamily = family;
        PreviewName.FontSize = _fontSize;
        PreviewName.FontWeight = weight;
        PreviewName.Foreground = ParseBrush(TextColorPicker.Hex, Color.FromRgb(0xEC, 0xEC, 0xF0));

        PreviewStatus.FontFamily = family;
        PreviewStatus.FontSize = _fontSize;
        PreviewStatus.FontWeight = FontWeights.Normal;
        PreviewStatus.Foreground = ParseBrush(MutedColorPicker.Hex, Color.FromRgb(0x9B, 0x9B, 0xA4));
        PreviewStatus.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        PreviewStatus.TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis;

        PreviewBorder.HorizontalAlignment = PositionCenter.IsChecked == true
            ? System.Windows.HorizontalAlignment.Center
            : System.Windows.HorizontalAlignment.Right;

        if (!_loading)
        {
            PreviewChanged?.Invoke();
        }
    }

    private static double Parse(string text, double fallback, double min, double max)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static Brush ParseBrush(string value, Color fallback)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }
        catch
        {
            return new SolidColorBrush(fallback);
        }
    }
}
