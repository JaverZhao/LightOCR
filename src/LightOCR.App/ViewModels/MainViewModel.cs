using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightOCR.App.Models;
using LightOCR.App.Services;
using Serilog;
using System.IO;
using System.Windows.Media.Imaging;

namespace LightOCR.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ImageInputService _imageInput;
    private readonly OcrCoordinator _ocrCoordinator;
    private readonly ClipboardService _clipboard;
    private readonly Func<Task>? _screenshotAction;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private NormalizedImage? _currentImage;

    [ObservableProperty]
    private BitmapSource? _imagePreview;

    [ObservableProperty]
    private string _ocrResultText = string.Empty;

    [ObservableProperty]
    private string _ocrElapsed = string.Empty;

    [ObservableProperty]
    private bool _hasResult;

    public MainViewModel(
        ImageInputService imageInput,
        OcrCoordinator ocrCoordinator,
        ClipboardService clipboard,
        Func<Task>? screenshotAction = null)
    {
        _imageInput = imageInput;
        _ocrCoordinator = ocrCoordinator;
        _clipboard = clipboard;
        _screenshotAction = screenshotAction;
    }

    [RelayCommand]
    private async Task OpenImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件|*.*",
            Title = "选择图片"
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadAndOcrAsync(dialog.FileName);
        }
    }

    [RelayCommand]
    private async Task PasteImage()
    {
        if (Clipboard.ContainsImage())
        {
            var image = await _imageInput.FromClipboardAsync();
            if (image != null)
                await OcrImageAsync(image);
        }
    }

    [RelayCommand]
    private async Task Screenshot()
    {
        if (_screenshotAction == null)
        {
            StatusText = "截图服务不可用";
            return;
        }

        StatusText = "拖动鼠标框选识别区域，Esc 取消";
        await _screenshotAction();
    }

    [RelayCommand]
    private async Task CopyResult()
    {
        if (!string.IsNullOrEmpty(OcrResultText))
        {
            var success = await _clipboard.CopyTextAsync(OcrResultText);
            StatusText = success ? "已复制" : "复制失败";
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
            if (w is Views.SettingsWindow) { w.Activate(); return; }
        var settingsService = new SettingsService();
        var hotkey = new HotkeyService();
        var sw = new Views.SettingsWindow(settingsService, hotkey);
        sw.Show();
    }

    [ObservableProperty]
    private string _modelState = "等待识别";

    public void OnOcrReady()
    {
        ModelState = "模型就绪";
    }

    [RelayCommand]
    private async Task ReloadModel()
    {
        ModelState = "重新加载中...";
        var modelDir = Path.Combine(AppContext.BaseDirectory, "models", "onnx");
        if (!Directory.Exists(modelDir))
            modelDir = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, @"..\..\..\..\..\models\onnx"));
        await _ocrCoordinator.InitializeAsync(modelDir);
        ModelState = "模型已就绪";
        StatusText = "模型已重新加载";
    }

    [RelayCommand]
    private void OpenDiagnostics()
    {
        var dw = new Views.DiagnosticsWindow();
        dw.Show();
    }

    [RelayCommand]
    private async Task ReRecognize()
    {
        if (CurrentImage != null)
            await OcrImageAsync(CurrentImage);
    }

    public async Task LoadAndOcrAsync(string filePath)
    {
        var image = await _imageInput.FromFileAsync(filePath);
        if (image != null)
            await OcrImageAsync(image);
    }

    public Task RecognizeCapturedImageAsync(NormalizedImage image) => OcrImageAsync(image);

    private async Task OcrImageAsync(NormalizedImage image)
    {
        if (IsBusy) return;

        IsBusy = true;
        StatusText = "识别中...";

        try
        {
            CurrentImage = image;
            ImagePreview = image.Preview;

            var result = await _ocrCoordinator.RecognizeAsync(image);
            if (result != null)
            {
                OcrResultText = result.FullText;
                OcrElapsed = $"{result.Elapsed.TotalMilliseconds:F0} ms";
                HasResult = true;
                StatusText = $"识别完成 — {result.Lines.Count} 行, {result.FullText.Length} 字符";

                if (result.FullText.Length > 0)
                {
                    var copied = await _clipboard.CopyTextAsync(result.FullText);
                    if (copied)
                        StatusText += "，已复制";
                }
            }
            else
            {
                StatusText = "识别失败";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OCR failed");
            StatusText = $"错误: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
