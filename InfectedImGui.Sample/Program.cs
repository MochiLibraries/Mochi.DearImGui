// Note: This is a quick and dirty mechanical translation of the Dear ImGui example_win32_directx11 sample.
// It is not intended to demonstrate good interop practices
// Things that are quirks of using Biohazrd and could stand to be improved are marked with `BIOQUIRK`
#nullable disable
using Ares.Platform.Windows.Interop;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Ares.Platform.Windows.Interop.Native;
using static imgui_impl_dx11;
using static imgui_impl_win32;
using static InfectedImGui.Sample.LazyPin;
using Device = SharpDX.Direct3D11.Device;
using HWND = System.IntPtr;
using LPARAM = nint;
using LRESULT = nint;
using MessageId = Ares.Platform.Windows.Interop.MessageId;
using WPARAM = nint;

public unsafe static class Program
{
    // Data
    private static Device g_pd3dDevice;
    private static DeviceContext g_pd3dDeviceContext;
    private static SwapChain g_pSwapChain;
    private static RenderTargetView g_mainRenderTargetView;

    // Main code
    public static int Main()
    {
        ImGui_ImplWin32_EnableDpiAwareness();

        // Create application window
        WNDCLASSEXW wc = new()
        {
            StructSize = (uint)sizeof(WNDCLASSEXW),
            Style = ClassStyles.CS_CLASSDC,
            WindowProcedure = &WndProc,
            ExtraClassBytes = 0,
            ExtraWindowBytes = 0,
            Instance = Native.GetModuleHandleW(IntPtr.Zero),
            Icon = IntPtr.Zero,
            Cursor = IntPtr.Zero,
            Background = IntPtr.Zero,
            MenuName = null,
            ClassName = PinnedUtf16("ImGui Example"),
            SmallIcon = IntPtr.Zero
        };
        ATOM classAtom = RegisterClassExW(wc);
        if (!classAtom.IsValid)
        { throw new Win32Exception(); }

        HWND hwnd = CreateWindowExW(0, wc.ClassName, PinnedUtf16("Infected Dear ImGui DirectX11 Example"), WindowStyles.WS_OVERLAPPEDWINDOW,
            100, 100, 1280, 800, IntPtr.Zero, IntPtr.Zero, wc.Instance, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
        { throw new Win32Exception(); }

        // Initialize Direct3D
        if (!CreateDeviceD3D(hwnd))
        {
            CleanupDeviceD3D();
            UnregisterClassW(wc.ClassName, wc.Instance);
            return 1;
        }

        // Show the window
        ShowWindow(hwnd, ShowWindowMode.SW_SHOWDEFAULT);
        UpdateWindow(hwnd);

        // Setup Dear ImGui context
        // IMGUI_CHECKVERSION() //BIOQUIRK: No macro translation
        imgui.DebugCheckVersionAndDataLayout(PinnedUtf8("1.81"), (ulong)sizeof(ImGuiIO), (ulong)sizeof(ImGuiStyle), (ulong)sizeof(ImVec2), (ulong)sizeof(ImVec4), (ulong)sizeof(ImDrawVert), sizeof(ushort));
        imgui.CreateContext();
        ImGuiIO* io = imgui.GetIO();
        io->ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        //io->ConfigFlags |= ImGuiConfigFlags_NavEnableGamepad;
        io->ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io->ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        //io->ConfigViewportsNoAutoMerge = true;
        //io->ConfigViewportsNoTaskBarIcon = true;
        //io->ConfigViewportsNoDefaultParent = true;
        //io->ConfigDockingAlwaysTabBar = true;
        //io->ConfigDockingTransparentPayload = true;
#if true
        io->ConfigFlags |= ImGuiConfigFlags.DpiEnableScaleFonts;
        io->ConfigFlags |= ImGuiConfigFlags.DpiEnableScaleViewports;
#endif

        // Setup Dear ImGui style
        imgui.StyleColorsDark();
        //imgui.StyleColorsClassic(null);

        // When viewports are enabled we tweak WindowRounding/WindowBg so platform windows can look identical to regular ones.
        ImGuiStyle* style = imgui.GetStyle();
        if ((io->ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
        {
            style->WindowRounding = 0f;
            // BIOQUIRK: Could provide overload for indexer.
            // Maybe just allow some sort of non-deduping for constant arrays?
            // We could add this today since ConstantArray_ImVec4_50 is partial, but it'd be annoying if the same constant array type was used in multiple places but this only applied in some.
            // (Maybe allow generating unique constant arrays like ConstantArray_ImGuiStyle_Colors instead?)
            style->Colors[(int)ImGuiCol.WindowBg].w = 1f;
        }

        // Setup Platform/Renderer bindings
        ImGui_ImplWin32_Init((void*)hwnd);
        // This looks a little odd because we're converting the SharpDX objects to pointers to the forwardly-declared types imgui_impl_dx11 has.
        ImGui_ImplDX11_Init((ID3D11Device*)g_pd3dDevice.NativePointer, (ID3D11DeviceContext*)g_pd3dDeviceContext.NativePointer);

        // Our state
        bool show_demo_window = true;
        bool show_another_window = false;
        ImVec4 clear_color = new() { x = 0.45f, y = 0.55f, z = 0.6f, w = 1f };

        float slider_f = 0f;
        int counter = 0;

        // Main loop
        MSG msg = default;
        while (msg.MessageId != MessageId.WM_QUIT)
        {
            // Poll and handle messages (inputs, window resize, etc.)
            // You can read the io.WantCaptureMouse, io.WantCaptureKeyboard flags to tell if dear imgui wants to use your inputs.
            // - When io.WantCaptureMouse is true, do not dispatch mouse input data to your main application.
            // - When io.WantCaptureKeyboard is true, do not dispatch keyboard input data to your main application.
            // Generally you may always pass all inputs to dear imgui, and hide them from your application based on those two flags.
            if (PeekMessageW(out msg, IntPtr.Zero, 0U, 0U, PeekMessageFlags.PM_REMOVE))
            {
                TranslateMessage(msg);
                DispatchMessage(msg);
                continue;
            }

            // Start the Dear ImGui frame
            ImGui_ImplDX11_NewFrame();
            ImGui_ImplWin32_NewFrame();
            imgui.NewFrame();

            // 1. Show the big demo window (Most of the sample code is in ImGui::ShowDemoWindow()! You can browse its code to learn more about Dear ImGui!).
            if (show_demo_window)
            { imgui.ShowDemoWindow(&show_demo_window); }

            // 2. Show a simple window that we create ourselves. We use a Begin/End pair to created a named window.
            {
                imgui.Begin(PinnedUtf8("Hello, world!"));

                imgui.Text(PinnedUtf8("This is some useful text."));
                imgui.Checkbox(PinnedUtf8("Demo Window"), &show_demo_window);
                imgui.Checkbox(PinnedUtf8("Another Window"), &show_another_window);

                imgui.SliderFloat(PinnedUtf8("float"), &slider_f, 0f, 1f, PinnedUtf8("%.3f")); //BIOQUIRK: Default string arguments
                imgui.ColorEdit3(PinnedUtf8("clear color"), Unsafe.As<ImVec4, ConstantArray_float_3>(ref clear_color), 0); //BIOQUIRK: This isn't being translated correctly, see https://github.com/InfectedLibraries/Biohazrd/issues/73

                ImVec2 defaultVec2 = default;
                if (imgui.Button(PinnedUtf8("Button"), &defaultVec2)) //BIOQUIRK: Default non-const arguments
                { counter++; }
                imgui.SameLine();
                //imgui.Text(PinnedUtf8("counter = %d"), counter); //BIOQUIRK: This variable argument function is not translated correctly!
                imgui.TextV(PinnedUtf8("counter = %d"), (byte*)&counter); //BIOQUIRK: This is a manual ABI kludge of va_list

                // ImGui::Text("Application average %.3f ms/frame (%.1f FPS)", 1000.0f / ImGui::GetIO().Framerate, ImGui::GetIO().Framerate);
                (double, double) parameters = (1000.0f / imgui.GetIO()->Framerate, imgui.GetIO()->Framerate);
                imgui.TextV(PinnedUtf8("Application average %.3f ms/frame (%.1f FPS)"), (byte*)&parameters);

                imgui.End();
            }

            // 3. Show another simple window
            if (show_another_window)
            {
                imgui.Begin(PinnedUtf8("Another Window"), &show_another_window);
                imgui.Text(PinnedUtf8("Hello from another window!"));
                ImVec2 defaultVec2 = default;

                if (imgui.Button(PinnedUtf8("Close Me"), &defaultVec2)) //BIOQUIRK: Default non-const arguments
                { show_another_window = false; }

                imgui.End();
            }

            // Rendering
            imgui.Render();
            g_pd3dDeviceContext.OutputMerger.SetRenderTargets(g_mainRenderTargetView);
            g_pd3dDeviceContext.ClearRenderTargetView(g_mainRenderTargetView, Unsafe.As<ImVec4, RawColor4>(ref clear_color));
            ImGui_ImplDX11_RenderDrawData(imgui.GetDrawData());

            // Update and Render additional Platform Windows
            if ((io->ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0)
            {
                imgui.UpdatePlatformWindows();
                imgui.RenderPlatformWindowsDefault();
            }

            g_pSwapChain.Present(1, PresentFlags.None); // Present with vsync
        }

        // Cleanup
        ImGui_ImplDX11_Shutdown();
        ImGui_ImplWin32_Shutdown();
        imgui.DestroyContext();

        CleanupDeviceD3D();
        DestroyWindow(hwnd);
        UnregisterClassW(wc.ClassName, wc.Instance);

        return 0;
    }

    // Helper functions
    private static bool CreateDeviceD3D(HWND hWnd)
    {
        // Setup swap chain
        SwapChainDescription sd = new SwapChainDescription()
        {
            BufferCount = 2,
            ModeDescription = new ModeDescription()
            {
                Width = 0,
                Height = 0,
                Format = Format.R8G8B8A8_UNorm,
                RefreshRate = new(60, 1)
            },
            Flags = SwapChainFlags.AllowModeSwitch,
            Usage = Usage.RenderTargetOutput,
            OutputHandle = hWnd,
            SampleDescription = new()
            {
                Count = 1,
                Quality = 0
            },
            IsWindowed = true,
            SwapEffect = SwapEffect.Discard
        };

        DeviceCreationFlags createDeviceFlags = 0;
        //createDeviceFlags |= DeviceCreationFlags.Debug;
        FeatureLevel[] featureLevelArray = new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_0 };
        Device.CreateWithSwapChain(DriverType.Hardware, createDeviceFlags, featureLevelArray, sd, out g_pd3dDevice, out g_pSwapChain);
        g_pd3dDeviceContext = g_pd3dDevice.ImmediateContext;

        CreateRenderTarget();
        return true;
    }

    private static void CleanupDeviceD3D()
    {
        CleanupRenderTarget();
        if (g_pSwapChain is not null)
        {
            g_pSwapChain.Dispose();
            g_pSwapChain = null;
        }

        if (g_pd3dDeviceContext is not null)
        {
            g_pd3dDeviceContext.Dispose();
            g_pd3dDeviceContext = null;
        }

        if (g_pd3dDevice is not null)
        {
            g_pd3dDevice.Dispose();
            g_pd3dDevice = null;
        }
    }

    private static void CreateRenderTarget()
    {
        Texture2D pBackBuffer = g_pSwapChain.GetBackBuffer<Texture2D>(0);
        g_mainRenderTargetView = new RenderTargetView(g_pd3dDevice, pBackBuffer);
        pBackBuffer.Dispose();
    }

    private static void CleanupRenderTarget()
    {
        if (g_mainRenderTargetView is not null)
        {
            g_mainRenderTargetView.Dispose();
            g_mainRenderTargetView = null;
        }
    }

    // Forward declare message handler from imgui_impl_win32.cpp
    //BIOQUIRK: https://github.com/InfectedLibraries/Biohazrd/issues/72 could improve this.
    [DllImport("InfectedImGui.Native.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern LRESULT ImGui_ImplWin32_WndProcHandler(HWND hWnd, MessageId msg, WPARAM wParam, LPARAM lParam);

    // Win32 message handler
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static LRESULT WndProc(HWND hWnd, MessageId msg, WPARAM wParam, LPARAM lParam)
    {
        if (ImGui_ImplWin32_WndProcHandler(hWnd, msg, wParam, lParam) != 0)
        { return 1; }

        switch (msg)
        {
            case MessageId.WM_SIZE:
                const nint SIZE_MINIMIZED = 1;
                if (g_pd3dDevice is not null && wParam != SIZE_MINIMIZED)
                {
                    CleanupRenderTarget();
                    int width = (int)(lParam & 0xFFFF);
                    int height = (int)((lParam >> 16) & 0xFFFF);
                    g_pSwapChain.ResizeBuffers(0, width, height, Format.Unknown, 0);
                    CreateRenderTarget();
                }
                return 0;
            case MessageId.WM_SYSCOMMAND:
                const nint SC_KEYMENU = 0xF100;
                if ((wParam & 0xFFF0) == SC_KEYMENU) // Disable ALT application menu
                { return 0; }
                break;
            case MessageId.WM_DESTROY:
                PostQuitMessage(0);
                return 0;
            case MessageId.WM_DPICHANGED:
                if ((imgui.GetIO()->ConfigFlags & ImGuiConfigFlags.DpiEnableScaleViewports) != 0)
                {
                    RECT* suggested_rect = (RECT*)lParam;
                    SetWindowPos(hWnd, IntPtr.Zero, suggested_rect->Left, suggested_rect->Top, suggested_rect->Right - suggested_rect->Left, suggested_rect->Bottom - suggested_rect->Top, SetWindowPosFlags.NOZORDER | SetWindowPosFlags.NOACTIVATE);
                }
                break;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }
}
