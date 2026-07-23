using LightOCR.App.Interop.Win32;

namespace LightOCR.App.Services;

public static class DpiService
{
    public static double GetScaleFactor(IntPtr hwnd)
    {
        var hdc = User32.GetDC(hwnd);
        if (hdc == IntPtr.Zero) return 1.0;

        int logicalW = Gdi32.GetDeviceCaps(hdc, Gdi32.HORZRES);
        int physicalW = Gdi32.GetDeviceCaps(hdc, Gdi32.DESKTOPHORZRES);
        User32.ReleaseDC(hwnd, hdc);

        if (logicalW == 0) return 1.0;
        return (double)physicalW / logicalW;
    }

    public static System.Windows.Point DipsToPhysical(System.Windows.Point dips, double scale)
        => new(dips.X * scale, dips.Y * scale);

    public static System.Windows.Point PhysicalToDips(System.Windows.Point physical, double scale)
        => new(physical.X / scale, physical.Y / scale);

    public static System.Drawing.Point DipsToPhysical(System.Drawing.Point dips, double scale)
        => new((int)(dips.X * scale), (int)(dips.Y * scale));
}
