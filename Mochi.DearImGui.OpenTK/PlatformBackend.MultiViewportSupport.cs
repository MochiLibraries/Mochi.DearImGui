using Mochi.DearImGui.Infrastructure;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Mochi.DearImGui.OpenTK;

unsafe partial class PlatformBackend
{
    // ImGui_ImplGlfw_ViewportData
    private struct ViewportData
    {
        public Window* Window;
        public bool WindowOwned;
        public int IgnoreWindowPosEventFrame;
        public int IgnoreWindowSizeEventFrame;

        public ViewportData()
        {
            Window = null;
            WindowOwned = false;
            IgnoreWindowPosEventFrame = -1;
            IgnoreWindowSizeEventFrame = -1;
        }

        internal static ViewportData* Allocate()
            => (ViewportData*)ImGui.MemAlloc((nuint)sizeof(ViewportData));

        internal static void Free(ViewportData* viewportData)
        {
            Debug.Assert(viewportData->Window is null);
            ImGui.MemFree(viewportData);
        }
    }

    // ImGui_ImplGlfw_InitPlatformInterface
    private void InitPlatformInterface()
    {
        Io->BackendFlags |= ImGuiBackendFlags.PlatformHasViewports; // We can create multi-viewports on the Platform side (optional)

        PlatformIo->Platform_CreateWindow = &CreateWindow;
        PlatformIo->Platform_DestroyWindow = &DestroyWindow;
        PlatformIo->Platform_ShowWindow = &ShowWindow;
        PlatformIo->Platform_SetWindowPos = &SetWindowPos;
        PlatformIo->Platform_GetWindowPos = &GetWindowPos;
        PlatformIo->Platform_SetWindowSize = &SetWindowSize;
        PlatformIo->Platform_GetWindowSize = &GetWindowSize;
        PlatformIo->Platform_SetWindowFocus = &SetWindowFocus;
        PlatformIo->Platform_GetWindowFocus = &GetWindowFocus;
        PlatformIo->Platform_GetWindowMinimized = &GetWindowMinimized;
        PlatformIo->Platform_SetWindowTitle = &SetWindowTitle;
        PlatformIo->Platform_SetWindowAlpha = &SetWindowAlpha;
        // Unused: UpdateWindow
        PlatformIo->Platform_RenderWindow = &RenderWindow;
        PlatformIo->Platform_SwapBuffers = &SwapBuffers;
        // Unused: GetWindowDpiScale
        // Unused: OnChangedViewport
        // Unused: CreateVkSurface

        // Register main window handle (which is owned by the main application, not by us)
        // This is mostly for simplicity and consistency, so that our code (e.g. mouse handling etc.) can use same logic for main and secondary viewports.
        ImGuiViewport* mainViewport = ImGui.GetMainViewport();
        ViewportData* viewportData = ViewportData.Allocate();
        *viewportData = new()
        {
            Window = Window,
            WindowOwned = false
        };
        mainViewport->PlatformUserData = viewportData;
        mainViewport->PlatformHandle = Window;

        // Determine if SetWindowSize needs the macOS workaround
        // (See SetWindowSize for details.)
        if (OperatingSystem.IsMacOS())
        {
            GLFW.GetVersion(out int major, out int minor, out int revision);
            NeedMacOsSetWindowSizeWorkaround = (major * 1000 + minor * 100 + revision * 10) < 3310;
        }
    }

