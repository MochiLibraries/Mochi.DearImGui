using System.Runtime.InteropServices;
using DWORD = System.UInt32;
using HWND = System.IntPtr;
using LPARAM = System.IntPtr;
using WPARAM = System.IntPtr;

namespace Ares.Platform.Windows.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public HWND WindowHandle;
        public MessageId MessageId;
        public WPARAM WParam;
        public LPARAM LParam;
        public DWORD Time;
        public int CursorX;
        public int CursorY;
    }
}
