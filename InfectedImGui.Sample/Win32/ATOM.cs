using System;

namespace Ares.Platform.Windows.Interop
{
    public struct ATOM
    {
        private UInt16 value;

        private ATOM(UInt16 value)
            => this.value = value;

        public static explicit operator UInt16(ATOM atom) => atom.value;
        public static explicit operator ATOM(UInt16 value) => new ATOM(value);

        public unsafe static implicit operator char* (ATOM atom) => (char*)atom.value;
        public unsafe static explicit operator ATOM(char* value) => checked(new ATOM((UInt16)value));

        public unsafe static implicit operator IntPtr(ATOM atom) => (IntPtr)(char*)atom;
        public unsafe static explicit operator ATOM(IntPtr value) => (ATOM)(char*)value;

        public bool IsValid => value != 0;

        public static readonly ATOM Zero = new ATOM(0);
    }
}
