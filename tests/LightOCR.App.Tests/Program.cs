using LightOCR.App.Models;
using LightOCR.App.Services;
using LightOCR.App.ViewModels;
using LightOCR.App.Views;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        VerifyRegionCrop();
        VerifyCaptureRemainsBusyUntilRecognitionCompletes();
        VerifyCoordinatorReload();

        if (args.Contains("--render-ui"))
            RenderMainWindow();

        Console.WriteLine("LightOCR.App regression checks passed.");
        return 0;
    }

    private static void VerifyCaptureRemainsBusyUntilRecognitionCompletes()
    {
        const int width = 20;
        const int height = 20;
        var pixels = Enumerable.Repeat((byte)255, width * height * 4).ToArray();
        var source = new NormalizedImage(width, height, width * 4, pixels, "capture-state-test");
        var session = new CaptureSessionService(() => source);
        var handlerStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        session.CaptureCompleted += async _ =>
        {
            handlerStarted.TrySetResult();
            await allowCompletion.Task;
        };

        Assert(session.BeginCapture(), "Capture should begin");
        session.UpdateSelection(new Rectangle(0, 0, 10, 10));
        var completion = session.CompleteSelectionAsync();
        handlerStarted.Task.GetAwaiter().GetResult();

        Assert(session.State == CaptureState.Recognizing,
            "Capture should remain in Recognizing while OCR handler is running");
        Assert(!session.BeginCapture(), "A second capture must be rejected while recognizing");

        allowCompletion.TrySetResult();
        Assert(completion.GetAwaiter().GetResult() != null, "Capture should complete");
        Assert(session.State == CaptureState.Idle, "Capture should return to Idle after recognition");

        var failedSession = new CaptureSessionService(() => null);
        Assert(!failedSession.BeginCapture(), "Failed screen capture should be reported");
        Assert(failedSession.State == CaptureState.Idle,
            "Failed screen capture must reset so the user can retry");
    }

    private static void VerifyCoordinatorReload()
    {
        var modelDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "models", "onnx_medium"));
        Assert(Directory.Exists(modelDir), "OCR model directory should exist");

        using var coordinator = new OcrCoordinator();
        var config = new OcrConfig
        {
            CpuThreads = 1,
            ConfidenceThreshold = 0.55f
        };

        coordinator.InitializeAsync(modelDir, config).GetAwaiter().GetResult();
        coordinator.InitializeAsync(modelDir, config).GetAwaiter().GetResult();

        const int width = 64;
        const int height = 32;
        var pixels = Enumerable.Repeat((byte)255, width * height * 4).ToArray();
        var image = new NormalizedImage(width, height, width * 4, pixels, "reload-test");
        var result = coordinator.RecognizeAsync(image).GetAwaiter().GetResult();

        Assert(result != null, "OCR should still work after model reload");
        Assert(result!.ImageWidth == width && result.ImageHeight == height,
            "OCR result dimensions are invalid after reload");
    }

    private static void VerifyRegionCrop()
    {
        const int width = 4;
        const int height = 3;
        const int stride = width * 4;
        var pixels = new byte[stride * height];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int offset = y * stride + x * 4;
            pixels[offset] = (byte)(y * 10 + x);
            pixels[offset + 3] = 255;
        }

        var source = new NormalizedImage(width, height, stride, pixels, "crop-test");
        var crop = CaptureService.CaptureRegion(source, new Rectangle(1, 1, 2, 2));

        Assert(crop != null, "Crop should be created");
        Assert(crop!.Width == 2 && crop.Height == 2, "Crop dimensions should be 2x2");
        Assert(crop.Stride == 8 && crop.BgraBytes.Length == 16, "Crop buffer dimensions are invalid");
        Assert(crop.BgraBytes[0] == 11 && crop.BgraBytes[4] == 12, "First crop row is incorrect");
        Assert(crop.BgraBytes[8] == 21 && crop.BgraBytes[12] == 22, "Second crop row is incorrect");
        Assert(crop.Preview != null, "Crop preview should be available");

        var clipped = CaptureService.CaptureRegion(source, new Rectangle(3, 2, 4, 4));
        Assert(clipped?.Width == 1 && clipped.Height == 1, "Out-of-bounds crop should be clipped");
    }

    private static void RenderMainWindow()
    {
        var app = new LightOCR.App.App();
        app.InitializeComponent();
        app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        var window = CreateMainWindow();
        window.Show();
        window.UpdateLayout();
        SaveWindowRender(window, "main-window-render.png");
        window.Close();

        var wideWindow = CreateMainWindow();
        var widePixels = new byte[1600 * 300 * 4];
        for (int i = 0; i < widePixels.Length; i += 4)
        {
            widePixels[i] = 92;
            widePixels[i + 1] = 112;
            widePixels[i + 2] = 104;
            widePixels[i + 3] = 255;
        }
        var widePreview = BitmapSource.Create(
            1600, 300, 96, 96, System.Windows.Media.PixelFormats.Bgra32,
            null, widePixels, 1600 * 4);
        widePreview.Freeze();
        if (wideWindow.DataContext is MainViewModel vm)
        {
            vm.CurrentImage = new NormalizedImage(
                1600, 300, 1600 * 4, widePixels, "超宽横图 1600 × 300", widePreview);
            vm.ImagePreview = widePreview;
            vm.OcrResultText = "横图预览保持在左侧容器内，右侧识别文本区域仍然完整可用。";
            vm.HasResult = true;
        }
        wideWindow.Show();
        wideWindow.UpdateLayout();
        SaveWindowRender(wideWindow, "main-window-wide-render.png");
        wideWindow.Close();
    }

    private static MainWindow CreateMainWindow()
    {
        return new MainWindow(
            new ImageInputService(),
            new OcrCoordinator(),
            new ClipboardService(),
            () => Task.CompletedTask)
        {
            Width = 1100,
            Height = 720,
            ShowInTaskbar = false,
            WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000
        };
    }

    private static void SaveWindowRender(MainWindow window, string fileName)
    {
        var bitmap = new RenderTargetBitmap(
            (int)window.ActualWidth, (int)window.ActualHeight,
            96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(window);

        var output = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", fileName));
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(output))
            encoder.Save(stream);

        Console.WriteLine($"Rendered UI: {output}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
