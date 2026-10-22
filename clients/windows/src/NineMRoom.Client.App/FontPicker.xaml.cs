using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class FontPicker : UserControl
{
    public static readonly DependencyProperty FontNameProperty = DependencyProperty.Register(
        nameof(FontName),
        typeof(string),
        typeof(FontPicker),
        new FrameworkPropertyMetadata(
            "Segoe UI",
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnFontNameChanged));

    private bool _updating;

    public FontPicker()
    {
        InitializeComponent();

        FontList.ItemsSource = Fonts.SystemFontFamilies
            .Select(font => font.Source)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        FontBox.TextChanged += (_, _) =>
        {
            if (_updating)
            {
                return;
            }

            FontName = FontBox.Text.Trim();
        };

        SyncText();
    }

    public event Action? FontChanged;

    public string FontName
    {
        get => (string)GetValue(FontNameProperty);
        set => SetValue(FontNameProperty, value);
    }

    private static void OnFontNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FontPicker picker)
        {
            picker.SyncText();
            picker.FontChanged?.Invoke();
        }
    }

    private void SyncText()
    {
        _updating = true;
        try
        {
            if (!string.Equals(FontBox.Text, FontName, StringComparison.Ordinal))
            {
                FontBox.Text = FontName;
            }

            FontList.SelectedItem = FontList.Items
                .OfType<string>()
                .FirstOrDefault(name => string.Equals(name, FontName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _updating = false;
        }
    }

    private void OpenList_Click(object sender, RoutedEventArgs e)
    {
        SyncText();
        FontPopup.IsOpen = !FontPopup.IsOpen;
    }

    private void FontList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || FontList.SelectedItem is not string name)
        {
            return;
        }

        FontName = name;
        FontPopup.IsOpen = false;
    }
}
