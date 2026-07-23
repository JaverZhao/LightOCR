using System.Runtime.InteropServices;
using System.Text;

class Program
{
    [DllImport("LightOCR.Native.dll")]
    static extern int lightocr_get_api_version();
    [DllImport("LightOCR.Native.dll")]
    static extern int lightocr_create(IntPtr cfg, out IntPtr h, out Buf e);
    [DllImport("LightOCR.Native.dll")]
    static extern int lightocr_recognize_bgra(IntPtr h, IntPtr px, int w,
        int hh, int s, out Buf r, out Buf e);
    [DllImport("LightOCR.Native.dll")]
    static extern int lightocr_destroy(IntPtr h, out Buf e);
    [DllImport("LightOCR.Native.dll")]
    static extern void lightocr_free_buffer(Buf b);
    [StructLayout(LayoutKind.Sequential)]
    struct Buf { public IntPtr Data; public nint Length; }

    static string R(Buf b)
    {
        if (b.Data == IntPtr.Zero || b.Length == 0) return "";
        byte[] d = new byte[b.Length];
        Marshal.Copy(b.Data, d, 0, d.Length);
        return Encoding.UTF8.GetString(d);
    }

    static int Main()
    {
        Console.WriteLine($"API v{lightocr_get_api_version()}");
var cfg = new { modelDir = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\models\onnx")),
    detModelOnnx = "det/inference.onnx",
    recModelOnnx = "rec/inference.onnx",
    dictPath = "ppocrv6_dict.txt" };

        IntPtr cp = Marshal.StringToCoTaskMemUTF8(
            System.Text.Json.JsonSerializer.Serialize(cfg));
        int rc = lightocr_create(cp, out IntPtr h, out Buf eb);
        Marshal.FreeCoTaskMem(cp);
        if (rc != 0) { Console.Error.WriteLine($"E: {R(eb)}"); return 1; }
        Console.WriteLine("Engine created");

        int w = 640, hh = 480;
        byte[] px = new byte[w * hh * 4];
        for (int i = 0; i < px.Length; i++) px[i] = 255;
        var g = GCHandle.Alloc(px, GCHandleType.Pinned);
        rc = lightocr_recognize_bgra(h, g.AddrOfPinnedObject(), w, hh, w * 4,
            out Buf r2, out Buf e2);
        g.Free();
        if (rc == 0) { Console.WriteLine($"OCR OK: {R(r2)}"); lightocr_free_buffer(r2); }
        else Console.Error.WriteLine($"OCR FAIL: {R(e2)}");
        lightocr_destroy(h, out _);
        Console.WriteLine("Done");
        return rc;
    }
}
