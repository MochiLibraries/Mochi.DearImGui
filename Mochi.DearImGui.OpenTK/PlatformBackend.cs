// Platform backend for OpenTK
// Based on imgui_impl_glfw.cpp
// https://github.com/ocornut/imgui/blob/704ab1114aa54858b690711554cf3312fbbcc3fc/backends/imgui_impl_glfw.cpp

// Implemented features:
//  [x] Platform: Clipboard support.
//  [x] Platform: Gamepad support. Enable with 'io.ConfigFlags |= ImGuiConfigFlags_NavEnableGamepad'.
//  [x] Platform: Mouse cursor shape and visibility. Disable with 'io.ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange' (note: the resizing cursors requires GLFW 3.4+).
//  [x] Platform: Keyboard arrays indexed using GLFW_KEY_* codes, e.g. ImGui::IsKeyPressed(GLFW_KEY_SPACE).
//  [ ] Platform: Multi-viewport support (multiple windows). Enable with 'io.ConfigFlags |= ImGuiConfigFlags_ViewportsEnable'.

// Issues:
//  [ ] Platform: Multi-viewport support: ParentViewportID not honored, and so io.ConfigViewportsNoDefaultParent has no effect (minor).

using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Mochi.DearImGui.OpenTK;

public unsafe sealed partial class PlatformBackend : IDisposable
{
    private Internal.ImGuiContext* Context;
    private GCHandle ThisHandle;
    private readonly ImGuiIO* Io;
    private readonly ImGuiPlatformIO* PlatformIo;

    private readonly Window* Window;
    private double Time;
    private Window* MouseWindow;
    private readonly bool[] MouseJustPressed = new bool[(int)ImGuiMouseButton.COUNT];
    private readonly Cursor*[] MouseCursors;
    private readonly Window*[] KeyOwnerWindows = new Window*[512];
    private readonly bool InstalledCallbacks;
    private bool WantUpdateMonitors;

    // Chain GLFW callbacks: our callbacks will call the user's previously installed callbacks, if any.
    private readonly delegate* unmanaged[Cdecl]<Window*, int, void> PrevUserCallbackWindowFocus;
    private readonly delegate* unmanaged[Cdecl]<Window*, int, void> PrevUserCallbackCursorEnter;
    private readonly delegate* unmanaged[Cdecl]<Window*, MouseButton, InputAction, KeyModifiers, void> PrevUserCallbackMousebutton;
    private readonly delegate* unmanaged[Cdecl]<Window*, double, double, void> PrevUserCallbackScroll;
    private readonly delegate* unmanaged[Cdecl]<Window*, Keys, int, InputAction, KeyModifiers, void> PrevUserCallbackKey;
    private readonly delegate* unmanaged[Cdecl]<Window*, uint, void> PrevUserCallbackChar;
    private readonly delegate* unmanaged[Cdecl]<Monitor*, ConnectedState, void> PrevUserCallbackMonitor;

    public PlatformBackend(NativeWindow window, bool installCallbacks)
        : this(window.WindowPtr, installCallbacks)
    { }

