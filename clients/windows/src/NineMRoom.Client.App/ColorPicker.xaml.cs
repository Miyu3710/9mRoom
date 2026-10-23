using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Colors = System.Windows.Media.Colors;
using M3Button = NineMRoom.Client.App.Controls.M3Button;
using FormsColorDialog = System.Windows.Forms.ColorDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty HexProperty = DependencyProperty.Register(
        nameof(Hex),
        typeof(string),
        typeof(ColorPicker),
        new FrameworkPropertyMetadata(
            "#FFFFFFFF",
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnHexChanged));

    private static readonly string[] PaletteColors =
    {
        "#00000000", "#D9232327", "#F217171A", "#2D2D33", "#3A3A42", "#8034343A",
        "#FFFFFF", "#ECECF0", "#9B9BA4", "#7AA2F7", "#4CC9F0", "#B388FF",
        "#FF6B6B", "#FFB86C", "#FFD166", "#7BD88F", "#F78FB3", "#E05C5C"
    };

    private bool _updating;

    public ColorPicker()
    {
        InitializeComponent();
        BuildPalette();
        HexBox.TextChanged += (_, _) =>
        {
            if (_updating)
            {
                return;
            }

            Hex = HexBox.Text.Trim();
        };
        SyncText();
    }

    public event Action? HexChanged;

    public string Hex
    {
        get => (string)GetValue(HexProperty);
        set => SetValue(HexProperty, value);
    }

    private static void OnHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPicker picker)
        {
            picker.SyncText();
            picker.HexChanged?.Invoke();
        }
    }

    private void SyncText()
    {
        _updating = true;
        try
        {
            if (!string.Equals(HexBox.Text, Hex, StringComparison.Ordinal))
            {
                HexBox.Text = Hex;
            }
        }
        finally
        {
            _updating = false;
        }

        UpdateSwatch();
    }

    private void BuildPalette()
    {
        foreach (var hex in PaletteColors)
        {
            var swatch = new Border
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)FindResource("BrushOutlineVariant"),
                Background = ParseBrush(hex)
            };

            var button = new M3Button
            {
                Style = (Style)FindResource("M3IconButton"),
                Content = swatch,
                Tag = hex,
                Width = 30,
                Height = 30,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = hex
            };
            button.Click += (_, _) =>
            {
                Hex = (string)button.Tag;
                PalettePopup.IsOpen = false;
            };
            Palette.Children.Add(button);
        }

        var wheel = new M3Button
        {
            Style = (Style)FindResource("M3TextButton"),
            Content = "颜色画盘…",
            Width = 210,
            Height = 30,
            Margin = new Thickness(2, 6, 2, 2),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        wheel.Click += (_, _) => OpenColorWheel();
        Palette.Children.Add(wheel);
    }

    private void OpenColorWheel()
    {
        var current = ParseColor(Hex);
        using var dialog = new FormsColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.Color.FromArgb(current.A, current.R, current.G, current.B)
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        Hex = $"#{current.A:X2}{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        PalettePopup.IsOpen = false;
    }

    private static Color ParseColor(string? value)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value ?? string.Empty);
        }
        catch
        {
            return Colors.White;
        }
    }

    private void OpenPalette_Click(object sender, RoutedEventArgs e) => PalettePopup.IsOpen = !PalettePopup.IsOpen;

    private void UpdateSwatch()
    {
        Swatch.Background = ParseBrush(Hex);
    }

    private static Brush ParseBrush(string? value)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value ?? string.Empty));
        }
        catch
        {
            return Brushes.Transparent;
        }
    }
}
