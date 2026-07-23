using Serilog;
using System.Runtime.InteropServices;
using System.Text;

namespace LightOCR.App.Interop;

internal sealed class OcrEngine : IDisposable
{
    private SafeOcrHandle? _handle;

    public IntPtr Handle => _handle?.DangerousGetHandle() ?? IntPtr.Zero;

    public void Create(string configJson)
    {
        var error = default(NativeBuffer);

        try
        {
            var configBytes = Encoding.UTF8.GetBytes(configJson);
            var configPtr = Marshal.AllocHGlobal(configBytes.Length + 1);
            try
            {
                Marshal.Copy(configBytes, 0, configPtr, configBytes.Length);
                Marshal.WriteByte(configPtr, configBytes.Length, 0);

                int rc = NativeOcrMethods.lightocr_create(configPtr, out var rawHandle, out error);
                if (rc != 0)
                {
                    var errMsg = NativeOcrMethods.ReadNativeBuffer(error);
                    throw new InvalidOperationException(
                        $"OCR init failed ({rc}): {errMsg}");
                }

                _handle = SafeOcrHandle.Create(rawHandle);
            }
            finally
            {
                Marshal.FreeHGlobal(configPtr);
            }
        }
        finally
        {
            if (error.Data != IntPtr.Zero)
                NativeOcrMethods.lightocr_free_buffer(error);
        }

        int apiVersion = NativeOcrMethods.lightocr_get_api_version();
        Log.Information("OCR engine created, API version: {Version}", apiVersion);
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }
}
