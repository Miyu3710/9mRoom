using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Colors = System.Windows.Media.Colors;
using FontFamily = System.Windows.Media.FontFamily;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using RadioButton = System.Windows.Controls.RadioButton;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class SettingsView : UserControl
{
    private bool _syncingPreset;
    private bool _loadingTheme;
    private string _fontFamily = "Segoe UI";
    private string _fontWeight = "SemiBold";
    private double _fontSize = 12;
    private string _background = "#D9232327";
    private string _text = "#ECECF0";
    private string _muted = "#9B9BA4";
    private string _border = "#8034343A";
    private double _radius = 10;
    private double _padding = 12;
    private double _width = 420;
    private string _layout = "twoLine";
    private string _position = "bottomRight";
    private bool _showHeader = true;
    private bool _wrapInOneLine;

    public SettingsView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            SyncPreset();
            LoadThemeControls();
        };
        DataContextChanged += (_, _) =>
        {
            SyncPreset();
            LoadThemeControls();
        };
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Preset_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncingPreset || ViewModel is null || sender is not RadioButton button)
        {
            return;
        }

        if (button.Tag is string preset)
        {
            ViewModel.OverlayPreset = preset;
            LoadThemeControls();
        }
    }

    private void SyncPreset()
    {
        if (ViewModel is null)
        {
            return;
        }

        _syncingPreset = true;
        foreach (var button in PresetPanel.Children.OfType<RadioButton>())
        {
            button.IsChecked = string.Equals(button.Tag as string, ViewModel.OverlayPreset, StringComparison.Ordinal);
        }

        _syncingPreset = false;
    }

    private void LoadThemeControls()
    {
        if (ViewModel is null)
        {
            return;
        }

        _loadingTheme = true;
        try
        {
            var theme = ViewModel.GetEffectiveTheme();
            _fontFamily = string.IsNullOrWhiteSpace(theme.FontFamily) ? "Segoe UI" : theme.FontFamily;
            _fontWeight = theme.FontWeight;
            _fontSize = theme.FontSize;
            _background = theme.Background;
            _text = theme.Text;
            _muted = theme.Muted;
            _border = theme.Border;
            _radius = theme.CornerRadius;
            _padding = theme.Padding;
            _width = theme.Width;
            _layout = theme.Layout;
            _position = theme.Position;
            _showHeader = theme.ShowHeader;
            _wrapInOneLine = theme.WrapInOneLine;

            UpdateSummaries();
            UpdatePreview(theme);
        }
        finally
        {
            _loadingTheme = false;
        }
    }

    private void UpdateSummaries()
    {
        FontSummary.Text = _fontFamily + " · " + WeightLabel(_fontWeight) + " · " +
                           _fontSize.ToString("0.#", CultureInfo.InvariantCulture) + "px";
        BorderSummary.Text = "不透明度 " + AlphaPercent(_background) + "% · " +
                             _width.ToString("0.#", CultureInfo.InvariantCulture) + "px · 圆角 " +
                             _radius.ToString("0.#", CultureInfo.InvariantCulture) + " · " +
                             (_layout == "oneLine" ? "单行" : "双行") + " · " +
                             (_position == "bottomCenter" ? "底部居中" : "右下");
    }

    private static int AlphaPercent(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            return (int)Math.Round(color.A * 100.0 / 255.0);
        }
        catch
        {
            return 85;
        }
    }

    private OverlayTheme ReadTheme() => new()
    {
        Name = "自定义",
        Background = _background,
        Text = _text,
        Muted = _muted,
        Border = _border,
        FontSize = _fontSize,
        FontFamily = _fontFamily,
        WrapInOneLine = _wrapInOneLine,
        FontWeight = _fontWeight,
        CornerRadius = _radius,
        Padding = _padding,
        Width = _width,
        BorderThickness = 1,
        Layout = _layout,
        ShowHeader = _showHeader,
        Position = _position
    };

    private void ChooseFont_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var originalTheme = ViewModel.GetEffectiveTheme();
        var originalPreset = ViewModel.OverlayPreset;
        var dialog = new FontDialog(_fontFamily, _fontWeight, _fontSize)
        {
            Owner = Window.GetWindow(this)
        };

        dialog.PreviewChanged += () =>
        {
            var theme = ReadTheme();
            theme.FontFamily = dialog.CurrentFontFamily;
            theme.FontWeight = dialog.CurrentFontWeight;
            theme.FontSize = dialog.CurrentFontSize;
            ViewModel.PreviewCustomTheme(theme);
            UpdatePreview(theme);
        };

        if (dialog.ShowDialog() != true)
        {
            RestoreTheme(originalTheme, originalPreset);
            return;
        }

        _fontFamily = string.IsNullOrWhiteSpace(dialog.ResultFontFamily) ? "Segoe UI" : dialog.ResultFontFamily;
        _fontWeight = dialog.ResultFontWeight;
        _fontSize = dialog.ResultFontSize;
        UpdateSummaries();
        ViewModel.SaveCustomTheme(ReadTheme());
        SyncPreset();
        LoadThemeControls();
    }

    private void ChooseBorder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var originalTheme = ViewModel.GetEffectiveTheme();
        var originalPreset = ViewModel.OverlayPreset;
        var dialog = new BorderDialog(
            _background,
            _text,
            _muted,
            _border,
            _radius,
            _padding,
            _width,
            _layout,
            _position,
            _showHeader,
            _wrapInOneLine,
            _fontFamily,
            _fontSize,
            _fontWeight)
        {
            Owner = Window.GetWindow(this)
        };

        dialog.PreviewChanged += () =>
        {
            var theme = dialog.BuildTheme();
            ViewModel.PreviewCustomTheme(theme);
            UpdatePreview(theme);
        };

        if (dialog.ShowDialog() != true)
        {
            RestoreTheme(originalTheme, originalPreset);
            return;
        }

        _background = dialog.ResultBackground;
        _text = dialog.ResultText;
        _muted = dialog.ResultMuted;
        _border = dialog.ResultBorder;
        _radius = dialog.ResultRadius;
        _padding = dialog.ResultPadding;
        _width = dialog.ResultWidth;
        _layout = dialog.ResultLayout;
        _position = dialog.ResultPosition;
        _showHeader = dialog.ResultShowHeader;
        _wrapInOneLine = dialog.ResultWrapInOneLine;
        UpdateSummaries();
        ViewModel.SaveCustomTheme(ReadTheme());
        SyncPreset();
        LoadThemeControls();
    }

    private void RestoreTheme(OverlayTheme theme, string preset)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (string.Equals(preset, "custom", StringComparison.Ordinal))
        {
            ViewModel.PreviewCustomTheme(theme);
        }
        else
        {
            ViewModel.OverlayPreset = preset;
        }

        LoadThemeControls();
        SyncPreset();
    }

    private void PreviewEdited()
    {
        if (_loadingTheme || ViewModel is null)
        {
            return;
        }

        var theme = ReadTheme();
        ViewModel.PreviewCustomTheme(theme);
        UpdatePreview(theme);
        SyncPreset();
    }

    private static string WeightLabel(string weight) => weight switch
    {
        "Normal" => "常规",
        "Medium" => "中等",
        _ => "半粗"
    };

    private void UpdatePreview(OverlayTheme theme)
    {
        var textBrush = ParseBrush(theme.Text, Color.FromRgb(0xEC, 0xEC, 0xF0));
        var mutedBrush = ParseBrush(theme.Muted, Color.FromRgb(0x9B, 0x9B, 0xA4));

        ThemePreview.Width = Math.Clamp(theme.Width, 280, 860);
        ThemePreview.Background = ParseBrush(theme.Background, Colors.Transparent);
        ThemePreview.BorderBrush = ParseBrush(theme.Border, Colors.Transparent);
        ThemePreview.BorderThickness = new Thickness(theme.BorderThickness);
        ThemePreview.CornerRadius = new CornerRadius(theme.CornerRadius);
        ThemePreview.Padding = new Thickness(theme.Padding);

        var family = new FontFamily(string.IsNullOrWhiteSpace(theme.FontFamily) ? "Segoe UI" : theme.FontFamily);
        ThemePreviewHeader.FontFamily = family;
        ThemePreviewHeader.Foreground = mutedBrush;
        ThemePreviewHeader.FontSize = Math.Max(9, theme.FontSize - 2);
        ThemePreviewHeader.Visibility = theme.ShowHeader ? Visibility.Visible : Visibility.Collapsed;

        ThemePreviewText.FontFamily = family;
        ThemePreviewText.Foreground = textBrush;
        ThemePreviewText.FontSize = theme.FontSize;
        ThemePreviewText.FontWeight = theme.FontWeight switch
        {
            "Normal" => FontWeights.Normal,
            "Medium" => FontWeights.Medium,
            _ => FontWeights.SemiBold
        };
        var wrap = theme.Layout != "oneLine" || theme.WrapInOneLine;
        ThemePreviewText.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        ThemePreviewText.TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis;
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

    private void ApplyCustom_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.SaveCustomTheme(ReadTheme());
        SyncPreset();
        LoadThemeControls();
    }

    private void CopyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.CopyCurrentToCustom();
        SyncPreset();
        LoadThemeControls();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出叠加层主题",
            Filter = "JSON 文件 (*.json)|*.json",
            FileName = "9mroom-overlay-theme.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var json = JsonSerializer.Serialize(
            ViewModel.GetEffectiveTheme(),
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dialog.FileName, json);
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "导入叠加层主题",
            Filter = "JSON 文件 (*.json)|*.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var theme = JsonSerializer.Deserialize<OverlayTheme>(File.ReadAllText(dialog.FileName));
            if (theme is null)
            {
                return;
            }

            ViewModel.SaveCustomTheme(theme);
            SyncPreset();
            LoadThemeControls();
        }
        catch
        {
            MessageBox.Show("主题文件格式不正确。", "9mRoom", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ResetOverlay_Click(object sender, RoutedEventArgs e) => ViewModel?.RequestOverlayReset();

    private void OpenSettingsFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = SettingsStore.CurrentPath;
        if (string.IsNullOrEmpty(path))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "9mRoom");
        }

        try
        {
            var target = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{path}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
        }
        catch
        {
            // 打开资源管理器失败时忽略
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        var path = LogService.DirectoryPath;

        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            // 打开资源管理器失败时忽略
        }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.CheckForUpdatesAsync();
        }
    }

    private void DownloadUpdate_Click(object sender, RoutedEventArgs e) => ViewModel?.OpenUpdateDownload();
}
