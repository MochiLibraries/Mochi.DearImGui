using System.Runtime.InteropServices;
using HBRUSH = System.IntPtr;
using HCURSOR = System.IntPtr;
using HICON = System.IntPtr;
using HINSTANCE = System.IntPtr;
using UINT = System.UInt32;
using HWND = System.IntPtr;
using LPARAM = System.IntPtr;
using LRESULT = System.IntPtr;
using WPARAM = System.IntPtr;

namespace Ares.Platform.Windows.Interop
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct WNDCLASSEXW
    {
        public UINT StructSize;
        public ClassStyles Style;
        public delegate* unmanaged[Stdcall]<HWND, MessageId, WPARAM, LPARAM, LRESULT> WindowProcedure;
        public int ExtraClassBytes;
        public int ExtraWindowBytes;
        public HINSTANCE Instance;
        public HICON Icon;
        public HCURSOR Cursor;
        public HBRUSH Background;
        public char* MenuName;
        public char* ClassName;
        public HICON SmallIcon;

        public const int DLGWINDOWEXTRA = 30;
    }
}
