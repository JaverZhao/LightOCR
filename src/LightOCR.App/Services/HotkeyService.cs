using LightOCR.App.Interop.Win32;
using Serilog;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace LightOCR.App.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 1;
    private HwndSource? _source;
    private nint _hwnd;
    private bool _registered;
    private uint _registeredModifiers;
    private uint _registeredKey;

    public event Action? HotkeyPressed;

    public void Initialize(Window window)
    {
        Unregister();
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        _source = PresentationSource.FromVisual(window) as HwndSource;
        if (_source == null)
            throw new InvalidOperationException("Cannot get HWND source");

        _source.AddHook(WndProc);
        _hwnd = _source.Handle;
    }

    public bool Register(string modifiers, string key)
    {
        var hadPrevious = _registered;
        var previousModifiers = _registeredModifiers;
        var previousKey = _registeredKey;
        Unregister();

        if (!TryParseHotkey(modifiers, key, out var mod, out var vk))
        {
            Log.Error("Invalid hotkey: {Mod}+{Key}", modifiers, key);
            RestorePreviousHotkey(hadPrevious, previousModifiers, previousKey);
            return false;
        }

        if (!User32.RegisterHotKey(_hwnd, HotkeyId, mod | User32.MOD_NOREPEAT, vk))
        {
            int err = Marshal.GetLastWin32Error();
            Log.Error("RegisterHotKey failed, error={Err}, mod={Mod}, key={Key}", err, mod, key);
            RestorePreviousHotkey(hadPrevious, previousModifiers, previousKey);
            return false;
        }

        _registered = true;
        _registeredModifiers = mod | User32.MOD_NOREPEAT;
        _registeredKey = vk;
        Log.Information("Hotkey registered: {Mod}+{Key}", modifiers, key);
        return true;
    }

    public bool CanRegister(string modifiers, string key)
    {
        var hadPrevious = _registered;
        var previousModifiers = _registeredModifiers;
        var previousKey = _registeredKey;
        Unregister();

        var registeredCandidate = false;
        try
        {
            if (!TryParseHotkey(modifiers, key, out var mod, out var vk))
                return false;

            registeredCandidate = User32.RegisterHotKey(
                _hwnd, HotkeyId, mod | User32.MOD_NOREPEAT, vk);
            if (!registeredCandidate)
            {
                int err = Marshal.GetLastWin32Error();
                Log.Warning("Hotkey availability check failed, error={Err}, mod={Mod}, key={Key}",
                    err, mod, key);
            }

            return registeredCandidate;
        }
        finally
        {
            if (registeredCandidate)
                User32.UnregisterHotKey(_hwnd, HotkeyId);

            RestorePreviousHotkey(hadPrevious, previousModifiers, previousKey);
        }
    }

    public void Unregister()
    {
        if (_registered)
        {
            User32.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == User32.WM_HOTKEY && wParam == (IntPtr)HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static bool TryParseHotkey(string modifiers, string key, out uint mod, out uint vk)
    {
        mod = 0;
        vk = 0;

        foreach (var m in modifiers.Split(
            '+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parsed = m.ToLowerInvariant() switch
            {
                "alt" => User32.MOD_ALT,
                "ctrl" or "control" => User32.MOD_CONTROL,
                "shift" => User32.MOD_SHIFT,
                "win" => User32.MOD_WIN,
                _ => 0
            };
            if (parsed == 0)
                return false;

            mod |= (uint)parsed;
        }

        if (!Enum.TryParse<Key>(key, true, out var parsedKey) || !IsUsableMainKey(parsedKey))
            return false;

        vk = (uint)KeyInterop.VirtualKeyFromKey(parsedKey);
        return vk != 0;
    }

    private static bool IsUsableMainKey(Key key)
    {
        return key is not (Key.None or
            Key.System or Key.ImeProcessed or Key.DeadCharProcessed or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftCtrl or Key.RightCtrl or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin);
    }

    private void RestorePreviousHotkey(bool hadPrevious, uint previousModifiers, uint previousKey)
    {
        if (!hadPrevious)
            return;

        if (User32.RegisterHotKey(_hwnd, HotkeyId, previousModifiers, previousKey))
        {
            _registered = true;
            _registeredModifiers = previousModifiers;
            _registeredKey = previousKey;
            Log.Warning("Restored previous hotkey");
        }
        else
        {
            int err = Marshal.GetLastWin32Error();
            Log.Error("Failed to restore previous hotkey, error={Err}", err);
        }
    }

    public void Dispose()
    {
        Unregister();
        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }
}
