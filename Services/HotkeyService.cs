using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ScreenGrab.Services;

public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9000;

    // Modifier keys
    private const uint MOD_CTRL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    // Virtual key for 'T'
    private const uint VK_T = 0x54;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private IntPtr _windowHandle;
    private HwndSource? _source;

    public event Action? HotkeyPressed;

    public void Register(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _source = HwndSource.FromHwnd(windowHandle);
        _source?.AddHook(HwndHook);

        if (!RegisterHotKey(windowHandle, HOTKEY_ID, MOD_CTRL | MOD_SHIFT | MOD_NOREPEAT, VK_T))
        {
            throw new InvalidOperationException(
                "Failed to register hotkey Ctrl+Shift+T. It may be in use by another application.");
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
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
        _source?.RemoveHook(HwndHook);
        UnregisterHotKey(_windowHandle, HOTKEY_ID);
    }
}
