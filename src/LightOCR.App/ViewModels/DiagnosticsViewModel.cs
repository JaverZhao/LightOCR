using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace LightOCR.App.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    public string AppVersion { get; }
    public string NativeApiVersion { get; }
    public string OcrEngineState { get; }
    public string OsVersion { get; }
    public string CpuArchitecture { get; }
    public string DpiMode { get; }
    public string Screens { get; }
    public string DetModel { get; }
    public string RecModel { get; }
    public string ModelDir { get; }
    public string OnnxRuntimeVersion { get; }
    public string NativeLibPath { get; }

    public DiagnosticsViewModel()
    {
        var asm = typeof(DiagnosticsViewModel).Assembly.GetName();
        AppVersion = $"应用版本：{asm.Version}";

        int apiVer = 0;
        try
        {
            apiVer = Interop.NativeOcrMethods.lightocr_get_api_version();
        }
        catch { }
        NativeApiVersion = $"原生 API 版本：{apiVer}";
        OcrEngineState = "OCR 引擎状态：就绪";

        OsVersion = $"操作系统：{RuntimeInformation.OSDescription}";
        CpuArchitecture = $"CPU 架构：{RuntimeInformation.ProcessArchitecture}";
        DpiMode = "DPI 模式：PerMonitorV2";

        var screenCount = System.Windows.Forms.Screen.AllScreens.Length;
        Screens = $"显示器：{screenCount} 个";

        var baseDir = AppContext.BaseDirectory;
        var modelDir = Path.Combine(baseDir, "models", "onnx");
        if (!Directory.Exists(modelDir))
            modelDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\models\onnx"));

        ModelDir = $"模型目录：{modelDir}";
        DetModel = $"检测模型：PP-OCRv6_small_det (ONNX)";
        RecModel = $"识别模型：PP-OCRv6_small_rec (ONNX)";

        OnnxRuntimeVersion = "推理引擎：ONNX Runtime 1.21.0";

        var nativePath = Path.Combine(baseDir, "LightOCR.Native.dll");
        NativeLibPath = $"原生 DLL：{(File.Exists(nativePath) ? "存在" : "未找到")}";
    }

    [RelayCommand]
    private void OpenLogDir()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LightOCR", "logs");
        if (Directory.Exists(logDir))
        {
            Process.Start("explorer.exe", logDir);
        }
    }
}
