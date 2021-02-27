// <auto-generated>
// This file was automatically generated by Biohazrd and should not be modified by hand!
// </auto-generated>
#nullable enable
using InfectedImGui.Infrastructure;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace InfectedImGui.Internal
{
    [StructLayout(LayoutKind.Explicit, Size = 36)]
    public unsafe partial struct ImGuiMenuColumns
    {
        [FieldOffset(0)] public float Spacing;

        [FieldOffset(4)] public float Width;

        [FieldOffset(8)] public float NextWidth;

        [FieldOffset(12)] public ConstantArray_float_3 Pos;

        [FieldOffset(24)] public ConstantArray_float_3 NextWidths;

        [DllImport("InfectedImGui.Native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "__InlineHelper44", ExactSpelling = true)]
        private static extern void Constructor_PInvoke(ImGuiMenuColumns* @this);

        [DebuggerStepThrough, DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Constructor()
        {
            fixed (ImGuiMenuColumns* @this = &this)
            { Constructor_PInvoke(@this); }
        }

        [DllImport("InfectedImGui.Native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?Update@ImGuiMenuColumns@@QEAAXHM_N@Z", ExactSpelling = true)]
        private static extern void Update_PInvoke(ImGuiMenuColumns* @this, int count, float spacing, [MarshalAs(UnmanagedType.I1)] bool clear);

        [DebuggerStepThrough, DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Update(int count, float spacing, bool clear)
        {
            fixed (ImGuiMenuColumns* @this = &this)
            { Update_PInvoke(@this, count, spacing, clear); }
        }

        [DllImport("InfectedImGui.Native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?DeclColumns@ImGuiMenuColumns@@QEAAMMMM@Z", ExactSpelling = true)]
        private static extern float DeclColumns_PInvoke(ImGuiMenuColumns* @this, float w0, float w1, float w2);

        [DebuggerStepThrough, DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe float DeclColumns(float w0, float w1, float w2)
        {
            fixed (ImGuiMenuColumns* @this = &this)
            { return DeclColumns_PInvoke(@this, w0, w1, w2); }
        }

        [DllImport("InfectedImGui.Native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "?CalcExtraSpace@ImGuiMenuColumns@@QEBAMM@Z", ExactSpelling = true)]
        private static extern float CalcExtraSpace_PInvoke(ImGuiMenuColumns* @this, float avail_w);

        [DebuggerStepThrough, DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe float CalcExtraSpace(float avail_w)
        {
            fixed (ImGuiMenuColumns* @this = &this)
            { return CalcExtraSpace_PInvoke(@this, avail_w); }
        }
    }
}