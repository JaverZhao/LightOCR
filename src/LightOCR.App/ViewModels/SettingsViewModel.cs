using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightOCR.App.Models;
using LightOCR.App.Services;
using Serilog;
using System.Windows;

namespace LightOCR.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly HotkeyService _hotkey;
    private Settings _original = Settings.Default;

    [ObservableProperty]
    private string _hotkeyText = "Alt + Shift + O";

    [ObservableProperty]
    private bool _autoCopy = true;

    [ObservableProperty]
    private bool _showResultWindow;

    [ObservableProperty]
    private float _confidenceThreshold = 0.55f;

    [ObservableProperty]
    private int _cpuThreads = 4;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimized = true;

    [ObservableProperty]
    private bool _saveHistory;

    [ObservableProperty]
    private string _hotkeyStatus = "";

    public SettingsViewModel(SettingsService settingsService, HotkeyService hotkey)
    {
        _settingsService = settingsService;
        _hotkey = hotkey;

        LoadFromSettings(_settingsService.Load<Settings>());
    }

    private void LoadFromSettings(Settings s)
    {
        _original = s;
        HotkeyText = string.Join(" + ", s.Hotkey.Modifiers) + " + " + s.Hotkey.Key;
        AutoCopy = s.Ocr.AutoCopy;
        ShowResultWindow = s.Ocr.ShowResultWindow;
        ConfidenceThreshold = s.Ocr.ConfidenceThreshold;
        CpuThreads = s.Ocr.CpuThreads;
        StartWithWindows = s.Application.StartWithWindows;
        StartMinimized = s.Application.StartMinimized;
        SaveHistory = s.Application.SaveHistory;
    }

    public void StartHotkeyCapture()
    {
        HotkeyStatus = "按下新的快捷键组合...";
    }

    public void SetHotkey(string[] modifiers, string key)
    {
        var mods = string.Join(" + ", modifiers);
        HotkeyText = $"{mods} + {key}";

        // Validate: try register the new hotkey
        var tempService = new HotkeyService();
        var win = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None,
            ShowInTaskbar = false };
        win.Show();
        win.Hide();
        tempService.Initialize(win);

        if (tempService.Register(string.Join("+", modifiers), key))
        {
            tempService.Dispose();
            HotkeyStatus = "快捷键可用";
        }
        else
        {
            tempService.Dispose();
            HotkeyStatus = "快捷键冲突，请选择其他组合";
        }
    }

    [RelayCommand]
    private void Apply()
    {
        var s = new Settings
        {
            SchemaVersion = 1,
            Hotkey = new HotkeyConfig
            {
                Modifiers = HotkeyText.Split(new[] { " + " }, StringSplitOptions.None)[..^1],
                Key = HotkeyText.Split(new[] { " + " }, StringSplitOptions.None)[^1]
            },
            Ocr = new OcrConfig
            {
                AutoCopy = AutoCopy,
                ShowResultWindow = ShowResultWindow,
                ConfidenceThreshold = ConfidenceThreshold,
                CpuThreads = CpuThreads,
                PreloadModel = true
            },
            Application = new AppConfig
            {
                StartWithWindows = StartWithWindows,
                StartMinimized = StartMinimized,
                PortableMode = _original.Application.PortableMode,
                SaveHistory = SaveHistory
            }
        };

        _settingsService.Save(s);

        // Apply hotkey changes
        var mods = string.Join("+", s.Hotkey.Modifiers);
        var win = new Window { Width = 0, Height = 0, WindowStyle = WindowStyle.None,
            ShowInTaskbar = false };
        win.Show();
        win.Hide();
        _hotkey.Initialize(win);
        _hotkey.Register(mods, s.Hotkey.Key);

        // Apply startup
        if (s.Application.StartWithWindows)
            StartupService.Enable();
        else
            StartupService.Disable();

        HotkeyStatus = "已保存";
        Log.Information("Settings saved and applied");
    }

    [RelayCommand]
    private void Reset()
    {
        LoadFromSettings(Settings.Default);
        HotkeyStatus = "已恢复默认值";
    }

    [RelayCommand]
    private void Close()
    {
        foreach (Window w in App.Current.Windows)
        {
            if (w is Views.SettingsWindow)
            {
                w.Close();
                break;
            }
        }
    }
}
