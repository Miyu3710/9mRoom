using System.Windows;
using MaterialDesignThemes.Wpf;

namespace NineMRoom.Client.App;

public static class ThemeService
{
    public static bool IsLight(string? theme) =>
        string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);

    public static void Apply(string? theme)
    {
        var light = IsLight(theme);
        // The BundledTheme in App.xaml provides a valid default palette. Only
        // replace it through PaletteHelper when explicitly switching to light.
        if (light)
        {
            try
            {
                var palette = new PaletteHelper();
                var current = palette.GetTheme();
                current.SetBaseTheme(BaseTheme.Light);
                palette.SetTheme(current);
            }
            catch (Exception ex)
            {
                LogService.Write($"theme palette failed: {ex.Message}");
            }
        }

        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var tokens = new ResourceDictionary
        {
            Source = new Uri(
                light
                    ? "pack://application:,,,/Themes/TokensLight.xaml"
                    : "pack://application:,,,/Themes/Tokens.xaml",
                UriKind.Absolute)
        };
        var controls = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/Controls.xaml", UriKind.Absolute)
        };
        var design = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/Design.xaml", UriKind.Absolute)
        };

        dictionaries.Add(tokens);
        dictionaries.Add(controls);
        dictionaries.Add(design);
    }
}
