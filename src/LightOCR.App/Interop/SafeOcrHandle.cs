using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace LightOCR.App.Interop;

internal sealed class SafeOcrHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeOcrHandle() : base(true) { }

    public static SafeOcrHandle Create(IntPtr handle)
    {
        var safe = new SafeOcrHandle();
        safe.SetHandle(handle);
        return safe;
    }

    protected override bool ReleaseHandle()
    {
        var error = default(NativeBuffer);
        try
        {
            int rc = NativeOcrMethods.lightocr_destroy(handle, out error);
            return rc == 0;
        }
        finally
        {
            if (error.Data != IntPtr.Zero)
                NativeOcrMethods.lightocr_free_buffer(error);
        }
    }
}
