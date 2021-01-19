// This file was automatically generated by Biohazrd and should not be modified by hand!
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Explicit, Size = 232)]
public unsafe partial struct ImGuiPlatformIO
{
    [FieldOffset(0)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void> Platform_CreateWindow;

    [FieldOffset(8)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void> Platform_DestroyWindow;

    [FieldOffset(16)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void> Platform_ShowWindow;

    [FieldOffset(24)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, ImVec2, void> Platform_SetWindowPos;

    [FieldOffset(32)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, ImVec2> Platform_GetWindowPos;

    [FieldOffset(40)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, ImVec2, void> Platform_SetWindowSize;

    [FieldOffset(48)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, ImVec2> Platform_GetWindowSize;

    [FieldOffset(56)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void> Platform_SetWindowFocus;

    [FieldOffset(64)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, NativeBoolean> Platform_GetWindowFocus;

    [FieldOffset(72)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, NativeBoolean> Platform_GetWindowMinimized;

    [FieldOffset(80)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, byte*, void> Platform_SetWindowTitle;

    [FieldOffset(88)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, float, void> Platform_SetWindowAlpha;

    [FieldOffset(96)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void> Platform_UpdateWindow;

    [FieldOffset(104)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void*, void> Platform_RenderWindow;

    [FieldOffset(112)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void*, void> Platform_SwapBuffers;

    [FieldOffset(120)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, float> Platform_GetWindowDpiScale;

    [FieldOffset(128)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void> Platform_OnChangedViewport;

    [FieldOffset(136)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, ImVec2, void> Platform_SetImeInputPos;

    [FieldOffset(144)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, ulong, void*, ulong*, int> Platform_CreateVkSurface;

    [FieldOffset(152)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void> Renderer_CreateWindow;

    [FieldOffset(160)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void> Renderer_DestroyWindow;

    [FieldOffset(168)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, ImVec2, void> Renderer_SetWindowSize;

    [FieldOffset(176)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void*, void> Renderer_RenderWindow;

    [FieldOffset(184)] public delegate* unmanaged[Cdecl]<ImGuiViewport*, void*, void> Renderer_SwapBuffers;

    [FieldOffset(192)] public ImVector<ImGuiPlatformMonitor> Monitors;

    [FieldOffset(208)] public ImGuiViewport* MainViewport;

    [FieldOffset(216)] public ImVector<Pointer<ImGuiViewport>> Viewports;

    [DllImport("InfectedImGui.Native.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "??0ImGuiPlatformIO@@QEAA@XZ", ExactSpelling = true)]
    private static extern void Constructor_PInvoke(ImGuiPlatformIO* @this);

    public unsafe void Constructor()
    {
        fixed (ImGuiPlatformIO* @this = &this)
        { Constructor_PInvoke(@this); }
    }
}
