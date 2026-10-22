using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using FontFamily = System.Windows.Media.FontFamily;

namespace NineMRoom.Client.App;

public partial class FontDialog : Window
{
    private bool _loading = true;

    public FontDialog(string fontFamily, string fontWeight, double fontSize)
    {
        InitializeComponent();

        var options = Fonts.SystemFontFamilies
            .Select(font => new FontOption(DisplayName(font), font.Source))
            .DistinctBy(option => option.Source)
            .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        FontList.ItemsSource = options;
        FontList.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option.Source, fontFamily, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(option.DisplayName, fontFamily, StringComparison.OrdinalIgnoreCase)) ??
            options.FirstOrDefault();

        WeightNormal.IsChecked = fontWeight == "Normal";
        WeightMedium.IsChecked = fontWeight == "Medium";
        WeightSemi.IsChecked = fontWeight is not ("Normal" or "Medium");
        SizeBox.Text = fontSize.ToString("0.#", CultureInfo.InvariantCulture);

        FontList.SelectionChanged += (_, _) => OnChanged();
        WeightNormal.Checked += (_, _) => OnChanged();
        WeightMedium.Checked += (_, _) => OnChanged();
        WeightSemi.Checked += (_, _) => OnChanged();
        SizeBox.TextChanged += (_, _) => OnChanged();

        _loading = false;
        UpdatePreview();
    }

    public event Action? PreviewChanged;

    public string ResultFontFamily { get; private set; } = "Segoe UI";

    public string ResultFontWeight { get; private set; } = "SemiBold";

    public double ResultFontSize { get; private set; } = 12;

    public string CurrentFontFamily =>
        (FontList.SelectedItem as FontOption)?.Source ?? "Segoe UI";

    public string CurrentFontWeight => WeightNormal.IsChecked == true
        ? "Normal"
        : WeightMedium.IsChecked == true
            ? "Medium"
            : "SemiBold";

    public double CurrentFontSize => ParseSize(SizeBox.Text);

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void FontList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var viewer = FindScrollViewer(FontList);
        if (viewer is null)
        {
            return;
        }

        var step = e.Delta > 0 ? -1 : 1;
        viewer.ScrollToVerticalOffset(
            Math.Clamp(viewer.VerticalOffset + step, 0, viewer.ScrollableHeight));
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }

            var nested = FindScrollViewer(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        ResultFontFamily = CurrentFontFamily;
        ResultFontWeight = CurrentFontWeight;
        ResultFontSize = CurrentFontSize;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnChanged()
    {
        if (_loading)
        {
            return;
        }

        UpdatePreview();
        PreviewChanged?.Invoke();
    }

    private void UpdatePreview()
    {
        if (PreviewText is null)
        {
            return;
        }

        var family = new FontFamily(CurrentFontFamily);
        var weight = CurrentFontWeight switch
        {
            "Normal" => FontWeights.Normal,
            "Medium" => FontWeights.Medium,
            _ => FontWeights.SemiBold
        };
        var size = CurrentFontSize;

        PreviewText.FontFamily = family;
        PreviewTitle.FontFamily = family;
        PreviewText.FontWeight = weight;
        PreviewText.FontSize = size;
        PreviewTitle.FontSize = Math.Max(9, size - 2);
    }

    private static string DisplayName(FontFamily font)
    {
        var language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        if (font.FamilyNames.TryGetValue(language, out var localized))
        {
            return localized;
        }

        var chinese = XmlLanguage.GetLanguage("zh-cn");
        return font.FamilyNames.TryGetValue(chinese, out localized) ? localized : font.Source;
    }

    private static double ParseSize(string text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return 12;
        }

        return Math.Clamp(value, 8, 32);
    }

    private sealed record FontOption(string DisplayName, string Source);
}
