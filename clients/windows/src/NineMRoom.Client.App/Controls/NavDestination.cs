using MaterialDesignThemes.Wpf;

namespace NineMRoom.Client.App.Controls;

/// <summary>一级导航目的地。</summary>
public sealed class NavDestination
{
    public string Label { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public PackIconKind Glyph { get; set; } = PackIconKind.CircleOutline;

    public string AutomationName { get; set; } = string.Empty;
}
