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

        // Merge order matters: semantic tokens first, then the M3 component
        // library that consumes them. Tokens use DynamicResource inside the
        // component dictionaries so the light palette is not overwritten.
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(
                light
                    ? "pack://application:,,,/Themes/TokensLight.xaml"
                    : "pack://application:,,,/Themes/Tokens.xaml",
                UriKind.Absolute)
        });
        dictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/M3.xaml", UriKind.Absolute)
        });

        // 组件库只用于 PackIcon 与基础主题；控件模板为项目内的 M3 实现。
    }

    /// <summary>
    /// Outlined text field 的基样式来自 MaterialDesignThemes。资源字典里的 Style 在加载后
    /// 会密封，不能直接改 BasedOn，因此按“组件库基样式 + 项目覆盖”重建一份并替换资源键。
    /// </summary>
    private static void RebuildComponentStyles()
    {
        var resources = System.Windows.Application.Current.Resources;
        var textBox = Rebuild("M3TextBox", resources["MaterialDesignOutlinedTextBox"] as Style);
        var combo = Rebuild("M3ComboBox", resources["MaterialDesignOutlinedComboBox"] as Style);
        Rebuild("M3TextBoxCompact", textBox);
        Rebuild("M3SearchField", textBox);
        RebuildImplicit(typeof(System.Windows.Controls.TextBox), textBox);
        RebuildImplicit(typeof(System.Windows.Controls.ComboBox), combo);
    }

    private static Style? Rebuild(string key, Style? baseStyle)
    {
        var resources = System.Windows.Application.Current.Resources;
        if (baseStyle is null || resources[key] is not Style own)
        {
            LogService.Write($"component base style missing: {key}");
            return null;
        }

        var style = new Style(own.TargetType, baseStyle);
        foreach (var setter in own.Setters.OfType<Setter>())
        {
            style.Setters.Add(new Setter(setter.Property, setter.Value));
        }

        foreach (var trigger in own.Triggers)
        {
            style.Triggers.Add(trigger);
        }

        resources[key] = style;
        return style;
    }

    private static void RebuildImplicit(Type targetType, Style? baseStyle)
    {
        if (baseStyle is null)
        {
            return;
        }

        var style = new Style(targetType, baseStyle);
        System.Windows.Application.Current.Resources[targetType] = style;
    }
}