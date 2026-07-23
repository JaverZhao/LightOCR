using LightOCR.App.Models;
using LightOCR.App.Services;
using System.Windows;
using System.Windows.Input;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace LightOCR.App.Views;

public partial class CaptureOverlayWindow : Window
{
    private readonly CaptureSessionService _session;
    private System.Windows.Point _startPoint;
    private bool _isDragging;
    private System.Windows.Rect _currentRect;
    private readonly System.Drawing.Rectangle _screenBounds;
    private readonly System.Drawing.Rectangle _virtualBounds;
    private readonly double _scaleFactor;

    public CaptureOverlayWindow(CaptureSessionService session,
        System.Drawing.Rectangle screenBounds,
        System.Drawing.Rectangle virtualBounds,
        double scaleFactor)
    {
        InitializeComponent();

        _session = session;
        _screenBounds = screenBounds;
        _virtualBounds = virtualBounds;
        _scaleFactor = scaleFactor;

        Left = screenBounds.X / scaleFactor;
        Top = screenBounds.Y / scaleFactor;
        Width = screenBounds.Width / scaleFactor;
        Height = screenBounds.Height / scaleFactor;

        DimmingRect.Width = Width;
        DimmingRect.Height = Height;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        KeyDown += OnKeyDown;
        Loaded += (_, _) => { Activate(); Focus(); };
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Right)
        {
            _session.CancelCapture();
            Close();
            return;
        }
        _startPoint = e.GetPosition(this);
        _isDragging = true;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (!_isDragging) return;

        var current = e.GetPosition(this);
        double x = Math.Min(_startPoint.X, current.X);
        double y = Math.Min(_startPoint.Y, current.Y);
        double w = Math.Abs(current.X - _startPoint.X);
        double h = Math.Abs(current.Y - _startPoint.Y);

        _currentRect = new Rect(x, y, w, h);
        SelectionRect.Visibility = Visibility.Visible;
        SelectionRect.Margin = new Thickness(x, y, 0, 0);
        SelectionRect.Width = w;
        SelectionRect.Height = h;

        SizeText.Visibility = Visibility.Visible;
        SizeText.Margin = new Thickness(x, y - 22, 0, 0);
        SizeText.Text = $"{w:F0} × {h:F0}";
    }

    private async void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();

        var physRect = new System.Drawing.Rectangle(
            _screenBounds.X - _virtualBounds.X + (int)Math.Round(_currentRect.X * _scaleFactor),
            _screenBounds.Y - _virtualBounds.Y + (int)Math.Round(_currentRect.Y * _scaleFactor),
            (int)Math.Round(_currentRect.Width * _scaleFactor),
            (int)Math.Round(_currentRect.Height * _scaleFactor));

        _session.UpdateSelection(physRect);

        var region = await _session.CompleteSelectionAsync();
        if (region != null && IsVisible) Close();
    }

    private void OnKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _session.CancelCapture();
            Close();
        }
    }
}