    //ImGui_ImplGlfw_InitForOpenGL / ImGui_ImplGlfw_Init
    public PlatformBackend(Window* window, bool installCallbacks)
    {
        Io = ImGui.GetIO();
        if (Io->BackendPlatformUserData != null)
        { throw new InvalidOperationException("A platform backend has already been initialized for the current Dear ImGui context!"); }

        PlatformIo = ImGui.GetPlatformIO();

        // These are hard-coded in the native backend, assert that they're correct right
        Debug.Assert(KeyOwnerWindows.Length == Io->KeysDown.Length);
        Debug.Assert(MouseJustPressed.Length == Io->MouseDown.Length);

        // Unlike the native bindings we have an object associated with each context so we generally don't use the BackendPlatformUserData and instead enforce
        // a 1:1 relationship between backend instances and Dear ImGui contexts. However we still use a GC handle in BackendPlatformUserData for our platform callbacks.
        Context = ImGui.GetCurrentContext();

        ThisHandle = GCHandle.Alloc(this, GCHandleType.Weak);
        Io->BackendPlatformUserData = (void*)GCHandle.ToIntPtr(ThisHandle);

        // Set the backend name
        {
            string name = GetType().FullName ?? nameof(PlatformBackend);
            int nameByteCount = Encoding.UTF8.GetByteCount(name) + 1;
            byte* nameP = (byte*)ImGui.MemAlloc((ulong)nameByteCount);
            Io->BackendPlatformName = nameP;
            Span<byte> nameSpan = new(nameP, nameByteCount);
            int encodedByteCount = Encoding.UTF8.GetBytes(name.AsSpan().Slice(0, nameSpan.Length - 1), nameSpan);
            nameSpan[encodedByteCount] = 0; // Null terminator
        }

        // Setup backend capabilities flags
        Io->BackendFlags |= ImGuiBackendFlags.HasMouseCursors; // We can honor GetMouseCursor() values (optional)
        Io->BackendFlags |= ImGuiBackendFlags.HasSetMousePos; // We can honor io.WantSetMousePos requests (optional, rarely used)

        // This only works in GLFW 3.4+
        // (There's a Windows-specific workaround for GLFW 3.3 in the native backend but we didn't bother porting it.)
#if false
        Io->BackendFlags |= ImGuiBackendFlags.HasMouseHoveredViewport; // We can set io.MouseHoveredViewport correctly (optional, not easy)
#endif

        Window = window;
        Time = 0.0;
        WantUpdateMonitors = true;

        // Keyboard mapping. Dear ImGui will use those indices to peek into the io.KeysDown[] array.
        Io->KeyMap[(int)ImGuiKey.Tab] = (int)Keys.Tab;
        Io->KeyMap[(int)ImGuiKey.LeftArrow] = (int)Keys.Left;
        Io->KeyMap[(int)ImGuiKey.RightArrow] = (int)Keys.Right;
        Io->KeyMap[(int)ImGuiKey.UpArrow] = (int)Keys.Up;
        Io->KeyMap[(int)ImGuiKey.DownArrow] = (int)Keys.Down;
        Io->KeyMap[(int)ImGuiKey.PageUp] = (int)Keys.PageUp;
        Io->KeyMap[(int)ImGuiKey.PageDown] = (int)Keys.PageDown;
        Io->KeyMap[(int)ImGuiKey.Home] = (int)Keys.Home;
        Io->KeyMap[(int)ImGuiKey.End] = (int)Keys.End;
        Io->KeyMap[(int)ImGuiKey.Insert] = (int)Keys.Insert;
        Io->KeyMap[(int)ImGuiKey.Delete] = (int)Keys.Delete;
        Io->KeyMap[(int)ImGuiKey.Backspace] = (int)Keys.Backspace;
        Io->KeyMap[(int)ImGuiKey.Space] = (int)Keys.Space;
        Io->KeyMap[(int)ImGuiKey.Enter] = (int)Keys.Enter;
        Io->KeyMap[(int)ImGuiKey.Escape] = (int)Keys.Escape;
        Io->KeyMap[(int)ImGuiKey.KeyPadEnter] = (int)Keys.KeyPadEnter;
        Io->KeyMap[(int)ImGuiKey.A] = (int)Keys.A;
        Io->KeyMap[(int)ImGuiKey.C] = (int)Keys.C;
        Io->KeyMap[(int)ImGuiKey.V] = (int)Keys.V;
        Io->KeyMap[(int)ImGuiKey.X] = (int)Keys.X;
        Io->KeyMap[(int)ImGuiKey.Y] = (int)Keys.Y;
        Io->KeyMap[(int)ImGuiKey.Z] = (int)Keys.Z;

        // Clipboard callbacks
        Io->SetClipboardTextFn = &SetClipboardText;
        Io->GetClipboardTextFn = &GetClipboardText;
        Io->ClipboardUserData = Window;

        // ImGui_ImplGlfw_SetClipboardText
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static void SetClipboardText(void* userData, byte* text)
            => GLFW.SetClipboardStringRaw((Window*)userData, text);

        // ImGui_ImplGlfw_GetClipboardText
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        static byte* GetClipboardText(void* userData)
            => GLFW.GetClipboardStringRaw((Window*)userData);

        // Create mouse cursors
        // (By design, on X11 cursors are user configurable and some cursors may be missing. When a cursor doesn't exist,
        // GLFW will emit an error which will often be printed by the app, so we temporarily disable error reporting.
        // Missing cursors will return NULL and our _UpdateMouseCursor() function will use the Arrow cursor instead.)
        {
            delegate* unmanaged[Cdecl]<ErrorCode, byte*, void> prevErrorCallback = GlfwNative.glfwSetErrorCallback(null);
            MouseCursors = new Cursor*[(int)ImGuiMouseCursor.COUNT];
            MouseCursors[(int)ImGuiMouseCursor.Arrow] = GLFW.CreateStandardCursor(CursorShape.Arrow);
            MouseCursors[(int)ImGuiMouseCursor.TextInput] = GLFW.CreateStandardCursor(CursorShape.IBeam);
            MouseCursors[(int)ImGuiMouseCursor.ResizeNS] = GLFW.CreateStandardCursor(CursorShape.VResize);
            MouseCursors[(int)ImGuiMouseCursor.ResizeEW] = GLFW.CreateStandardCursor(CursorShape.HResize);
            MouseCursors[(int)ImGuiMouseCursor.Hand] = GLFW.CreateStandardCursor(CursorShape.Hand);
#if false // OpenTK does not use a version of GLFW which exposes these cursors yet
            MouseCursors[(int)ImGuiMouseCursor.ResizeAll] = GLFW.CreateStandardCursor(CursorShape.ResizeAll);
            MouseCursors[(int)ImGuiMouseCursor.ResizeNESW] = GLFW.CreateStandardCursor(CursorShape.ResizeNesw);
            MouseCursors[(int)ImGuiMouseCursor.ResizeNWSE] = GLFW.CreateStandardCursor(CursorShape.ResizeNwse);
            MouseCursors[(int)ImGuiMouseCursor.NotAllowed] = GLFW.CreateStandardCursor(CursorShape.NotAllowed);
#endif
            GlfwNative.glfwSetErrorCallback(prevErrorCallback);
        }

        // Chain GLFW callbacks: our callbacks will call the user's previously installed callbacks, if any.
        if (installCallbacks)
        {
            InstalledCallbacks = true;
            PrevUserCallbackWindowFocus = GlfwNative.glfwSetWindowFocusCallback(window, &WindowFocusCallback);
            PrevUserCallbackCursorEnter = GlfwNative.glfwSetCursorEnterCallback(window, &CursorEnterCallback);
            PrevUserCallbackMousebutton = GlfwNative.glfwSetMouseButtonCallback(window, &MouseButtonCallback);
            PrevUserCallbackScroll = GlfwNative.glfwSetScrollCallback(window, &ScrollCallback);
            PrevUserCallbackKey = GlfwNative.glfwSetKeyCallback(window, &KeyCallback);
            PrevUserCallbackChar = GlfwNative.glfwSetCharCallback(window, &CharCallback);
            PrevUserCallbackMonitor = GlfwNative.glfwSetMonitorCallback(&MonitorCallback);
        }

        // Update monitors the first time
        UpdateMonitors();

        // Our mouse update function expects PlatformHandle to be filled for the main viewport
        {
            ImGuiViewport* mainViewport = ImGui.GetMainViewport();
            mainViewport->PlatformHandle = Window;

            if (OperatingSystem.IsWindows())
            { mainViewport->PlatformHandleRaw = (void*)GLFW.GetWin32Window(Window); }
        }

        // Initialize platform interface
        if (Io->ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
        { InitPlatformInterface(); }
    }

    private void AssertImGuiContext()
    {
        if (Context is null)
        { throw new ObjectDisposedException(typeof(PlatformBackend).FullName); }
        else if (ImGui.GetCurrentContext() != Context)
        { throw new InvalidOperationException("The current Dear ImGui context is not the one associated with this renderer backend!"); }
    }

    private static PlatformBackend GetPlatformBackend()
    {
        IntPtr userData = (IntPtr)ImGui.GetIO()->BackendPlatformUserData;
        if (userData == IntPtr.Zero)
        { throw new InvalidOperationException("The current Dear ImGui context has no associated platform backend."); }

        PlatformBackend backend = (PlatformBackend)GCHandle.FromIntPtr(userData).Target!;
        backend.AssertImGuiContext();
        return backend;
    }

    //ImGui_ImplGlfw_NewFrame
    public void NewFrame()
    {
        AssertImGuiContext();

        // Setup display size (every frame to accommodate for window resizing)
        GLFW.GetWindowSize(Window, out int width, out int height);
        GLFW.GetFramebufferSize(Window, out int frameBufferWidth, out int frameBufferHeight);
        Io->DisplaySize = new((float)width, (float)height);

        if (width > 0 && height > 0)
        { Io->DisplayFramebufferScale = new((float)frameBufferWidth / (float)width, (float)frameBufferHeight / (float)height); }

        // Update monitors if necessary
        if (WantUpdateMonitors)
        { UpdateMonitors(); }

        // Setup time step
        double currentTime = GLFW.GetTime();
        Io->DeltaTime = Time > 0.0 ? (float)(currentTime - Time) : 1f / 60f;
        Time = currentTime;

        // ImGui_ImplGlfw_UpdateMousePosAndButtons
        UpdateMousePositionAndButtons();
        void UpdateMousePositionAndButtons()
        {
            ImGuiIO* io = Io;

            Vector2 previousMousePosition = io->MousePos;
            io->MousePos = new(-float.MaxValue, -float.MaxValue);
            io->MouseHoveredViewport = 0;

            // Update mouse buttons
            // (if a mouse press event came, always pass it as "mouse held this frame", so we don't miss click-release events that are shorter than 1 frame)
            for (int i = 0; i < io->MouseDown.Length; i++)
            {
                io->MouseDown[i] = MouseJustPressed[i] || GLFW.GetMouseButton(Window, (MouseButton)i) != InputAction.Release;
                MouseJustPressed[i] = false;
            }

            for (int i = 0; i < PlatformIo->Viewports.Size; i++)
            {
                ImGuiViewport* viewport = PlatformIo->Viewports[i];
                Window* viewportWindow = (Window*)viewport->PlatformHandle;

                bool focused = OperatingSystem.IsBrowser() || GLFW.GetWindowAttrib(viewportWindow, WindowAttributeGetBool.Focused);
                Window* mouseWindow = MouseWindow == viewportWindow || focused ? viewportWindow : null;

                // Update mouse buttons
                if (focused)
                {
                    for (int j = 0; j < io->MouseDown.Length; j++)
                    { io->MouseDown[j] |= GLFW.GetMouseButton(viewportWindow, (MouseButton)j) != InputAction.Release; }
                }

                // Set OS mouse position from Dear ImGui if requested (rarely used, only when ImGuiConfigFlags_NavEnableSetMousePos is enabled by user)
                // (When multi-viewports are enabled, all Dear ImGui positions are same as OS positions)
                if (io->WantSetMousePos && focused)
                {
                    Vector2 newMousePosition = previousMousePosition - viewport->Pos;
                    GLFW.SetCursorPos(viewportWindow, (double)newMousePosition.X, (double)newMousePosition.Y);
                }

                // Set Dear ImGui mouse position from OS position
                if (mouseWindow != null)
                {
                    GLFW.GetCursorPos(mouseWindow, out double mouseX, out double mouseY);
                    if (io->ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
                    {
                        // Multi-viewport mode: mouse position in OS absolute coordinates (io.MousePos is (0,0) when the mouse is on the upper-left of the primary monitor)
                        GLFW.GetWindowPos(viewportWindow, out int windowX, out int windowY);
                        io->MousePos = new((float)mouseX + windowX, (float)mouseY + windowY);
                    }
                    else
                    {
                        // Single viewport mode: mouse position in client window coordinates (io.MousePos is (0,0) when the mouse is on the upper-left corner of the app window)
                        io->MousePos = new((float)mouseX, (float)mouseY);
                    }
                }

                // (Optional) When using multiple viewports: set io.MouseHoveredViewport to the viewport the OS mouse cursor is hovering.
                // Important: this information is not easy to provide and many high-level windowing library won't be able to provide it correctly, because
                // - This is _ignoring_ viewports with the ImGuiViewportFlags_NoInputs flag (pass-through windows).
                // - This is _regardless_ of whether another viewport is focused or being dragged from.
                // If ImGuiBackendFlags_HasMouseHoveredViewport is not set by the backend, imgui will ignore this field and infer the information by relying on the
                // rectangles and last focused time of every viewports it knows about. It will be unaware of other windows that may be sitting between or over your windows.
                // [GLFW] FIXME: This is currently only correct on Win32. See what we do below with the WM_NCHITTEST, missing an equivalent for other systems.
                // See https://github.com/glfw/glfw/issues/1236 if you want to help in making this a GLFW feature.
#if false // The hack for this on the platform backend side is more trouble than it's worth. Let's just wait for GLFW 3.4.
                if (OperatingSystem.IsWindows())
                {
                    bool windowNoInput = viewport->Flags.HasFlag(ImGuiViewportFlags.NoInputs);
#if false // This needs GLFW 3.4+, OpenTK currently uses 3.3.
                    GLFW.SetWindowAttrib(viewportWindow, WindowAttribute.MousePassthrough, windowNoInput);
#endif
                    if (!windowNoInput && GLFW.GetWindowAttrib(viewportWindow, WindowAttributeGetBool.Hovered))
                    { io->MouseHoveredViewport = viewport->ID; }
                }
#endif
            }
        }

        // ImGui_ImplGlfw_UpdateMouseCursor
        UpdateMouseCursor();
        void UpdateMouseCursor()
        {
            if (Io->ConfigFlags.HasFlag(ImGuiConfigFlags.NoMouseCursorChange) || GLFW.GetInputMode(Window, CursorStateAttribute.Cursor) == CursorModeValue.CursorDisabled)
            { return; }

            ImGuiMouseCursor imGuiCursor = ImGui.GetMouseCursor();
            foreach (ImGuiViewport* viewport in PlatformIo->Viewports.AsSpan())
            {
                Window* viewportWindow = (Window*)viewport->PlatformHandle;
                if (imGuiCursor == ImGuiMouseCursor.None || Io->MouseDrawCursor)
                {
                    // Hide OS mouse cursor if imgui is drawing it or if it wants no cursor
                    GLFW.SetInputMode(viewportWindow, CursorStateAttribute.Cursor, CursorModeValue.CursorHidden);
                }
                else
                {
                    // Show OS mouse cursor
                    Cursor* cursor = MouseCursors[(int)imGuiCursor];
                    if (cursor is null)
                    { cursor = MouseCursors[(int)ImGuiMouseCursor.Arrow]; }
                    GLFW.SetCursor(viewportWindow, cursor);
                    GLFW.SetInputMode(viewportWindow, CursorStateAttribute.Cursor, CursorModeValue.CursorNormal);
                }
            }
        }

        // Update game controllers (if enabled and available)
        // ImGui_ImplGlfw_UpdateGamepads
        UpdateGamepads();
        void UpdateGamepads()
        {
            if (!Io->ConfigFlags.HasFlag(ImGuiConfigFlags.NavEnableGamepad))
            { return; }

            // Update gamepad inputs
            float* axes = GLFW.GetJoystickAxesRaw(0, out int axisCount);
            JoystickInputAction* buttons = GLFW.GetJoystickButtonsRaw(0, out int buttonCount);

            void MapButton(ImGuiNavInput input, int buttonNumber)
            {
                if (buttonNumber < buttonCount && buttons[buttonNumber] == JoystickInputAction.Press)
                { Io->NavInputs[(int)input] = 1f; }
            }

            void MapAnalog(ImGuiNavInput input, int axisNumber, float v0, float v1)
            {
                float v = axisCount > axisNumber ? axes[axisNumber] : v0;

                v = (v - v0) / (v1 - v0);

                if (v > 1f)
                { v = 1f; }

                if (Io->NavInputs[(int)input] < v)
                { Io->NavInputs[(int)input] = v; }
            }

            MapButton(ImGuiNavInput.Activate, 0); // Cross / A
            MapButton(ImGuiNavInput.Cancel, 1); // Circle / B
            MapButton(ImGuiNavInput.Menu, 2); // Square / X
            MapButton(ImGuiNavInput.Input, 3); // Triangle / Y
            MapButton(ImGuiNavInput.DpadLeft, 13); // D-Pad Left
            MapButton(ImGuiNavInput.DpadRight, 11); // D-Pad Right
            MapButton(ImGuiNavInput.DpadUp, 10); // D-Pad Up
            MapButton(ImGuiNavInput.DpadDown, 12); // D-Pad Down
            MapButton(ImGuiNavInput.FocusPrev, 4); // L1 / LB
            MapButton(ImGuiNavInput.FocusNext, 5); // R1 / RB
            MapButton(ImGuiNavInput.TweakSlow, 4); // L1 / LB
            MapButton(ImGuiNavInput.TweakFast, 5); // R1 / RB
            MapAnalog(ImGuiNavInput.LStickLeft, 0, -0.3f, -0.9f);
            MapAnalog(ImGuiNavInput.LStickRight, 0, +0.3f, +0.9f);
            MapAnalog(ImGuiNavInput.LStickUp, 1, +0.3f, +0.9f);
            MapAnalog(ImGuiNavInput.LStickDown, 1, -0.3f, -0.9f);

            if (axisCount > 0 && buttonCount > 0)
            { Io->BackendFlags |= ImGuiBackendFlags.HasGamepad; }
            else
            { Io->BackendFlags &= ~ImGuiBackendFlags.HasGamepad; }
        }
    }

    // ImGui_ImplGlfw_UpdateMonitors
    private void UpdateMonitors()
    {
        Monitor** glfwMonitors = GLFW.GetMonitorsRaw(out int monitorCount);
        PlatformIo->Monitors.resize(monitorCount);

        for (int i = 0; i < monitorCount; i++)
        {
            Monitor* glfwMonitor = glfwMonitors[i];
            ref ImGuiPlatformMonitor monitor = ref PlatformIo->Monitors[i];
            monitor = new ImGuiPlatformMonitor();

            // Position
            {
                GLFW.GetMonitorPos(glfwMonitor, out int x, out int y);
                monitor.MainPos = new((float)x, (float)y);
                monitor.WorkPos = monitor.MainPos;
            }

            // Size
            VideoMode* videoMode = GLFW.GetVideoMode(glfwMonitor);
            monitor.MainSize = new((float)videoMode->Width, (float)videoMode->Height);
            monitor.WorkSize = monitor.MainSize;

            // Work area
            {
                GlfwNative.glfwGetMonitorWorkarea(glfwMonitor, out int x, out int y, out int w, out int h);
                if (w > 0 && h > 0) // Workaround a small GLFW issue reporting zero on monitor changes: https://github.com/glfw/glfw/pull/1761
                {
                    monitor.WorkPos = new((float)x, (float)y);
                    monitor.WorkSize = new((float)w, (float)h);
                }
            }

            // Per-monitor DPI
            GLFW.GetMonitorContentScale(glfwMonitor, out monitor.DpiScale, out _);
        }

        WantUpdateMonitors = false;
    }

    // GLFW callbacks
    // - When calling Init with 'install_callbacks=true': GLFW callbacks will be installed for you. They will call user's previously installed callbacks, if any.
    // - When calling Init with 'install_callbacks=false': GLFW callbacks won't be installed. You will need to call those function yourself from your own GLFW callbacks.

    //ImGui_ImplGlfw_MouseButtonCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void MouseButtonCallback(Window* window, MouseButton button, InputAction action, KeyModifiers modifiers)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackMousebutton is not null && window == backend.Window)
        { backend.PrevUserCallbackMousebutton(window, button, action, modifiers); }

        if (action == InputAction.Press && button >= 0 && (int)button < backend.MouseJustPressed.Length)
        { backend.MouseJustPressed[(int)button] = true; }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void ScrollCallback(Window* window, double xOffset, double yOffset)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackScroll is not null && window == backend.Window)
        { backend.PrevUserCallbackScroll(window, xOffset, yOffset); }

        backend.Io->MouseWheelH += (float)xOffset;
        backend.Io->MouseWheel += (float)yOffset;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void KeyCallback(Window* window, Keys key, int scancode, InputAction action, KeyModifiers modifiers)
        => KeyCallbackManaged(window, key, scancode, action, modifiers);

    private static void KeyCallbackManaged(Window* window, Keys key, int scancode, InputAction action, KeyModifiers modifiers)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackKey is not null && window == backend.Window)
        { backend.PrevUserCallbackKey(window, key, scancode, action, modifiers); }

