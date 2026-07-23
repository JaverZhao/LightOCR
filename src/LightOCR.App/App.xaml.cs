using LightOCR.App.Services;
using Serilog;
using System.Windows;

namespace LightOCR.App;

public partial class App : System.Windows.Application
{
    private AppLifetimeService? _lifetime;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal((Exception)args.ExceptionObject, "Unhandled exception");
            Log.CloseAndFlush();
        };

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Dispatcher unhandled exception");
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        try
        {
            _lifetime = new AppLifetimeService();
            await _lifetime.StartAsync(e.Args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application startup failed");
            Log.CloseAndFlush();
            System.Windows.MessageBox.Show($"启动失败: {ex.Message}", "LightOCR",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_lifetime != null)
            await _lifetime.StopAsync();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
