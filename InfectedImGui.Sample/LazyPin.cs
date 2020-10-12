using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace InfectedImGui.Sample
{
    internal unsafe static class LazyPin
    {
        private static Dictionary<string, IntPtr> Utf16Cache = new();
        private static Dictionary<string, IntPtr> Utf8Cache = new();

        public static char* PinnedUtf16(string s)
        {
            if (Utf16Cache.TryGetValue(s, out IntPtr ret))
            { return (char*)ret; }

            char[] charArray = GC.AllocateUninitializedArray<char>(s.Length + 1, pinned: true);

            s.AsSpan().CopyTo(charArray);
            charArray[charArray.Length - 1] = '\0';

            fixed (char* charPointer = charArray)
            {
                Utf16Cache.Add(s, (IntPtr)charPointer);
                return charPointer;
            }
        }

        public static byte* PinnedUtf8(string s)
        {
            if (Utf8Cache.TryGetValue(s, out IntPtr ret))
            { return (byte*)ret; }

            int byteCount = Encoding.UTF8.GetByteCount(s);
            byte[] byteArray = GC.AllocateUninitializedArray<byte>(byteCount + 1, pinned: true);

            int bytesWritten = Encoding.UTF8.GetBytes(s.AsSpan(), byteArray.AsSpan());
            Debug.Assert(byteCount == bytesWritten);
            byteArray[byteArray.Length - 1] = 0;

            fixed (byte* bytePointer = byteArray)
            {
                Utf8Cache.Add(s, (IntPtr)bytePointer);
                return bytePointer;
            }
        }
    }
}
