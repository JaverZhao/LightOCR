using System.Runtime.InteropServices;

namespace LightOCR.App.Interop.Win32;

internal static partial class Kernel32
{
    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll")]
    public static extern IntPtr LoadLibrary(string lpFileName);
}
