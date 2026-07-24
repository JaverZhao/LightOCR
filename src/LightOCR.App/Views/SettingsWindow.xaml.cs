using LightOCR.App.Services;
using LightOCR.App.ViewModels;
using System.Windows;
using System.Windows.Input;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace LightOCR.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(
        SettingsService settingsService,
        HotkeyService hotkey,
        Func<LightOCR.App.Models.Settings, Task>? settingsApplied = null)
    {
        InitializeComponent();
        _vm = new SettingsViewModel(settingsService, hotkey, settingsApplied);
        DataContext = _vm;
    }

    private void HotkeyBox_OnPreviewMouseDown(object sender, WpfMouseButtonEventArgs e)
    {
        _vm.StartHotkeyCapture();
        this.PreviewKeyDown += OnHotkeyPreviewKeyDown;
        e.Handled = true;
    }

    private void OnHotkeyPreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        e.Handled = true;
        var modifiers = new List<string>();
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            modifiers.Add("Alt");
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            modifiers.Add("Ctrl");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            modifiers.Add("Shift");
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            modifiers.Add("Win");

        var key = NormalizeCapturedKey(e.Key, e.SystemKey, e.ImeProcessedKey, e.DeadCharProcessedKey);
        if (IsModifierKey(key))
            return;

        if (modifiers.Count > 0)
        {
            _vm.SetHotkey(modifiers.ToArray(), key.ToString());
            PreviewKeyDown -= OnHotkeyPreviewKeyDown;
        }
    }

    internal static Key NormalizeCapturedKey(
        Key key,
        Key systemKey,
        Key imeProcessedKey,
        Key deadCharProcessedKey)
    {
        if (key == Key.System)
            return systemKey;
        if (key == Key.ImeProcessed)
            return imeProcessedKey;
        if (key == Key.DeadCharProcessed)
            return deadCharProcessedKey;
        return key;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftAlt or Key.RightAlt or
            Key.LeftCtrl or Key.RightCtrl or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin;
    }
}
