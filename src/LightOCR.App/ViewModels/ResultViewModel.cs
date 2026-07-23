using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightOCR.App.Models;
using LightOCR.App.Services;
using System.Windows.Media.Imaging;

namespace LightOCR.App.ViewModels;

public partial class ResultViewModel : ObservableObject
{
    private readonly ClipboardService _clipboard;
    public Action? CloseAction { get; set; }

    [ObservableProperty]
    private OcrDocumentResult? _result;

    [ObservableProperty]
    private string _editableText = string.Empty;

    [ObservableProperty]
    private string _elapsedText = string.Empty;

    [ObservableProperty]
    private BitmapSource? _imagePreview;

    public ResultViewModel(ClipboardService clipboard)
    {
        _clipboard = clipboard;
    }

    [RelayCommand]
    private async Task CopyAll()
    {
        if (!string.IsNullOrEmpty(EditableText))
            await _clipboard.CopyTextAsync(EditableText);
    }

    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
    }
}
