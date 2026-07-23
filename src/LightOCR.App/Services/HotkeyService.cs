using LightOCR.App.Interop.Win32;
using Serilog;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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

        uint mod = 0;
        foreach (var m in modifiers.Split('+', StringSplitOptions.TrimEntries))
        {
            mod |= (uint)(m.ToLower() switch
            {
                "alt" => User32.MOD_ALT,
                "ctrl" or "control" => User32.MOD_CONTROL,
                "shift" => User32.MOD_SHIFT,
                "win" => User32.MOD_WIN,
                _ => 0
            });
        }

        uint vk = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(
            (System.Windows.Input.Key)Enum.Parse(typeof(System.Windows.Input.Key), key, true));

        if (!User32.RegisterHotKey(_hwnd, HotkeyId, mod | User32.MOD_NOREPEAT, vk))
        {
            int err = Marshal.GetLastWin32Error();
            Log.Error("RegisterHotKey failed, error={Err}, mod={Mod}, key={Key}", err, mod, key);
            if (hadPrevious &&
                User32.RegisterHotKey(_hwnd, HotkeyId, previousModifiers, previousKey))
            {
                _registered = true;
                _registeredModifiers = previousModifiers;
                _registeredKey = previousKey;
                Log.Warning("Restored previous hotkey after registration failure");
            }
            return false;
        }

        _registered = true;
        _registeredModifiers = mod | User32.MOD_NOREPEAT;
        _registeredKey = vk;
        Log.Information("Hotkey registered: {Mod}+{Key}", modifiers, key);
        return true;
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
