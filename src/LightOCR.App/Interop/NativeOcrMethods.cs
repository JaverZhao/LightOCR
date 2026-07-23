using System.Runtime.InteropServices;
using System.Text;

namespace LightOCR.App.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeBuffer
{
    public IntPtr Data;
    public nint Length;
}

internal static partial class NativeOcrMethods
{
    private const string DllName = "LightOCR.Native.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lightocr_get_api_version();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lightocr_create(
        IntPtr configJsonUtf8,
        out IntPtr handle,
        out NativeBuffer error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lightocr_recognize_bgra(
        IntPtr handle,
        IntPtr pixels,
        int width,
        int height,
        int stride,
        out NativeBuffer result,
        out NativeBuffer error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int lightocr_destroy(
        IntPtr handle,
        out NativeBuffer error);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void lightocr_free_buffer(
        NativeBuffer buffer);

    internal static string ReadNativeBuffer(NativeBuffer buf)
    {
        if (buf.Data == IntPtr.Zero || buf.Length == 0)
            return string.Empty;
        var bytes = new byte[buf.Length];
        Marshal.Copy(buf.Data, bytes, 0, bytes.Length);
        return Encoding.UTF8.GetString(bytes);
    }

    internal static void Free(NativeBuffer buf)
    {
        if (buf.Data != IntPtr.Zero)
            lightocr_free_buffer(buf);
    }
}
