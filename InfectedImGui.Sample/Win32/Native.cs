using System.Runtime.InteropServices;
using HINSTANCE = System.IntPtr;
using HMENU = System.IntPtr;
using HWND = System.IntPtr;
using IntPtr = System.IntPtr;
using LPARAM = System.IntPtr;
using LRESULT = System.IntPtr;
using WPARAM = System.IntPtr;

namespace Ares.Platform.Windows.Interop
{
    public static unsafe class Native
    {
        private const string User32 = "user32.dll";
        private const string Kernel32 = "Kernel32.dll";
        private const string Ntdll = "ntdll.dll";
        private const string Gdi32 = "Gdi32.dll";

        [DllImport(User32, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern bool PeekMessageW(out MSG message, HWND windowHandle, uint messageFilterMinimum, uint messageFilterMaximum, PeekMessageFlags flags);

        [DllImport(User32)]
        public static extern bool TranslateMessage(in MSG message);

        [DllImport(User32)]
        public static extern LRESULT DispatchMessage(in MSG message);

        [DllImport(User32, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        public static extern ATOM RegisterClassExW(in WNDCLASSEXW classDescription);

        [DllImport(User32, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        public static extern unsafe bool UnregisterClassW(char* className, HINSTANCE instance);

        [DllImport(Kernel32, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        public static extern HINSTANCE GetModuleHandleW(IntPtr name);

        [DllImport(User32, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        public static extern HWND CreateWindowExW(ExtendedWindowStyles extendedStyle, char* className, char* windowName, WindowStyles style, int x, int y, int width, int height, HWND parentWindow, HMENU menu, HINSTANCE instance, IntPtr userParameter);

        [DllImport(User32, SetLastError = true)]
        public static extern bool DestroyWindow(HWND windowHandle);

        [DllImport(User32, CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern LRESULT DefWindowProcW(HWND windowHandle, MessageId messageId, WPARAM wParam, LPARAM lParam);

        [DllImport(User32, SetLastError = true)]
        public static extern bool SetWindowPos(HWND windowHandle, HWND insertAfter, int x, int y, int width, int height, SetWindowPosFlags flags);

        [DllImport(User32)]
        public static extern bool ShowWindow(HWND windowHandle, ShowWindowMode mode);

        [DllImport(User32)]
        public static extern bool UpdateWindow(HWND windowHandle);

        [DllImport(User32)]
        public static extern void PostQuitMessage(int exitCode);
    }
}
