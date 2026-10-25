using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Button = System.Windows.Controls.Button;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using UserControl = System.Windows.Controls.UserControl;

namespace NineMRoom.Client.App;

public partial class AvatarCropDialog : Window
{
    private const double ViewportSize = 360;

    private BitmapSource? _source;
    private double _zoom = 1;
    private double _baseScale = 1;
    private double _translateX;
    private double _translateY;
    private Point _dragStart;
    private double _dragTranslateX;
    private double _dragTranslateY;
    private bool _dragging;

    public AvatarCropDialog(string path)
    {
        InitializeComponent();
        Load(path);
        Loaded += (_, _) =>
        {
            FitToViewport();
            ApplyTransform();
        };
    }

    public BitmapSource? ResultImage { get; private set; }

    private void Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = 1600;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            _source = image;
            CropImage.Source = image;
            CropImage.Width = image.PixelWidth;
            CropImage.Height = image.PixelHeight;
        }
        catch
        {
            _source = null;
        }
    }

    private void FitToViewport()
    {
        if (_source is null)
        {
            return;
        }

        _zoom = 1;
        _baseScale = Math.Max(ViewportSize / _source.PixelWidth, ViewportSize / _source.PixelHeight);
        var scale = _baseScale;
        _translateX = (ViewportSize - _source.PixelWidth * scale) / 2;
        _translateY = (ViewportSize - _source.PixelHeight * scale) / 2;
    }

    private void ApplyTransform()
    {
        if (_source is null)
        {
            return;
        }

        var scale = _baseScale * _zoom;
        ImageScale.ScaleX = scale;
        ImageScale.ScaleY = scale;
        ClampTranslation(scale);
        ImageTranslate.X = _translateX;
        ImageTranslate.Y = _translateY;
        UpdatePreview();
    }

    private void ClampTranslation(double scale)
    {
        if (_source is null)
        {
            return;
        }

        var minX = Math.Min(0, ViewportSize - _source.PixelWidth * scale);
        var minY = Math.Min(0, ViewportSize - _source.PixelHeight * scale);
        _translateX = Math.Clamp(_translateX, minX, 0);
        _translateY = Math.Clamp(_translateY, minY, 0);
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_source is null || _baseScale <= 0 || ZoomSlider is null)
        {
            return;
        }

        var oldScale = _baseScale * _zoom;
        var centerX = (ViewportSize / 2 - _translateX) / oldScale;
        var centerY = (ViewportSize / 2 - _translateY) / oldScale;

        _zoom = e.NewValue;
        var newScale = _baseScale * _zoom;
        _translateX = ViewportSize / 2 - centerX * newScale;
        _translateY = ViewportSize / 2 - centerY * newScale;
        ApplyTransform();
    }

    private void CropViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_source is null)
        {
            return;
        }

        _dragging = true;
        _dragStart = e.GetPosition(CropViewport);
        _dragTranslateX = _translateX;
        _dragTranslateY = _translateY;
        CropViewport.CaptureMouse();
        e.Handled = true;
    }

    private void CropViewport_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _source is null)
        {
            return;
        }

        var point = e.GetPosition(CropViewport);
        _translateX = _dragTranslateX + (point.X - _dragStart.X);
        _translateY = _dragTranslateY + (point.Y - _dragStart.Y);
        var scale = _baseScale * _zoom;
        ClampTranslation(scale);
        ImageTranslate.X = _translateX;
        ImageTranslate.Y = _translateY;
        UpdatePreview();
    }

    private void CropViewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        CropViewport.ReleaseMouseCapture();
    }

    private Int32Rect? GetCropRect()
    {
        if (_source is null)
        {
            return null;
        }

        var scale = _baseScale * _zoom;
        var side = ViewportSize / scale;
        side = Math.Min(side, Math.Min(_source.PixelWidth, _source.PixelHeight));
        var left = Math.Clamp(-_translateX / scale, 0, _source.PixelWidth - side);
        var top = Math.Clamp(-_translateY / scale, 0, _source.PixelHeight - side);

        var x = (int)Math.Round(left);
        var y = (int)Math.Round(top);
        var size = Math.Max(1, (int)Math.Round(side));
        size = Math.Min(size, Math.Min(_source.PixelWidth - x, _source.PixelHeight - y));
        return new Int32Rect(x, y, size, size);
    }

    private void UpdatePreview()
    {
        if (_source is null || GetCropRect() is not { } rect)
        {
            return;
        }

        try
        {
            var cropped = new CroppedBitmap(_source, rect);
            var scale = 160.0 / rect.Width;
            var preview = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));
            preview.Freeze();
            PreviewImage.Source = preview;
        }
        catch
        {
            PreviewImage.Source = null;
        }
    }

    private BitmapSource? CreateResult()
    {
        if (_source is null || GetCropRect() is not { } rect)
        {
            return null;
        }

        try
        {
            var cropped = new CroppedBitmap(_source, rect);
            var scale = 512.0 / rect.Width;
            var result = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));
            result.Freeze();
            return result;
        }
        catch
        {
            return null;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        ResultImage = CreateResult();
        if (ResultImage is null)
        {
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