    // ImGui_ImplGlfw_CreateWindow
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void CreateWindow(ImGuiViewport* viewport)
    {
        PlatformBackend backend = GetPlatformBackend();
        ViewportData* viewportData = ViewportData.Allocate();
        viewport->PlatformUserData = viewportData;

        GLFW.WindowHint(WindowHintBool.Visible, false);
        GLFW.WindowHint(WindowHintBool.Focused, false);
        GLFW.WindowHint(WindowHintBool.FocusOnShow, false);
        GLFW.WindowHint(WindowHintBool.Decorated, !viewport->Flags.HasFlag(ImGuiViewportFlags.NoDecoration));
        GLFW.WindowHint(WindowHintBool.Floating, viewport->Flags.HasFlag(ImGuiViewportFlags.TopMost));

        *viewportData = new ViewportData()
        {
            Window = GLFW.CreateWindow((int)viewport->Size.X, (int)viewport->Size.Y, "No Title Yet", null, backend.Window),
            WindowOwned = true,
        };

        viewport->PlatformHandle = viewportData->Window;

        if (OperatingSystem.IsWindows())
        { viewport->PlatformHandleRaw = (void*)GLFW.GetWin32Window(viewportData->Window); }

        GLFW.SetWindowPos(viewportData->Window, (int)viewport->Pos.X, (int)viewport->Pos.Y);

        // Install GLFW callbacks for secondary viewports
        GlfwNative.glfwSetWindowFocusCallback(viewportData->Window, &WindowFocusCallback);
        GlfwNative.glfwSetCursorEnterCallback(viewportData->Window, &CursorEnterCallback);
        GlfwNative.glfwSetCursorPosCallback(viewportData->Window, &CursorPosCallback);
        GlfwNative.glfwSetMouseButtonCallback(viewportData->Window, &MouseButtonCallback);
        GlfwNative.glfwSetScrollCallback(viewportData->Window, &ScrollCallback);
        GlfwNative.glfwSetKeyCallback(viewportData->Window, &KeyCallback);
        GlfwNative.glfwSetCharCallback(viewportData->Window, &CharCallback);
        GlfwNative.glfwSetWindowCloseCallback(viewportData->Window, &WindowCloseCallback);
        GlfwNative.glfwSetWindowPosCallback(viewportData->Window, &WindowPosCallback);
        GlfwNative.glfwSetWindowSizeCallback(viewportData->Window, &WindowSizeCallback);
        GLFW.MakeContextCurrent(viewportData->Window);
        GLFW.SwapInterval(0);
    }

    // ImGui_ImplGlfw_DestroyWindow
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void DestroyWindow(ImGuiViewport* viewport)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        if (viewportData is not null)
        {
            if (viewportData->WindowOwned)
            {
                PlatformBackend backend = GetPlatformBackend();

                // Release any keys that were pressed in the window being destroyed and are still held down,
                // because we will not receive any release events after window is destroyed.
                for (int i = 0; i < backend.KeyOwnerWindows.Length; i++)
                {
                    if (backend.KeyOwnerWindows[i] == viewportData->Window)
                    { KeyCallbackManaged(viewportData->Window, (Keys)i, 0, InputAction.Release, 0); } // Later params are only used for main viewport, on which this function is never called.
                }

                GLFW.DestroyWindow(viewportData->Window);
            }

            viewportData->Window = null;
            ViewportData.Free(viewportData);
        }

