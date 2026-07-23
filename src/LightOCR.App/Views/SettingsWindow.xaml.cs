using LightOCR.App.Services;
using LightOCR.App.ViewModels;
using System.Windows;
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
        if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftAlt) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightAlt))
            modifiers.Add("Alt");
        if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl))
            modifiers.Add("Ctrl");
        if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
            modifiers.Add("Shift");
        if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LWin) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RWin))
            modifiers.Add("Win");

        var key = e.Key;
        if (key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
            key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
            key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
            key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
            return;

        if (modifiers.Count > 0)
        {
            _vm.SetHotkey(modifiers.ToArray(), key.ToString());
            PreviewKeyDown -= OnHotkeyPreviewKeyDown;
        }
    }
}
