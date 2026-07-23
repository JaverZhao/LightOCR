using Serilog;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace LightOCR.App.Infrastructure;

public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Global\\LightOCR.Desktop.Singleton";
    private const string PipeName = "LightOCR.Desktop.Ipc";
    private readonly Mutex _mutex;
    private bool _isFirstInstance;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(true, MutexName, out _isFirstInstance);
    }

    public bool IsFirstInstance => _isFirstInstance;

    public bool TryActivateFirstInstance(string[] args)
    {
        if (_isFirstInstance) return true;

        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            pipe.Connect(1000);
            var payload = string.Join("\n", args);
            var data = Encoding.UTF8.GetBytes(payload);
            pipe.Write(data, 0, data.Length);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "IPC send failed");
            return false;
        }
    }

    public void StartIpcServer(Action<string[]> onMessage)
    {
        if (!_isFirstInstance) return;

        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await pipe.WaitForConnectionAsync();
                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    var payload = await reader.ReadToEndAsync();
                    var args = payload.Split('\n');
                    App.Current.Dispatcher.Invoke(() => onMessage(args));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "IPC server error");
                }
            }
        });
    }

    public void Dispose()
    {
        if (_isFirstInstance)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
