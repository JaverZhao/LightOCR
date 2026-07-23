using Serilog;
using System.Drawing;
using System.Windows.Forms;

namespace LightOCR.App.Services;

public sealed class TrayService : IDisposable
{
    private NotifyIcon? _trayIcon;
    private bool _disposed;

    public event Action? ScreenshotRequested;
    public event Action? OpenImageRequested;
    public event Action? ShowWindowRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public void Initialize()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "LightOCR",
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("截图识别", null, (_, _) => ScreenshotRequested?.Invoke());
        menu.Items.Add("打开图片", null, (_, _) => OpenImageRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("显示主窗口", null, (_, _) => ShowWindowRequested?.Invoke());
        menu.Items.Add("设置", null, (_, _) => SettingsRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowWindowRequested?.Invoke();

        Log.Information("Tray icon initialized");
    }

    public void ShowNotification(string title, string text, int durationMs = 3000)
    {
        _trayIcon?.ShowBalloonTip(durationMs, title, text, ToolTipIcon.Info);
    }

    private static Icon CreateDefaultIcon()
    {
        using var stream = typeof(TrayService).Assembly
            .GetManifestResourceStream("LightOCR.App.Assets.icon.ICON.ico");
        if (stream != null)
            return new Icon(stream);
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.DodgerBlue);
        g.FillRectangle(brush, 2, 2, 12, 12);
        using var font = new Font("Segoe UI", 8, FontStyle.Bold);
        using var whiteBrush = new SolidBrush(Color.White);
        g.DrawString("O", font, whiteBrush, 3, 3);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}
