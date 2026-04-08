using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LiteMarkWin.Native;

namespace LiteMarkWin.Input;

internal sealed class GlobalKeyboardHook : IDisposable
{
    private readonly NativeMethods.HookProc _hookProc;
    private readonly Func<KeyStateChangedEventArgs, bool> _handler;
    private IntPtr _hookHandle;

    public GlobalKeyboardHook(Func<KeyStateChangedEventArgs, bool> handler)
    {
        _handler = handler;
        _hookProc = HookCallback;
        _hookHandle = InstallHook();
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }

    private IntPtr InstallHook()
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = NativeMethods.GetModuleHandle(module?.ModuleName);
        return NativeMethods.SetWindowsHookEx(NativeMethods.WhKeyboardLl, _hookProc, moduleHandle, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown or NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp)
            {
                var keyboardData = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
                var isDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
                var key = Normalize((Keys)keyboardData.VkCode);
                if (_handler(new KeyStateChangedEventArgs(key, isDown)))
                {
                    return new IntPtr(1);
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static Keys Normalize(Keys key) => key switch
    {
        Keys.LControlKey or Keys.RControlKey => Keys.ControlKey,
        Keys.LMenu or Keys.RMenu => Keys.Menu,
        Keys.LShiftKey or Keys.RShiftKey => Keys.ShiftKey,
        _ => key
    };
}

internal sealed record KeyStateChangedEventArgs(Keys Key, bool IsDown);
