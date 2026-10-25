using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NineMRoom.Client.Core;

namespace NineMRoom.Client.App;

public static class AvatarStore
{
    public const int OutputSize = 512;
    public const int MaxBytes = 128 * 1024;

    private static string CacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "9mRoom", "avatar");

    public static string CachePath => Path.Combine(CacheDirectory, "avatar.jpg");

    public static AvatarPayload? LoadCached()
    {
        try
        {
            if (!File.Exists(CachePath))
            {
                return null;
            }

            var bytes = File.ReadAllBytes(CachePath);
            if (bytes.Length == 0 || bytes.Length > MaxBytes)
            {
                return null;
            }

            var (width, height) = ReadSize(bytes);
            return new AvatarPayload(
                "image/jpeg",
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                width,
                height,
                Convert.ToBase64String(bytes));
        }
        catch
        {
            return null;
        }
    }

    public static AvatarPayload? CreateFromFile(string path)
    {
        try
        {
            var source = LoadImage(path);
            var side = Math.Min(source.PixelWidth, source.PixelHeight);
            if (side <= 0)
            {
                return null;
            }

            var left = (source.PixelWidth - side) / 2;
            var top = (source.PixelHeight - side) / 2;
            var cropped = new CroppedBitmap(source, new Int32Rect(left, top, side, side));
            cropped.Freeze();

            var scale = OutputSize / (double)side;
            var resized = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));
            resized.Freeze();

            foreach (var size in new[] { OutputSize, 448, 384, 320 })
            {
                BitmapSource current = size == OutputSize
                    ? resized
                    : new TransformedBitmap(resized, new ScaleTransform(size / (double)OutputSize, size / (double)OutputSize));
                current.Freeze();

                foreach (var quality in new[] { 86, 78, 70, 60, 50 })
                {
                    var bytes = EncodeJpeg(current, quality);
                    if (bytes.Length <= 96 * 1024)
                    {
                        var (width, height) = ReadSize(bytes);
                        return new AvatarPayload(
                            "image/jpeg",
                            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                            width,
                            height,
                            Convert.ToBase64String(bytes));
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static bool Save(AvatarPayload avatar)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            var bytes = Convert.FromBase64String(avatar.Data);
            var temp = CachePath + ".tmp";
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, CachePath, overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static ImageSource? ToImage(AvatarPayload? avatar, int decodePixelWidth = 128)
    {
        if (avatar is null || string.IsNullOrWhiteSpace(avatar.Data))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(avatar.Data);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = decodePixelWidth;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapSource LoadImage(string path)
    {
        using var stream = File.OpenRead(path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.DecodePixelWidth = 1024;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static byte[] EncodeJpeg(BitmapSource source, int quality)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static (int Width, int Height) ReadSize(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.DelayCreation,
            BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }
}
