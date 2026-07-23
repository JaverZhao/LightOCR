using System.Windows;
using System.Windows.Forms;

namespace LightOCR.App.Services;

public sealed class ScreenTopologyInfo
{
    public System.Drawing.Rectangle Bounds { get; init; }
    public System.Drawing.Rectangle WorkingArea { get; init; }
    public string DeviceName { get; init; } = "";
    public bool IsPrimary { get; init; }
    public double ScaleFactor { get; init; } = 1.0;
}

public static class ScreenTopologyService
{
    public static ScreenTopologyInfo[] GetAllScreens()
    {
        return Screen.AllScreens.Select(s => new ScreenTopologyInfo
        {
            Bounds = s.Bounds,
            WorkingArea = s.WorkingArea,
            DeviceName = s.DeviceName,
            IsPrimary = s.Primary,
            ScaleFactor = DpiService.GetScaleFactor(IntPtr.Zero)
        }).ToArray();
    }

    public static System.Drawing.Rectangle VirtualScreenBounds()
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var screen in Screen.AllScreens)
        {
            minX = Math.Min(minX, screen.Bounds.X);
            minY = Math.Min(minY, screen.Bounds.Y);
            maxX = Math.Max(maxX, screen.Bounds.Right);
            maxY = Math.Max(maxY, screen.Bounds.Bottom);
        }

        return new System.Drawing.Rectangle(minX, minY, maxX - minX, maxY - minY);
    }
}