        ImGuiIO* io = backend.Io;
        if (key >= 0 && (int)key < backend.KeyOwnerWindows.Length)
        {
            if (action == InputAction.Press)
            {
                io->KeysDown[(int)key] = true;
                backend.KeyOwnerWindows[(int)key] = window;
            }
            else if (action == InputAction.Release)
            {
                io->KeysDown[(int)key] = false;
                backend.KeyOwnerWindows[(int)key] = null;
            }
        }

        // Modifiers are not reliable across systems
        io->KeyCtrl = io->KeysDown[(int)Keys.LeftControl] || io->KeysDown[(int)Keys.RightControl];
        io->KeyShift = io->KeysDown[(int)Keys.LeftShift] || io->KeysDown[(int)Keys.RightShift];
        io->KeyAlt = io->KeysDown[(int)Keys.LeftAlt] || io->KeysDown[(int)Keys.RightAlt];
        if (OperatingSystem.IsWindows())
        { io->KeySuper = false; }
        else
        { io->KeySuper = io->KeysDown[(int)Keys.LeftSuper] || io->KeysDown[(int)Keys.RightSuper]; }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void WindowFocusCallback(Window* window, int focused)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackWindowFocus is not null && window == backend.Window)
        { backend.PrevUserCallbackWindowFocus(window, focused); }

        backend.Io->AddFocusEvent(focused != 0);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void CursorEnterCallback(Window* window, int entered)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackCursorEnter is not null && window == backend.Window)
        { backend.PrevUserCallbackCursorEnter(window, entered); }

        if (entered != 0)
        { backend.MouseWindow = window; }
        else if (backend.MouseWindow == window)
        { backend.MouseWindow = null; }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void CharCallback(Window* window, uint c)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackChar is not null && window == backend.Window)
        { backend.PrevUserCallbackChar(window, c); }

        backend.Io->AddInputCharacter(c);
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void MonitorCallback(Monitor* monitor, ConnectedState state)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackMonitor is not null)
        { backend.PrevUserCallbackMonitor(monitor, state); }

        backend.WantUpdateMonitors = true;
    }

    // ImGui_ImplGlfw_Shutdown
    public void Dispose()
    {
        AssertImGuiContext();

        // ImGui_ImplGlfw_ShutdownPlatformInterface
        ImGui.DestroyPlatformWindows();

        if (InstalledCallbacks)
        {
            GlfwNative.glfwSetWindowFocusCallback(Window, PrevUserCallbackWindowFocus);
            GlfwNative.glfwSetCursorEnterCallback(Window, PrevUserCallbackCursorEnter);
            GlfwNative.glfwSetMouseButtonCallback(Window, PrevUserCallbackMousebutton);
            GlfwNative.glfwSetScrollCallback(Window, PrevUserCallbackScroll);
            GlfwNative.glfwSetKeyCallback(Window, PrevUserCallbackKey);
            GlfwNative.glfwSetCharCallback(Window, PrevUserCallbackChar);
            GlfwNative.glfwSetMonitorCallback(PrevUserCallbackMonitor);
        }

        foreach (Cursor* cursor in MouseCursors)
        { GLFW.DestroyCursor(cursor); }
        Array.Clear(MouseCursors);

        ImGui.MemFree(Io->BackendPlatformName);
        Io->BackendPlatformName = null;
        Io->BackendPlatformUserData = null;

        ThisHandle.Free();
        Context = null;
    }
}
