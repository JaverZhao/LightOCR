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
    private readonly SettingsService _settingsService;
    private readonly Func<Task>? _screenshotAction;
    private readonly Action? _settingsAction;

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
        Func<Task>? screenshotAction = null,
        SettingsService? settingsService = null,
        Action? settingsAction = null)
    {
        _imageInput = imageInput;
        _ocrCoordinator = ocrCoordinator;
        _clipboard = clipboard;
        _screenshotAction = screenshotAction;
        _settingsService = settingsService ?? new SettingsService();
        _settingsAction = settingsAction;
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
        if (_settingsAction != null)
        {
            _settingsAction();
            return;
        }

        foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
            if (w is Views.SettingsWindow) { w.Activate(); return; }
        StatusText = "设置服务不可用";
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
        var modelDir = Path.Combine(AppContext.BaseDirectory, "models", "onnx_medium");
        if (!Directory.Exists(modelDir))
            modelDir = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, @"..\..\..\..\..\..\models\onnx_medium"));
        var settings = _settingsService.Load<Settings>();
        await _ocrCoordinator.InitializeAsync(modelDir, settings.Ocr);
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

                var settings = _settingsService.Load<Settings>();
                if (settings.Ocr.AutoCopy && result.FullText.Length > 0)
                {
                    var copied = await _clipboard.CopyTextAsync(result.FullText);
                    if (copied)
                        StatusText += "，已复制";
                }

                if (settings.Ocr.ShowResultWindow)
                {
                    var resultWindow = new Views.ResultWindow(_clipboard);
                    resultWindow.ViewModel.Result = result;
                    resultWindow.ViewModel.EditableText = result.FullText;
                    resultWindow.ViewModel.ElapsedText =
                        $"识别耗时：{result.Elapsed.TotalMilliseconds:F0} ms";
                    resultWindow.ViewModel.ImagePreview = image.Preview;
                    resultWindow.Show();
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
