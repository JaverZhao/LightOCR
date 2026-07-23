using Serilog;
using System.Runtime.InteropServices;
using System.Windows;

namespace LightOCR.App.Services;

public class ClipboardService
{
    private static readonly int[] RetryDelaysMs = { 50, 100, 200 };

    public async Task<bool> CopyTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        for (int i = 0; i < RetryDelaysMs.Length; i++)
        {
            try
            {
                return await SetClipboardWin32(text);
            }
            catch (Exception ex) when (i < RetryDelaysMs.Length - 1)
            {
                Log.Warning(ex, "Clipboard retry {I}/{Max}", i + 1, RetryDelaysMs.Length);
                await Task.Delay(RetryDelaysMs[i]);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Clipboard failed");
                return false;
            }
        }
        return false;
    }

    private static Task<bool> SetClipboardWin32(string text)
    {
        var tcs = new TaskCompletionSource<bool>();
        var thread = new Thread(() =>
        {
            try
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    tcs.TrySetResult(false);
                    return;
                }
                try
                {
                    EmptyClipboard();
                    var hGlobal = Marshal.StringToHGlobalUni(text);
                    if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(hGlobal);
                        tcs.TrySetResult(false);
                        return;
                    }
                    tcs.TrySetResult(true);
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    private const uint CF_UNICODETEXT = 13;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
}
