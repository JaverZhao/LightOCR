using System.Windows.Media.Imaging;

namespace LightOCR.App.Models;

public sealed record NormalizedImage(
    int Width,
    int Height,
    int Stride,
    byte[] BgraBytes,
    string SourceName,
    BitmapSource? Preview = null);
