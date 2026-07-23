using LightOCR.App.Services;
using LightOCR.App.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace LightOCR.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(
        ImageInputService imageInput,
        OcrCoordinator ocrCoordinator,
        ClipboardService clipboard,
        Func<Task> screenshotAction,
        SettingsService? settingsService = null,
        Action? settingsAction = null)
    {
        InitializeComponent();
        DataContext = new MainViewModel(
            imageInput, ocrCoordinator, clipboard, screenshotAction, settingsService, settingsAction);
    }

    private void OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm ||
            e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        await vm.LoadAndOcrAsync(files[0]);
    }
}
