using System.Runtime.InteropServices;
using LONG = System.Int32;

namespace Ares.Platform.Windows.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public LONG Left;
        public LONG Top;
        public LONG Right;
        public LONG Bottom;
    }
}
