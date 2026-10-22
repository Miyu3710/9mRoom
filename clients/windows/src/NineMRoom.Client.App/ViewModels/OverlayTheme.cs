namespace NineMRoom.Client.App;

public sealed class OverlayTheme
{
    public string Name { get; set; } = "自定义";
    public string Layout { get; set; } = "twoLine";
    public double Width { get; set; } = 420;
    public string Background { get; set; } = "#D9232327";
    public string Border { get; set; } = "#8034343A";
    public string Text { get; set; } = "#ECECF0";
    public string Muted { get; set; } = "#9B9BA4";
    public double FontSize { get; set; } = 12;
    public string FontWeight { get; set; } = "SemiBold";
    public string FontFamily { get; set; } = "Segoe UI";
    public bool WrapInOneLine { get; set; }
    public double CornerRadius { get; set; } = 10;
    public double Padding { get; set; } = 12;
    public double BorderThickness { get; set; } = 1;
    public bool ShowHeader { get; set; } = true;
    public string Position { get; set; } = "bottomRight";
}

public static class OverlayThemes
{
    public static OverlayTheme GetPreset(string id) => id switch
    {
        "minimal" => new OverlayTheme
        {
            Name = "极简单行",
            Layout = "oneLine",
            Width = 460,
            Background = "#00000000",
            Border = "#00000000",
            BorderThickness = 0,
            FontSize = 12,
            FontWeight = "Normal",
            CornerRadius = 0,
            Padding = 6,
            ShowHeader = false,
            Position = "bottomRight"
        },
        "compact" => new OverlayTheme
        {
            Name = "紧凑双行",
            Layout = "twoLine",
            Width = 400,
            Background = "#D9232327",
            Border = "#8034343A",
            FontSize = 11,
            FontWeight = "SemiBold",
            CornerRadius = 8,
            Padding = 8,
            ShowHeader = true,
            Position = "bottomRight"
        },
        "subtitle" => new OverlayTheme
        {
            Name = "字幕条",
            Layout = "oneLine",
            Width = 760,
            Background = "#E6232327",
            Border = "#8034343A",
            FontSize = 12,
            FontWeight = "SemiBold",
            CornerRadius = 6,
            Padding = 10,
            ShowHeader = false,
            Position = "bottomCenter"
        },
        _ => new OverlayTheme
        {
            Name = "卡片",
            Layout = "twoLine",
            Width = 420,
            Background = "#D9232327",
            Border = "#8034343A",
            FontSize = 12,
            FontWeight = "SemiBold",
            CornerRadius = 10,
            Padding = 12,
            ShowHeader = true,
            Position = "bottomRight"
        }
    };

    public static OverlayTheme DefaultCustom => GetPreset("card");

    public static bool IsPreset(string id) =>
        id is "minimal" or "compact" or "card" or "subtitle";
}
