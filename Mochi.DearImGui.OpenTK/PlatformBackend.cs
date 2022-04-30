// Platform backend for OpenTK
// Based on imgui_impl_glfw.cpp
// https://github.com/ocornut/imgui/blob/1ee252772ae9c0a971d06257bb5c89f628fa696a/backends/imgui_impl_glfw.cpp

// Implemented features:
//  [x] Platform: Clipboard support.
//  [X] Platform: Keyboard support. Since 1.87 we are using the io.AddKeyEvent() function. Pass ImGuiKey values to all key functions e.g. ImGui::IsKeyPressed(ImGuiKey_Space). [Legacy GLFW_KEY_* values will also be supported unless IMGUI_DISABLE_OBSOLETE_KEYIO is set]
//  [x] Platform: Gamepad support. Enable with 'io.ConfigFlags |= ImGuiConfigFlags_NavEnableGamepad'.
//  [x] Platform: Mouse cursor shape and visibility. Disable with 'io.ConfigFlags |= ImGuiConfigFlags_NoMouseCursorChange' (note: the resizing cursors requires GLFW 3.4+).
//  [x] Platform: Keyboard arrays indexed using GLFW_KEY_* codes, e.g. ImGui::IsKeyPressed(GLFW_KEY_SPACE).
//  [x] Platform: Multi-viewport support (multiple windows). Enable with 'io.ConfigFlags |= ImGuiConfigFlags_ViewportsEnable'.

// Issues:
//  [ ] Platform: Multi-viewport support: ParentViewportID not honored, and so io.ConfigViewportsNoDefaultParent has no effect (minor).

