using LightOCR.App.Infrastructure;
using LightOCR.App.Interop;
using LightOCR.App.Models;
using LightOCR.App.Views;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace LightOCR.App.Services;

public sealed class AppLifetimeService : IDisposable
{
    private readonly SingleInstanceGuard _guard = new();
    private readonly TrayService _tray = new();
    private readonly SettingsService _settingsService = new();
    private readonly OcrCoordinator _ocr = new();
    private readonly ClipboardService _clipboard = new();
    private readonly ImageInputService _imageInput = new();
    private readonly HotkeyService _hotkey = new();
    private readonly CaptureSessionService _captureSession = new();
    private Settings _settings = Settings.Default;
    private ToastWindow? _currentToast;

    public AppLifetimeService()
    {
        _captureSession.CaptureCompleted += OnCaptureCompleted;
        _captureSession.CaptureCancelled += OnCaptureCancelled;
    }

    public TrayService Tray => _tray;
    public OcrCoordinator Ocr => _ocr;
    public ClipboardService Clipboard => _clipboard;
    public ImageInputService ImageInput => _imageInput;
    public HotkeyService Hotkey => _hotkey;
    public CaptureSessionService CaptureSession => _captureSession;
    public Settings Settings => _settings;

    public async Task StartAsync(string[] args)
    {
        var sw = Stopwatch.StartNew();
        InitializeLogging();
        Log.Information("LightOCR starting");

        if (!_guard.IsFirstInstance)
        {
            _guard.TryActivateFirstInstance(args);
            Environment.Exit(0);
        }
        Log.Debug("Single instance OK");

        _guard.StartIpcServer(OnIpcMessage);
        Log.Debug("IPC server started");

        _settings = _settingsService.Load<Settings>();
        Log.Debug("Settings loaded");

        _tray.Initialize();
        _tray.ScreenshotRequested += () => _ = BeginScreenshot();
        _tray.OpenImageRequested += () => _ = OnOpenImage();
        _tray.ShowWindowRequested += OnShowWindow;
        _tray.SettingsRequested += OnShowSettings;
        _tray.ExitRequested += OnExit;

        _ = InitializeOcrAsync();

        RegisterGlobalHotkey();

        if (!_settings.Application.StartMinimized)
            OnShowWindow();

        sw.Stop();
        Log.Information("Startup in {Ms}ms", sw.ElapsedMilliseconds);
    }

    private void RegisterGlobalHotkey()
    {
        var win = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None,
            ShowInTaskbar = false };
        win.Show(); win.Hide();
        _hotkey.Initialize(win);
        _hotkey.HotkeyPressed += () => _ = BeginScreenshot();

        var hk = _settings.Hotkey;
        if (!_hotkey.Register(string.Join("+", hk.Modifiers), hk.Key))
            Log.Warning("Hotkey registration failed");
    }

    private async Task InitializeOcrAsync()
    {
        try
        {
            var modelDir = ResolveModelDir();
            if (modelDir != null)
            {
                await _ocr.InitializeAsync(modelDir);
                Log.Information("OCR engine ready from {Dir}", modelDir);

                // Notify main window
                App.Current.Dispatcher.Invoke(() =>
                {
                    if (_mainWindow?.DataContext is ViewModels.MainViewModel vm)
                        vm.OnOcrReady();
                });
            }
            else
            {
                Log.Warning("OCR model directory not found");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OCR init failed");
        }
    }

    private static string? ResolveModelDir()
    {
        // Priority 1: next to the exe (packaged build)
        var d1 = Path.Combine(AppContext.BaseDirectory, "models", "onnx");
        if (Directory.Exists(d1)) return d1;

        // Priority 2: dev directory relative to output (bin/Debug/net8.0-windows/win-x64/ -> repo root)
        var d2 = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            @"..\..\..\..\..\..\models\onnx"));
        if (Directory.Exists(d2)) return d2;

        // Priority 3: flat model names (packaged naming)
        var d3 = Path.Combine(AppContext.BaseDirectory, "models", "onnx");
        if (File.Exists(Path.Combine(d3, "det_inference.onnx"))) return d3;

        return null;
    }

    private async Task BeginScreenshot()
    {
        if (_captureSession.State == CaptureState.Selecting ||
            _captureSession.State == CaptureState.Recognizing)
        {
            ShowToast("已有截图任务进行中");
            return;
        }

        foreach (Window w in App.Current.Windows)
            if (w.IsVisible)
                w.Hide();
        await Task.Delay(100);

        if (!_captureSession.BeginCapture())
        {
            ShowToast("截图失败");
            OnShowWindow();
            return;
        }

        var virtualBounds = ScreenTopologyService.VirtualScreenBounds();
        foreach (var screen in ScreenTopologyService.GetAllScreens())
        {
            var overlay = new CaptureOverlayWindow(
                _captureSession, screen.Bounds, virtualBounds, screen.ScaleFactor);
            overlay.Show();
        }
    }

    private async void OnCaptureCompleted(NormalizedImage image)
    {
        CloseCaptureOverlays();
        OnShowWindow();

        if (_mainWindow?.DataContext is ViewModels.MainViewModel vm)
            await vm.RecognizeCapturedImageAsync(image);
    }

    private void OnCaptureCancelled()
    {
        CloseCaptureOverlays();
        OnShowWindow();
        ShowToast("已取消截图");
    }

    private static void CloseCaptureOverlays()
    {
        foreach (Window window in App.Current.Windows.OfType<CaptureOverlayWindow>().ToArray())
            window.Close();
    }

    public void ShowToast(string text)
    {
        try { _currentToast?.Close(); } catch { }
        _currentToast = new ToastWindow();
        _currentToast.Show(text);
    }

    private void OnIpcMessage(string[] args)
    {
        if (args.Length > 0 && File.Exists(args[0]))
            _ = OpenImageAsync(args[0]);
        else
            OnShowWindow();
    }

    private async Task OnOpenImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件|*.*"
        };
        if (dialog.ShowDialog() == true)
            await OpenImageAsync(dialog.FileName);
    }

    private async Task OpenImageAsync(string path)
    {
        var image = await _imageInput.FromFileAsync(path);
        if (image == null) return;
        OnShowWindow();
        var result = await _ocr.RecognizeAsync(image);
        if (result != null)
            ShowToast($"识别完成: {result.Lines.Count} 行");
    }

    private Views.MainWindow? _mainWindow;

    private void OnShowWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.Activate();
            return;
        }

        Log.Debug("Creating main window");
        try
        {
            _mainWindow = new Views.MainWindow(_imageInput, _ocr, _clipboard, BeginScreenshot);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
            _mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create main window");
        }
    }

    private void OnShowSettings()
    {
        foreach (Window w in App.Current.Windows)
            if (w is SettingsWindow) { w.Activate(); return; }

        var sw = new SettingsWindow(_settingsService, _hotkey);
        sw.Show();
    }

    private void OnExit()
    {
        App.Current.Shutdown();
    }

    public async Task StopAsync()
    {
        _hotkey.Dispose();
        _ocr.Dispose();
        _tray.Dispose();
        await Task.CompletedTask;
    }

    private static void InitializeLogging()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LightOCR", "logs");
        Directory.CreateDirectory(appData);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(appData, "lightocr-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
    }

    public void Dispose()
    {
        _captureSession.CaptureCompleted -= OnCaptureCompleted;
        _captureSession.CaptureCancelled -= OnCaptureCancelled;
        _hotkey.Dispose(); _ocr.Dispose(); _tray.Dispose(); _guard.Dispose();
    }
}
