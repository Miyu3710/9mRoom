using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NineMRoom.Client.App;

internal sealed class DarkMenuColorTable : ProfessionalColorTable
{
    private static readonly Color Background = Color.FromArgb(28, 28, 32);
    private static readonly Color Selection = Color.FromArgb(51, 53, 66);
    private static readonly Color Border = Color.FromArgb(74, 76, 90);

    public override Color ToolStripDropDownBackground => Background;
    public override Color MenuItemSelected => Selection;
    public override Color MenuItemSelectedGradientBegin => Selection;
    public override Color MenuItemSelectedGradientEnd => Selection;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuBorder => Border;
    public override Color ImageMarginGradientBegin => Background;
    public override Color ImageMarginGradientMiddle => Background;
    public override Color ImageMarginGradientEnd => Background;
    public override Color SeparatorDark => Border;
    public override Color SeparatorLight => Border;
}

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color Background = Color.FromArgb(28, 28, 32);
    private static readonly Color Border = Color.FromArgb(74, 76, 90);
    private static readonly Color Text = Color.FromArgb(238, 238, 242);
    private static readonly Color TextDisabled = Color.FromArgb(132, 132, 146);
    private static readonly Color Selection = Color.FromArgb(51, 53, 66);
    private static readonly Color Check = Color.FromArgb(198, 198, 208);

    public DarkMenuRenderer() : base(new DarkMenuColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Rounded(e.AffectedBounds, 10);
        using var brush = new SolidBrush(Background);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Rounded(new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 10);
        using var pen = new Pen(Border);
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(3, 1, Math.Max(1, e.Item.Width - 6), Math.Max(1, e.Item.Height - 2));
        using var path = Rounded(bounds, 6);
        using var brush = new SolidBrush(Selection);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Text : TextDisabled;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        using var font = new Font("Segoe UI Symbol", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        var size = TextRenderer.MeasureText("✓", font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        var x = 7;
        var y = Math.Max(1, (e.Item.Height - size.Height) / 2);
        TextRenderer.DrawText(
            e.Graphics,
            "✓",
            font,
            new Point(x, y),
            Check,
            TextFormatFlags.NoPadding);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var y = Math.Max(1, e.Item.Height / 2);
        using var pen = new Pen(Border);
        e.Graphics.DrawLine(pen, 12, y, Math.Max(13, e.Item.Width - 12), y);
    }

    private static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Max(1, radius * 2);
        var rect = new Rectangle(bounds.X, bounds.Y, Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
