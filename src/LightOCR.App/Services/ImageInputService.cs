using LightOCR.App.Models;
using Serilog;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightOCR.App.Services;

public class ImageInputService
{
    private const int MaxDimension = 10000;
    private const int MaxPixels = 80_000_000;

    public async Task<NormalizedImage?> FromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Log.Warning("File not found: {Path}", filePath);
            return null;
        }

        try
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var supported = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };
            if (!supported.Contains(ext))
            {
                Log.Warning("Unsupported format: {Ext}", ext);
                return null;
            }

            var data = await File.ReadAllBytesAsync(filePath);
            return DecodeImage(data, Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load image: {Path}", filePath);
            return null;
        }
    }

    public Task<NormalizedImage?> FromClipboardAsync()
    {
        try
        {
            var dataObject = System.Windows.Clipboard.GetDataObject();
            if (dataObject?.GetDataPresent(DataFormats.Bitmap) == true)
            {
                if (dataObject.GetData(DataFormats.Bitmap) is System.Drawing.Bitmap bitmap)
                {
                    using var ms = new MemoryStream();
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    var data = ms.ToArray();
                    return Task.FromResult(DecodeImage(data, "剪贴板"));
                }
            }
            return Task.FromResult<NormalizedImage?>(null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to paste image");
            return Task.FromResult<NormalizedImage?>(null);
        }
    }

    private static NormalizedImage? DecodeImage(byte[] data, string sourceName)
    {
        var ms = new MemoryStream(data);
        var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);

        var frame = decoder.Frames[0];
        int w = frame.PixelWidth;
        int h = frame.PixelHeight;

        if (w > MaxDimension || h > MaxDimension || w * h > MaxPixels)
        {
            Log.Warning("Image too large: {W}x{H}", w, h);
            return null;
        }

        var bgra = new byte[w * h * 4];
        var stride = w * 4;

        if (frame.Format == PixelFormats.Bgra32)
        {
            frame.CopyPixels(bgra, stride, 0);
        }
        else
        {
            var formatted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            formatted.CopyPixels(bgra, stride, 0);
        }

        var preview = CreatePreview(frame, w, h);

        return new NormalizedImage(w, h, stride, bgra, sourceName, preview);
    }

    private static BitmapSource CreatePreview(BitmapSource frame, int w, int h)
    {
        const int maxPreview = 400;
        if (w <= maxPreview && h <= maxPreview)
            return frame;

        double scale = Math.Min((double)maxPreview / w, (double)maxPreview / h);
        int pw = (int)(w * scale);
        int ph = (int)(h * scale);

        var scaled = new TransformedBitmap(frame,
            new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }
}
