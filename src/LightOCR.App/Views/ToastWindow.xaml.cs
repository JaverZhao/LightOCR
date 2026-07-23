using System.Windows;
using System.Windows.Media;

namespace LightOCR.App.Views;

public partial class ToastWindow : Window
{
    private readonly System.Timers.Timer _timer;

    public ToastWindow()
    {
        InitializeComponent();
        _timer = new System.Timers.Timer(2500);
        _timer.Elapsed += (_, _) =>
        {
            _timer.Stop();
            Dispatcher.Invoke(() =>
            {
                Close();
            });
        };
        MouseDown += (_, _) => Close();
    }

    public void Show(string text, VerticalAlignment align = VerticalAlignment.Bottom)
    {
        ToastText.Text = text;

        Left = SystemParameters.WorkArea.Right - ActualWidth - 20;
        Top = align == VerticalAlignment.Bottom
            ? SystemParameters.WorkArea.Bottom - ActualHeight - 20
            : SystemParameters.WorkArea.Top + 20;

        Show();
        _timer.Start();
    }
}
