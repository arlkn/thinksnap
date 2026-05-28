using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Thinksnap.Core.Hotkeys;

namespace Thinksnap.App.Interop;

public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;

    private static int nextId;

    private readonly HwndSource source;
    private readonly Action callback;
    private readonly int id;
    private bool disposed;

    public GlobalHotkey(IntPtr windowHandle, HotkeyGesture gesture, Action callback)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentNullException.ThrowIfNull(callback);

        source = HwndSource.FromHwnd(windowHandle)
            ?? throw new InvalidOperationException("Window source is not available.");
        this.callback = callback;
        id = Interlocked.Increment(ref nextId);

        if (!RegisterHotKey(windowHandle, id, gesture.WindowsModifierFlags, gesture.VirtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        source.AddHook(WndProc);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        source.RemoveHook(WndProc);
        UnregisterHotKey(source.Handle, id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == id)
        {
            callback();
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
