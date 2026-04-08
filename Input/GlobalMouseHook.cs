using System.Diagnostics;
using System.Runtime.InteropServices;
using LiteMarkWin.Native;

namespace LiteMarkWin.Input;

internal sealed class GlobalMouseHook : IDisposable
{
    private readonly NativeMethods.HookProc _hookProc;
    private readonly Func<MouseHookEventArgs, bool> _handler;
    private IntPtr _hookHandle;

    public GlobalMouseHook(Func<MouseHookEventArgs, bool> handler)
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
        return NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _hookProc, moduleHandle, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message is NativeMethods.WmMouseMove or NativeMethods.WmNcMouseMove or
                NativeMethods.WmLButtonDown or NativeMethods.WmLButtonUp or NativeMethods.WmLButtonDblClk)
            {
                var mouseData = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
                var args = new MouseHookEventArgs(message, mouseData.Pt);
                if (_handler(args))
                {
                    return new IntPtr(1);
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }
}

internal sealed record MouseHookEventArgs(int Message, Point ScreenPoint);
