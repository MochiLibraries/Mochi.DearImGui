using System.Runtime.InteropServices;

namespace Mochi.DearImGui.OpenTK;

internal unsafe static class Win32
{
    [DllImport("USER32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetWindowLongW", SetLastError = true, ExactSpelling = true)]
    public static extern int GetWindowLong(void* hWnd, int nIndex);

    [DllImport("USER32.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "SetWindowLongW", SetLastError = true, ExactSpelling = true)]
    public static extern int SetWindowLong(void* hWnd, int nIndex, int dwNewLong);

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_APPWINDOW = 0x40000;
    public const int WS_EX_TOOLWINDOW = 0x80;
}
