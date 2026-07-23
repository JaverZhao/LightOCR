using LightOCR.App.ViewModels;
using System.Windows;

namespace LightOCR.App.Views;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow()
    {
        InitializeComponent();
        DataContext = new DiagnosticsViewModel();
    }
}