        viewport->PlatformUserData = viewport->PlatformHandle = null;
    }

    // ImGui_ImplGlfw_ShowWindow
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void ShowWindow(ImGuiViewport* viewport)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;

        if (OperatingSystem.IsWindows())
        {
            // GLFW hack: Hide icon from task bar
            void* hwnd = viewport->PlatformHandleRaw;
            if (viewport->Flags.HasFlag(ImGuiViewportFlags.NoTaskBarIcon))
            {
                int extendedStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
                extendedStyle &= ~Win32.WS_EX_APPWINDOW;
                extendedStyle |= Win32.WS_EX_TOOLWINDOW;
                Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, extendedStyle);
            }
        }

        GLFW.ShowWindow(viewportData->Window);
    }

    // ImGui_ImplGlfw_SetWindowPos
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowPos(ImGuiViewport* viewport, Vector2 position)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        viewportData->IgnoreWindowPosEventFrame = ImGui.GetFrameCount();
        GLFW.SetWindowPos(viewportData->Window, (int)position.X, (int)position.Y);
    }

    // ImGui_ImplGlfw_GetWindowPos
    private static Vector2 GetWindowPosManaged(ImGuiViewport* viewport)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        GLFW.GetWindowPos(viewportData->Window, out int x, out int y);
        return new Vector2((float)x, (float)y);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static Vector2 GetWindowPos(ImGuiViewport* viewport)
        => GetWindowPosManaged(viewport);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static Vector2* GetWindowPos(Vector2* returnBuffer, ImGuiViewport* viewport)
    {
        *returnBuffer = GetWindowPosManaged(viewport);
        return returnBuffer;
    }

    private static bool NeedMacOsSetWindowSizeWorkaround = false;

    // ImGui_ImplGlfw_SetWindowSize
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowSize(ImGuiViewport* viewport, Vector2 size)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;

        if (NeedMacOsSetWindowSizeWorkaround)
        {
            // Native OS windows are positioned from the bottom-left corner on macOS, whereas on other platforms they are
            // positioned from the upper-left corner. GLFW makes an effort to convert macOS style coordinates, however it
            // doesn't handle it when changing size. We are manually moving the window in order for changes of size to be based
            // on the upper-left corner.
            GLFW.GetWindowPos(viewportData->Window, out int x, out int y);
            GLFW.GetWindowSize(viewportData->Window, out int width, out int height);
            GLFW.SetWindowPos(viewportData->Window, x, y - height + (int)size.Y);
        }

        viewportData->IgnoreWindowSizeEventFrame = ImGui.GetFrameCount();
        GLFW.SetWindowSize(viewportData->Window, (int)size.X, (int)size.Y);
    }

    // ImGui_ImplGlfw_GetWindowSize
    private static Vector2 GetWindowSizeManaged(ImGuiViewport* viewport)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        GLFW.GetWindowSize(viewportData->Window, out int width, out int height);
        return new Vector2((float)width, (float)height);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static Vector2 GetWindowSize(ImGuiViewport* viewport)
        => GetWindowSizeManaged(viewport);

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static Vector2* GetWindowSize(Vector2* returnBuffer, ImGuiViewport* viewport)
    {
        *returnBuffer = GetWindowSizeManaged(viewport);
        return returnBuffer;
    }

    // ImGui_ImplGlfw_SetWindowFocus
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowFocus(ImGuiViewport* viewport)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        GLFW.FocusWindow(viewportData->Window);
    }

    // ImGui_ImplGlfw_GetWindowFocus
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static NativeBoolean GetWindowFocus(ImGuiViewport* viewport)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        return GLFW.GetWindowAttrib(viewportData->Window, WindowAttributeGetBool.Focused);
    }

    // ImGui_ImplGlfw_GetWindowMinimized
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static NativeBoolean GetWindowMinimized(ImGuiViewport* viewport)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        return GLFW.GetWindowAttrib(viewportData->Window, WindowAttributeGetBool.Iconified);
    }

    // ImGui_ImplGlfw_SetWindowTitle
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowTitle(ImGuiViewport* viewport, byte* title)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        GLFW.SetWindowTitleRaw(viewportData->Window, title);
    }

    // ImGui_ImplGlfw_SetWindowAlpha
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SetWindowAlpha(ImGuiViewport* viewport, float alpha)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        GLFW.SetWindowOpacity(viewportData->Window, alpha);
    }

    // ImGui_ImplGlfw_RenderWindow
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void RenderWindow(ImGuiViewport* viewport, void* renderArgument)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        GLFW.MakeContextCurrent(viewportData->Window);
    }

    // ImGui_ImplGlfw_SwapBuffers
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void SwapBuffers(ImGuiViewport* viewport, void* renderArgument)
    {
        ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
        GLFW.MakeContextCurrent(viewportData->Window);
        GLFW.SwapBuffers(viewportData->Window);
    }

    // ImGui_ImplGlfw_WindowCloseCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void WindowCloseCallback(Window* window)
    {
        ImGuiViewport* viewport = ImGui.FindViewportByPlatformHandle(window);
        if (viewport is not null)
        { viewport->PlatformRequestClose = true; }
    }

    // GLFW may dispatch window pos/size events after calling glfwSetWindowPos()/glfwSetWindowSize().
    // However: depending on the platform the callback may be invoked at different time:
    // - on Windows it appears to be called within the glfwSetWindowPos()/glfwSetWindowSize() call
    // - on Linux it is queued and invoked during glfwPollEvents()
    // Because the event doesn't always fire on glfwSetWindowXXX() we use a frame counter tag to only
    // ignore recent glfwSetWindowXXX() calls.

    // ImGui_ImplGlfw_WindowPosCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void WindowPosCallback(Window* window, int x, int y)
    {
        ImGuiViewport* viewport = ImGui.FindViewportByPlatformHandle(window);
        if (viewport is not null)
        {
            ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
            if (viewportData is not null && ImGui.GetFrameCount() < viewportData->IgnoreWindowPosEventFrame + 1)
            { return; }

            viewport->PlatformRequestMove = true;
        }
    }

    // ImGui_ImplGlfw_WindowSizeCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void WindowSizeCallback(Window* window, int width, int height)
    {
        ImGuiViewport* viewport = ImGui.FindViewportByPlatformHandle(window);
        if (viewport is not null)
        {
            ViewportData* viewportData = (ViewportData*)viewport->PlatformUserData;
            if (viewportData is not null && ImGui.GetFrameCount() < viewportData->IgnoreWindowSizeEventFrame + 1)
            { return; }

            viewport->PlatformRequestResize = true;
        }
    }
}
