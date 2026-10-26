using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MeaningOverlay;

/// <summary>
/// Registers a global hotkey (Ctrl+Shift+M) using Win32 APIs and fires an event when pressed.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int HOTKEY_ID = 0x4D4F; // arbitrary unique ID
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint VK_M = 0x4D;
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private bool _disposed;

    public event Action? HotkeyPressed;

    public HotkeyManager()
    {
        // We need a hidden HWND to receive WM_HOTKEY messages
        var helper = new HwndSourceParameters("HotkeyReceiver")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0
        };

        _source = new HwndSource(helper);
        _source.AddHook(WndProc);

        var success = RegisterHotKey(_source.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_M);
        if (!success)
        {
            int err = Marshal.GetLastWin32Error();
            // Error 1409 = hotkey already registered — might be another instance
            System.Diagnostics.Debug.WriteLine($"RegisterHotKey failed with error: {err}");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_source != null)
        {
            UnregisterHotKey(_source.Handle, HOTKEY_ID);
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }
}
