using LightOCR.App.Interop.Win32;
using LightOCR.App.Models;
using Serilog;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LightOCR.App.Services;

public static class CaptureService
{
    public static NormalizedImage? CaptureScreen()
    {
        var bounds = ScreenTopologyService.VirtualScreenBounds();
        return CaptureRect(bounds);
    }

    public static NormalizedImage? CaptureRect(System.Drawing.Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return null;

        IntPtr hdcScreen = User32.GetDC(IntPtr.Zero);
        IntPtr hdcMem = Gdi32.CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = Gdi32.CreateCompatibleBitmap(hdcScreen, rect.Width, rect.Height);
        IntPtr hOld = Gdi32.SelectObject(hdcMem, hBitmap);

        try
        {
            Gdi32.BitBlt(hdcMem, 0, 0, rect.Width, rect.Height,
                hdcScreen, rect.X, rect.Y, Gdi32.SRCCOPY);

            using var bitmap = Image.FromHbitmap(hBitmap);

            // Convert to BGRA byte array
            var bgra = new byte[rect.Width * rect.Height * 4];
            var bmpData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, rect.Width, rect.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            Marshal.Copy(bmpData.Scan0, bgra, 0, bgra.Length);
            bitmap.UnlockBits(bmpData);

            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(rect.Width, rect.Height));
            source.Freeze();

            return new NormalizedImage(
                rect.Width, rect.Height, rect.Width * 4, bgra,
                $"截图 ({rect.Width}x{rect.Height})", source);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Screen capture failed");
            return null;
        }
        finally
        {
            Gdi32.SelectObject(hdcMem, hOld);
            Gdi32.DeleteObject(hBitmap);
            Gdi32.DeleteDC(hdcMem);
            User32.ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    public static NormalizedImage? CaptureRegion(NormalizedImage fullScreen,
        System.Drawing.Rectangle region)
    {
        var imageBounds = new System.Drawing.Rectangle(0, 0, fullScreen.Width, fullScreen.Height);
        var safeRegion = System.Drawing.Rectangle.Intersect(imageBounds, region);
        if (safeRegion.Width <= 0 || safeRegion.Height <= 0)
            return null;

        int targetStride = safeRegion.Width * 4;
        var cropped = new byte[targetStride * safeRegion.Height];
        for (int y = 0; y < safeRegion.Height; y++)
        {
            int srcOffset = (safeRegion.Y + y) * fullScreen.Stride + safeRegion.X * 4;
            int dstOffset = y * targetStride;
            Buffer.BlockCopy(fullScreen.BgraBytes, srcOffset, cropped, dstOffset, targetStride);
        }

        var preview = BitmapSource.Create(
            safeRegion.Width, safeRegion.Height, 96, 96,
            PixelFormats.Bgra32, null, cropped, targetStride);
        preview.Freeze();

        return new NormalizedImage(
            safeRegion.Width, safeRegion.Height, targetStride, cropped,
            $"截图选区 ({safeRegion.Width}x{safeRegion.Height})", preview);
    }
}
