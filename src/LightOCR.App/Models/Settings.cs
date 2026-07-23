namespace LightOCR.App.Models;

public sealed record HotkeyConfig
{
    public string[] Modifiers { get; set; } = { "Alt", "Shift" };
    public string Key { get; set; } = "O";
    public static readonly HotkeyConfig Default = new();
}

public sealed record OcrConfig
{
    public bool AutoCopy { get; set; } = true;
    public bool ShowResultWindow { get; set; } = false;
    public float ConfidenceThreshold { get; set; } = 0.55f;
    public int CpuThreads { get; set; } = 4;
    public bool PreloadModel { get; set; } = true;
    public static readonly OcrConfig Default = new();
}

public sealed record AppConfig
{
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool PortableMode { get; set; } = false;
    public bool SaveHistory { get; set; } = false;
    public static readonly AppConfig Default = new();
}

public sealed record Settings
{
    public int SchemaVersion { get; set; } = 1;
    public HotkeyConfig Hotkey { get; set; } = HotkeyConfig.Default;
    public OcrConfig Ocr { get; set; } = OcrConfig.Default;
    public AppConfig Application { get; set; } = AppConfig.Default;

    public static readonly Settings Default = new();
}
