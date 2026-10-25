using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Size = System.Windows.Size;

namespace NineMRoom.Client.App.Controls;

/// <summary>
/// 方圆形（squircle）背景：用超椭圆代替普通圆角矩形，四角过渡更连续。
/// 只负责绘制背景，不参与内容布局。
/// </summary>
public sealed class Squircle : FrameworkElement
{
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(Squircle),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PowerProperty = DependencyProperty.Register(
        nameof(Power),
        typeof(double),
        typeof(Squircle),
        new FrameworkPropertyMetadata(4.2, FrameworkPropertyMetadataOptions.AffectsRender));

    private Size _cachedSize;
    private Geometry? _cachedGeometry;

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>超椭圆指数，越大越接近直角矩形，4 左右接近 iOS 方圆形。</summary>
    public double Power
    {
        get => (double)GetValue(PowerProperty);
        set => SetValue(PowerProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsNaN(Width)
            ? (double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width)
            : Width;
        var height = double.IsNaN(Height)
            ? (double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height)
            : Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _cachedGeometry = null;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Fill is null)
        {
            return;
        }

        var geometry = BuildGeometry(RenderSize);
        if (!geometry.IsEmpty())
        {
            drawingContext.DrawGeometry(Fill, null, geometry);
        }
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var point = hitTestParameters.HitPoint;
        return BuildGeometry(RenderSize).FillContains(point)
            ? new PointHitTestResult(this, point)
            : null;
    }

    private Geometry BuildGeometry(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return Geometry.Empty;
        }

        if (_cachedGeometry is not null && _cachedSize == size)
        {
            return _cachedGeometry;
        }

        var halfWidth = size.Width / 2;
        var halfHeight = size.Height / 2;
        var exponent = 2.0 / Math.Max(2.0, Power);
        const int segments = 72;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            for (var index = 0; index < segments; index++)
            {
                var angle = 2 * Math.PI * index / segments;
                var cos = Math.Cos(angle);
                var sin = Math.Sin(angle);
                var x = halfWidth + halfWidth * Math.Sign(cos) * Math.Pow(Math.Abs(cos), exponent);
                var y = halfHeight + halfHeight * Math.Sign(sin) * Math.Pow(Math.Abs(sin), exponent);
                var point = new Point(x, y);

                if (index == 0)
                {
                    context.BeginFigure(point, isFilled: true, isClosed: true);
                }
                else
                {
                    context.LineTo(point, isStroked: false, isSmoothJoin: false);
                }
            }
        }

        geometry.Freeze();
        _cachedGeometry = geometry;
        _cachedSize = size;
        return geometry;
    }
}