using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
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
    private readonly Cursor*[] MouseCursors;
    private Vector2 LastValidMousePos;
    private readonly Window*[] KeyOwnerWindows = new Window*[(int)Keys.LastKey];
    private readonly bool InstalledCallbacks;
    private bool WantUpdateMonitors;

    // Chain GLFW callbacks: our callbacks will call the user's previously installed callbacks, if any.
    private readonly delegate* unmanaged[Cdecl]<Window*, int, void> PrevUserCallbackWindowFocus;
    private readonly delegate* unmanaged[Cdecl]<Window*, double, double, void> PrevUserCallbackCursorPos;
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

        // Unlike the native bindings we have an object associated with each context so we generally don't use the BackendPlatformUserData and instead enforce
        // a 1:1 relationship between backend instances and Dear ImGui contexts. However we still use a GC handle in BackendPlatformUserData for our platform callbacks.
        Context = ImGui.GetCurrentContext();

        ThisHandle = GCHandle.Alloc(this, GCHandleType.Weak);
        Io->BackendPlatformUserData = (void*)GCHandle.ToIntPtr(ThisHandle);

        // Set the backend name
        {
            string name = GetType().FullName ?? nameof(PlatformBackend);
            int nameByteCount = Encoding.UTF8.GetByteCount(name) + 1;
            byte* nameP = (byte*)ImGui.MemAlloc((nuint)nameByteCount);
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
            // ImGui_ImplGlfw_InstallCallbacks
            // (Unlike the native backend we don't expose the ability to do this later to discourage weird usage patterns.)
            InstalledCallbacks = true;
            PrevUserCallbackWindowFocus = GlfwNative.glfwSetWindowFocusCallback(window, &WindowFocusCallback);
            PrevUserCallbackCursorEnter = GlfwNative.glfwSetCursorEnterCallback(window, &CursorEnterCallback);
            PrevUserCallbackCursorPos = GlfwNative.glfwSetCursorPosCallback(window, &CursorPosCallback);
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

        // ImGui_ImplGlfw_UpdateMouseData
        UpdateMouseData();
        void UpdateMouseData()
        {
            ImGuiIO* io = Io;

            uint mouseViewportId = 0;
            Vector2 previousMousePosition = io->MousePos;

            for (int i = 0; i < PlatformIo->Viewports.Size; i++)
            {
                ImGuiViewport* viewport = PlatformIo->Viewports[i];
                Window* viewportWindow = (Window*)viewport->PlatformHandle;

                bool isWindowFocused = OperatingSystem.IsBrowser() || GLFW.GetWindowAttrib(viewportWindow, WindowAttributeGetBool.Focused);

                if (isWindowFocused)
                {
                    // (Optional) Set OS mouse position from Dear ImGui if requested (rarely used, only when ImGuiConfigFlags_NavEnableSetMousePos is enabled by user)
                    // When multi-viewports are enabled, all Dear ImGui positions are same as OS positions.
                    if (io->WantSetMousePos)
                    {
                        Vector2 newMousePosition = previousMousePosition - viewport->Pos;
                        GLFW.SetCursorPos(viewportWindow, (double)newMousePosition.X, (double)newMousePosition.Y);
                    }

                    // (Optional) Fallback to provide mouse position when focused (ImGui_ImplGlfw_CursorPosCallback already provides this when hovered or captured)
                    if (MouseWindow is null)
                    {
                        GLFW.GetCursorPos(viewportWindow, out double mouseX, out double mouseY);
                        if (io->ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
                        {
                            // Single viewport mode: mouse position in client window coordinates (io.MousePos is (0,0) when the mouse is on the upper-left corner of the app window)
                            // Multi-viewport mode: mouse position in OS absolute coordinates (io.MousePos is (0,0) when the mouse is on the upper-left of the primary monitor)
                            GLFW.GetWindowPos(viewportWindow, out int windowX, out int windowY);
                            mouseX += windowX;
                            mouseY += windowY;
                        }

                        LastValidMousePos = new((float)mouseX, (float)mouseY);
                        io->AddMousePosEvent((float)mouseX, (float)mouseY);
                    }
                }

                // (Optional) When using multiple viewports: call io.AddMouseViewportEvent() with the viewport the OS mouse cursor is hovering.
                // If ImGuiBackendFlags_HasMouseHoveredViewport is not set by the backend, Dear imGui will ignore this field and infer the information using its flawed heuristic.
                // - [X] GLFW >= 3.3 backend ON WINDOWS ONLY does correctly ignore viewports with the _NoInputs flag.
                // - [!] GLFW <= 3.2 backend CANNOT correctly ignore viewports with the _NoInputs flag, and CANNOT reported Hovered Viewport because of mouse capture.
                //       Some backend are not able to handle that correctly. If a backend report an hovered viewport that has the _NoInputs flag (e.g. when dragging a window
                //       for docking, the viewport has the _NoInputs flag in order to allow us to find the viewport under), then Dear ImGui is forced to ignore the value reported
                //       by the backend, and use its flawed heuristic to guess the viewport behind.
                // - [X] GLFW backend correctly reports this regardless of another viewport behind focused and dragged from (we need this to find a useful drag and drop target).
                // FIXME: This is currently only correct on Win32. See what we do below with the WM_NCHITTEST, missing an equivalent for other systems.
                // See https://github.com/glfw/glfw/issues/1236 if you want to help in making this a GLFW feature.
#if false // The hack for this on the platform backend side is more trouble than it's worth. Let's just wait for GLFW 3.4.
                if (OperatingSystem.IsWindows())
                {
                    bool windowNoInput = viewport->Flags.HasFlag(ImGuiViewportFlags.NoInputs);
#if false // This needs GLFW 3.4+, OpenTK currently uses 3.3.
                    GLFW.SetWindowAttrib(viewportWindow, WindowAttribute.MousePassthrough, windowNoInput);
#endif
                    if (!windowNoInput && GLFW.GetWindowAttrib(viewportWindow, WindowAttributeGetBool.Hovered))
                    { mouseViewportId = viewport->ID; }
                }
#else
                // We cannot use bd->MouseWindow maintained from CursorEnter/Leave callbacks, because it is locked to the window capturing mouse.
#endif
            }

            if (io->BackendFlags.HasFlag(ImGuiBackendFlags.HasMouseHoveredViewport))
            { io->AddMouseViewportEvent(mouseViewportId); }
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

            Io->BackendFlags &= ~ImGuiBackendFlags.HasGamepad;

            // Update gamepad inputs
            // (This is the GLFW_HAS_GAMEPAD_API == true case since OpenTK supports it.)
            const int GLFW_JOYSTICK_1 = 0;
            if (!GLFW.GetGamepadState(GLFW_JOYSTICK_1, out GamepadState gamepad))
            { return; }

            byte* gamepadButtons = gamepad.Buttons;
            float* gamepadAxes = gamepad.Axes;

            void MapButton(ImGuiKey key, GamepadButton button)
                => Io->AddKeyEvent(key, gamepadButtons[(int)button] != 0);

            void MapAnalog(ImGuiKey key, GamepadAxis axis, float v0, float v1)
            {
                float v = gamepadAxes[(int)axis];
                v = (v - v0) / (v1 - v0);
                float vSaturated = v < 0f ? 0f : v > 1f ? 1f : v;
                Io->AddKeyAnalogEvent(key, v > 0.1f, vSaturated);
            }

            Io->BackendFlags |= ImGuiBackendFlags.HasGamepad;

            MapButton(ImGuiKey.GamepadStart, GamepadButton.Start);
            MapButton(ImGuiKey.GamepadBack, GamepadButton.Back);
            MapButton(ImGuiKey.GamepadFaceDown, GamepadButton.A); // Xbox A, PS Cross
            MapButton(ImGuiKey.GamepadFaceRight, GamepadButton.B); // Xbox B, PS Circle
            MapButton(ImGuiKey.GamepadFaceLeft, GamepadButton.X); // Xbox X, PS Square
            MapButton(ImGuiKey.GamepadFaceUp, GamepadButton.Y); // Xbox Y, PS Triangle
            MapButton(ImGuiKey.GamepadDpadLeft, GamepadButton.DPadLeft);
            MapButton(ImGuiKey.GamepadDpadRight, GamepadButton.DPadRight);
            MapButton(ImGuiKey.GamepadDpadUp, GamepadButton.DPadUp);
            MapButton(ImGuiKey.GamepadDpadDown, GamepadButton.DPadDown);
            MapButton(ImGuiKey.GamepadL1, GamepadButton.LeftBumper);
            MapButton(ImGuiKey.GamepadR1, GamepadButton.RightBumper);
            MapAnalog(ImGuiKey.GamepadL2, GamepadAxis.LeftTrigger, -0.75f, +1.0f);
            MapAnalog(ImGuiKey.GamepadR2, GamepadAxis.RightTrigger, -0.75f, +1.0f);
            MapButton(ImGuiKey.GamepadL3, GamepadButton.LeftThumb);
            MapButton(ImGuiKey.GamepadR3, GamepadButton.RightThumb);
            MapAnalog(ImGuiKey.GamepadLStickLeft, GamepadAxis.LeftX, -0.25f, -1.0f);
            MapAnalog(ImGuiKey.GamepadLStickRight, GamepadAxis.LeftX, +0.25f, +1.0f);
            MapAnalog(ImGuiKey.GamepadLStickUp, GamepadAxis.LeftY, -0.25f, -1.0f);
            MapAnalog(ImGuiKey.GamepadLStickDown, GamepadAxis.LeftY, +0.25f, +1.0f);
            MapAnalog(ImGuiKey.GamepadRStickLeft, GamepadAxis.RightX, -0.25f, -1.0f);
            MapAnalog(ImGuiKey.GamepadRStickRight, GamepadAxis.RightX, +0.25f, +1.0f);
            MapAnalog(ImGuiKey.GamepadRStickUp, GamepadAxis.RightY, -0.25f, -1.0f);
            MapAnalog(ImGuiKey.GamepadRStickDown, GamepadAxis.RightY, +0.25f, +1.0f);
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

    // ImGui_ImplGlfw_UpdateKeyModifiers
    private static void UpdateKeyModifiers(ImGuiIO* io, KeyModifiers modifiers)
    {
        io->AddKeyEvent(ImGuiKey.ModCtrl, modifiers.HasFlag(KeyModifiers.Control));
        io->AddKeyEvent(ImGuiKey.ModShift, modifiers.HasFlag(KeyModifiers.Shift));
        io->AddKeyEvent(ImGuiKey.ModAlt, modifiers.HasFlag(KeyModifiers.Alt));
        io->AddKeyEvent(ImGuiKey.ModSuper, modifiers.HasFlag(KeyModifiers.Super));
    }

    //ImGui_ImplGlfw_MouseButtonCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void MouseButtonCallback(Window* window, MouseButton button, InputAction action, KeyModifiers modifiers)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackMousebutton is not null && window == backend.Window)
        { backend.PrevUserCallbackMousebutton(window, button, action, modifiers); }

        UpdateKeyModifiers(backend.Io, modifiers);

        if (button >= 0 && (int)button < (int)ImGuiMouseButton.COUNT)
        { backend.Io->AddMouseButtonEvent((int)button, action == InputAction.Press); }
    }

    // ImGui_ImplGlfw_ScrollCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void ScrollCallback(Window* window, double xOffset, double yOffset)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackScroll is not null && window == backend.Window)
        { backend.PrevUserCallbackScroll(window, xOffset, yOffset); }

        backend.Io->AddMouseWheelEvent((float)xOffset, (float)yOffset);
    }

    // ImGui_ImplGlfw_KeyCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void KeyCallback(Window* window, Keys key, int scancode, InputAction action, KeyModifiers modifiers)
        => KeyCallbackManaged(window, key, scancode, action, modifiers);

    private static void KeyCallbackManaged(Window* window, Keys key, int scancode, InputAction action, KeyModifiers modifiers)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackKey is not null && window == backend.Window)
        { backend.PrevUserCallbackKey(window, key, scancode, action, modifiers); }

        if (action != InputAction.Press && action != InputAction.Release)
        { return; }

        ImGuiIO* io = backend.Io;
        UpdateKeyModifiers(io, modifiers);

        if (key >= 0 && (int)key < backend.KeyOwnerWindows.Length)
        { backend.KeyOwnerWindows[(int)key] = action is InputAction.Press ? window : null; }

        key = TranslateUntranslatedKey(key, scancode);
        // ImGui_ImplGlfw_TranslateUntranslatedKey
        static Keys TranslateUntranslatedKey(Keys key, int scancode)
        {
            if (OperatingSystem.IsBrowser())
            { return key; }

            if (key >= Keys.KeyPad0 && key <= Keys.KeyPadEqual)
            { return key; }

            byte* _keyName = GLFW.GetKeyNameRaw(key, scancode);
            if (_keyName is not null && _keyName[0] != 0 && _keyName[1] == 0)
            {
                char keyName = (char)_keyName[0];
                return keyName switch
                {
                    >= '0' and <= '9' => Keys.D0 + (keyName - '0'),
                    >= 'A' and <= 'Z' => Keys.A + (keyName - 'A'),
                    >= 'a' and <= 'z' => Keys.A + (keyName - 'a'),
                    '`' => Keys.GraveAccent,
                    '-' => Keys.Minus,
                    '=' => Keys.Equal,
                    '[' => Keys.LeftBracket,
                    ']' => Keys.RightBracket,
                    '\\' => Keys.Backslash,
                    ',' => Keys.Comma,
                    ';' => Keys.Semicolon,
                    '\'' => Keys.Apostrophe,
                    '.' => Keys.Period,
                    '/' => Keys.Slash,
                    _ => key
                };
            }
            else
            { return key; }
        }

        // ImGui_ImplGlfw_KeyToImGuiKey
        ImGuiKey imGuiKey = key switch
        {
            Keys.Tab => ImGuiKey.Tab,
            Keys.Left => ImGuiKey.LeftArrow,
            Keys.Right => ImGuiKey.RightArrow,
            Keys.Up => ImGuiKey.UpArrow,
            Keys.Down => ImGuiKey.DownArrow,
            Keys.PageUp => ImGuiKey.PageUp,
            Keys.PageDown => ImGuiKey.PageDown,
            Keys.Home => ImGuiKey.Home,
            Keys.End => ImGuiKey.End,
            Keys.Insert => ImGuiKey.Insert,
            Keys.Delete => ImGuiKey.Delete,
            Keys.Backspace => ImGuiKey.Backspace,
            Keys.Space => ImGuiKey.Space,
            Keys.Enter => ImGuiKey.Enter,
            Keys.Escape => ImGuiKey.Escape,
            Keys.Apostrophe => ImGuiKey.Apostrophe,
            Keys.Comma => ImGuiKey.Comma,
            Keys.Minus => ImGuiKey.Minus,
            Keys.Period => ImGuiKey.Period,
            Keys.Slash => ImGuiKey.Slash,
            Keys.Semicolon => ImGuiKey.Semicolon,
            Keys.Equal => ImGuiKey.Equal,
            Keys.LeftBracket => ImGuiKey.LeftBracket,
            Keys.Backslash => ImGuiKey.Backslash,
            Keys.RightBracket => ImGuiKey.RightBracket,
            Keys.GraveAccent => ImGuiKey.GraveAccent,
            Keys.CapsLock => ImGuiKey.CapsLock,
            Keys.ScrollLock => ImGuiKey.ScrollLock,
            Keys.NumLock => ImGuiKey.NumLock,
            Keys.PrintScreen => ImGuiKey.PrintScreen,
            Keys.Pause => ImGuiKey.Pause,
            Keys.KeyPad0 => ImGuiKey.Keypad0,
            Keys.KeyPad1 => ImGuiKey.Keypad1,
            Keys.KeyPad2 => ImGuiKey.Keypad2,
            Keys.KeyPad3 => ImGuiKey.Keypad3,
            Keys.KeyPad4 => ImGuiKey.Keypad4,
            Keys.KeyPad5 => ImGuiKey.Keypad5,
            Keys.KeyPad6 => ImGuiKey.Keypad6,
            Keys.KeyPad7 => ImGuiKey.Keypad7,
            Keys.KeyPad8 => ImGuiKey.Keypad8,
            Keys.KeyPad9 => ImGuiKey.Keypad9,
            Keys.KeyPadDecimal => ImGuiKey.KeypadDecimal,
            Keys.KeyPadDivide => ImGuiKey.KeypadDivide,
            Keys.KeyPadMultiply => ImGuiKey.KeypadMultiply,
            Keys.KeyPadSubtract => ImGuiKey.KeypadSubtract,
            Keys.KeyPadAdd => ImGuiKey.KeypadAdd,
            Keys.KeyPadEnter => ImGuiKey.KeypadEnter,
            Keys.KeyPadEqual => ImGuiKey.KeypadEqual,
            Keys.LeftShift => ImGuiKey.LeftShift,
            Keys.LeftControl => ImGuiKey.LeftCtrl,
            Keys.LeftAlt => ImGuiKey.LeftAlt,
            Keys.LeftSuper => ImGuiKey.LeftSuper,
            Keys.RightShift => ImGuiKey.RightShift,
            Keys.RightControl => ImGuiKey.RightCtrl,
            Keys.RightAlt => ImGuiKey.RightAlt,
            Keys.RightSuper => ImGuiKey.RightSuper,
            Keys.Menu => ImGuiKey.Menu,
            Keys.D0 => ImGuiKey._0,
            Keys.D1 => ImGuiKey._1,
            Keys.D2 => ImGuiKey._2,
            Keys.D3 => ImGuiKey._3,
            Keys.D4 => ImGuiKey._4,
            Keys.D5 => ImGuiKey._5,
            Keys.D6 => ImGuiKey._6,
            Keys.D7 => ImGuiKey._7,
            Keys.D8 => ImGuiKey._8,
            Keys.D9 => ImGuiKey._9,
            Keys.A => ImGuiKey.A,
            Keys.B => ImGuiKey.B,
            Keys.C => ImGuiKey.C,
            Keys.D => ImGuiKey.D,
            Keys.E => ImGuiKey.E,
            Keys.F => ImGuiKey.F,
            Keys.G => ImGuiKey.G,
            Keys.H => ImGuiKey.H,
            Keys.I => ImGuiKey.I,
            Keys.J => ImGuiKey.J,
            Keys.K => ImGuiKey.K,
            Keys.L => ImGuiKey.L,
            Keys.M => ImGuiKey.M,
            Keys.N => ImGuiKey.N,
            Keys.O => ImGuiKey.O,
            Keys.P => ImGuiKey.P,
            Keys.Q => ImGuiKey.Q,
            Keys.R => ImGuiKey.R,
            Keys.S => ImGuiKey.S,
            Keys.T => ImGuiKey.T,
            Keys.U => ImGuiKey.U,
            Keys.V => ImGuiKey.V,
            Keys.W => ImGuiKey.W,
            Keys.X => ImGuiKey.X,
            Keys.Y => ImGuiKey.Y,
            Keys.Z => ImGuiKey.Z,
            Keys.F1 => ImGuiKey.F1,
            Keys.F2 => ImGuiKey.F2,
            Keys.F3 => ImGuiKey.F3,
            Keys.F4 => ImGuiKey.F4,
            Keys.F5 => ImGuiKey.F5,
            Keys.F6 => ImGuiKey.F6,
            Keys.F7 => ImGuiKey.F7,
            Keys.F8 => ImGuiKey.F8,
            Keys.F9 => ImGuiKey.F9,
            Keys.F10 => ImGuiKey.F10,
            Keys.F11 => ImGuiKey.F11,
            Keys.F12 => ImGuiKey.F12,
            _ => ImGuiKey.None
        };

        io->AddKeyEvent(imGuiKey, action == InputAction.Press);
        io->SetKeyEventNativeData(imGuiKey, (int)key, scancode); // To support legacy indexing (<1.87 user code)
    }

    // ImGui_ImplGlfw_WindowFocusCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void WindowFocusCallback(Window* window, int focused)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackWindowFocus is not null && window == backend.Window)
        { backend.PrevUserCallbackWindowFocus(window, focused); }

        backend.Io->AddFocusEvent(focused != 0);
    }

    // ImGui_ImplGlfw_CursorPosCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void CursorPosCallback(Window* window, double x, double y)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackCursorPos is not null && window == backend.Window)
        { backend.PrevUserCallbackCursorPos(window, x, y); }

        if (backend.Io->ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
        {
            GLFW.GetWindowPos(window, out int windowX, out int windowY);
            x += windowX;
            y += windowY;
        }

        backend.Io->AddMousePosEvent((float)x, (float)y);
        backend.LastValidMousePos = new((float)x, (float)y);
    }

    // ImGui_ImplGlfw_CursorEnterCallback
    // Workaround: X11 seems to send spurious Leave/Enter events which would make us lose our position,
    // so we back it up and restore on Leave/Enter (see https://github.com/ocornut/imgui/issues/4984)
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void CursorEnterCallback(Window* window, int entered)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackCursorEnter is not null && window == backend.Window)
        { backend.PrevUserCallbackCursorEnter(window, entered); }

        if (entered != 0)
        {
            backend.MouseWindow = window;
            backend.Io->AddMousePosEvent(backend.LastValidMousePos.X, backend.LastValidMousePos.Y);
        }
        else if (backend.MouseWindow == window)
        {
            backend.LastValidMousePos = backend.Io->MousePos;
            backend.MouseWindow = null;
            backend.Io->AddMousePosEvent(-float.MaxValue, -float.MaxValue);
        }
    }

    // ImGui_ImplGlfw_CharCallback
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void CharCallback(Window* window, uint c)
    {
        PlatformBackend backend = GetPlatformBackend();
        if (backend.PrevUserCallbackChar is not null && window == backend.Window)
        { backend.PrevUserCallbackChar(window, c); }

        backend.Io->AddInputCharacter(c);
    }

    // ImGui_ImplGlfw_MonitorCallback
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
            // ImGui_ImplGlfw_RestoreCallbacks
            // (Unlike the native backend we don't expose the ability to do this later to discourage weird usage patterns.)
            GlfwNative.glfwSetWindowFocusCallback(Window, PrevUserCallbackWindowFocus);
            GlfwNative.glfwSetCursorEnterCallback(Window, PrevUserCallbackCursorEnter);
            GlfwNative.glfwSetCursorPosCallback(Window, PrevUserCallbackCursorPos);
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
