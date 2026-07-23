using LightOCR.App.Services;
using LightOCR.App.ViewModels;
using System.Windows;

namespace LightOCR.App.Views;

public partial class ResultWindow : Window
{
    public ResultWindow(ClipboardService clipboard)
    {
        InitializeComponent();
        DataContext = new ResultViewModel(clipboard);
        ViewModel.CloseAction = Close;
    }

    public ResultViewModel ViewModel => (ResultViewModel)DataContext;
}
