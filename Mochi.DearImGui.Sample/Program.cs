// This sample is based on example_glfw_opengl3
// https://github.com/ocornut/imgui/tree/704ab1114aa54858b690711554cf3312fbbcc3fc/examples/example_glfw_opengl3
// We derive from OpenTK's NativeWindow rather than GameWindow for the sake of simplicity.
// Some of the direct GLFW access could be replaced with higher level calls or removed if GameWindow was used instead.
//
// Things that are quirks of using Biohazrd and could stand to be improved are marked with `BIOQUIRK`
using Mochi.DearImGui;
using Mochi.DearImGui.OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Numerics;

// Start in the app's base directory to avoid polluting imgui.ini and to make fonts accessible
Environment.CurrentDirectory = AppContext.BaseDirectory;

// Setup window
NativeWindowSettings nativeWindowSettings = new()
{
    Size = new(1280, 720),
    Title = "Dear ImGui-flavored Mochi OpenTK example",
};

// Decide GL+GLSL versions
string glslVersion;
//TODO: GLES support
if (OperatingSystem.IsMacOS())
{
    // GL 3.2 + GLSL 150
    glslVersion = "#version 150";
    nativeWindowSettings.APIVersion = new(3, 2);
    nativeWindowSettings.Profile = ContextProfile.Core; // 3.2+ only
    nativeWindowSettings.Flags = ContextFlags.ForwardCompatible; // Required on macOS
}
else
{
    // GL 3.0 + GLSL 130
    glslVersion = "#version 130";
    nativeWindowSettings.APIVersion = new(3, 0);
    nativeWindowSettings.Profile = ContextProfile.Any;
    //nativeWindowSettings.Profile = ContextProfile.Core; // 3.2+ only
    //nativeWindowSettings.Flags = ContextFlags.ForwardCompatible; // 3.0+ only
}

using SampleWindow window = new(nativeWindowSettings, glslVersion);
window.Run();

internal unsafe sealed class SampleWindow : NativeWindow
{
    private readonly string? GlslVersion;
    private readonly RendererBackend RendererBackend;
    private readonly PlatformBackend PlatformBackend;

    public SampleWindow(NativeWindowSettings nativeWindowSettings, string? glslVersion)
        : base(nativeWindowSettings)
    {
        GlslVersion = glslVersion;
        Context.MakeCurrent();
        VSync = VSyncMode.On; // Enable vsync

        // Setup Dear ImGui context
        ImGui.CHECKVERSION();
        ImGui.CreateContext();
        ImGuiIO* io = ImGui.GetIO();
        io->ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard; // Enable Keyboard Controls
        //io->ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad; // Enable Gamepad Controls
        io->ConfigFlags |= ImGuiConfigFlags.DockingEnable; // Enable Docking
        io->ConfigFlags |= ImGuiConfigFlags.ViewportsEnable; // Enable Multi-Viewport / Platform Windows
        //io->ConfigViewportsNoAutoMerge = true;
        //io->ConfigViewportsNoTaskBarIcon = true;

        // Setup Dear ImGui style
        ImGui.StyleColorsDark();
        //ImGui.StyleColorsClassic();

        // When viewports are enabled we tweak WindowRounding/WindowBg so platform windows can look identical to regular ones.
        ImGuiStyle* style = ImGui.GetStyle();
        if (io->ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
        {
            style->WindowRounding = 0f;
            //BIOQUIRK: We should special-case this to make it more friendly. (https://github.com/MochiLibraries/Biohazrd/issues/139 would help here)
            style->Colors[(int)ImGuiCol.WindowBg].W = 1f;
        }

        // Setup Platform/Renderer backends
        PlatformBackend = new(this, true);
        RendererBackend = new(GlslVersion);

        // Load Fonts
        // - If no fonts are loaded, dear imgui will use the default font. You can also load multiple fonts and use ImGui::PushFont()/PopFont() to select them.
        // - AddFontFromFileTTF() will return the ImFont* so you can store it if you need to select the font among multiple.
        // - If the file cannot be loaded, the function will return NULL. Please handle those errors in your application (e.g. use an assertion, or display an error and quit).
        // - The fonts will be rasterized at a given size (w/ oversampling) and stored into a texture when calling ImFontAtlas::Build()/GetTexDataAsXXXX(), which ImGui_ImplXXXX_NewFrame below will call.
        // - Read 'docs/FONTS.md' for more instructions and details.
        // - Remember that in C# if you want to include a backslash \ in a string literal you need to write a double backslash \\ !
        //io->Fonts->AddFontDefault();
        //io->Fonts->AddFontFromFileTTF("fonts/Roboto-Medium.ttf", 16f);
        //io->Fonts->AddFontFromFileTTF("fonts/Cousine-Regular.ttf", 15.0f);
        //io->Fonts->AddFontFromFileTTF("fonts/DroidSans.ttf", 16.0f);
        //io->Fonts->AddFontFromFileTTF("fonts/ProggyTiny.ttf", 10.0f);
        //ImFont* font = io->Fonts->AddFontFromFileTTF(@"C:\Windows\Fonts\ArialUni.ttf", 18.0f, null, io->Fonts->GetGlyphRangesJapanese());
        //Debug.Assert(font != null);
    }

    public void Run()
    {
        ImGuiIO* io = ImGui.GetIO();

        // Our state
        bool showDemoWindow = true;
        bool showAnotherWindow = false;
        Vector3 clearColor = new(0.45f, 0.55f, 0.6f);

        float f = 0f;
        int counter = 0;

        // Main loop
        while (!GLFW.WindowShouldClose(WindowPtr))
        {
            // Poll and handle events (inputs, window resize, etc.)
            // You can read the io.WantCaptureMouse, io.WantCaptureKeyboard flags to tell if dear imgui wants to use your inputs.
            // - When io.WantCaptureMouse is true, do not dispatch mouse input data to your main application.
            // - When io.WantCaptureKeyboard is true, do not dispatch keyboard input data to your main application.
            // Generally you may always pass all inputs to dear imgui, and hide them from your application based on those two flags.
            ProcessEvents();

            // Start the Dear ImGui frame
            RendererBackend.NewFrame();
            PlatformBackend.NewFrame();
            ImGui.NewFrame();

            // 1. Show the big demo window (Most of the sample code is in ImGui::ShowDemoWindow()! You can browse its code to learn more about Dear ImGui!).
            if (showDemoWindow)
            { ImGui.ShowDemoWindow(&showDemoWindow); }

            // 2. Show a simple window that we create ourselves. We use a Begin/End pair to created a named window.
            {
                // Create a window called "Hello, world!" and append into it.
                ImGui.Begin("Hello, world!");

                // Display some text
                ImGui.Text("This is some useful text.");

                // Edit bools storing our window open/close state
                ImGui.Checkbox("Demo Window", &showDemoWindow);
                ImGui.Checkbox("Another Window", &showAnotherWindow);

                // Edit 1 float using a slider from 0.0f to 1.0f
                ImGui.SliderFloat("float", &f, 0f, 1f, "%.3f"); //BIOQUIRK: Default string argument
                ImGui.ColorEdit3("clear color", &clearColor);

                // Buttons return true when clicked (most widgets return true when edited/activated)
                if (ImGui.Button("Button", default)) //BIOQUIRK: Default vector argument
                { counter++; }
                ImGui.SameLine();
                ImGui.Text($"counter = {counter}");

                ImGui.Text($"Application average {1000f / io->Framerate} ms/frame ({io->Framerate} FPS)");
                ImGui.End();
            }

            // 3. Show another simple window.
            if (showAnotherWindow)
            {
                ImGui.Begin("Another Window", &showAnotherWindow); // Pass a pointer to our bool variable (the window will have a closing button that will clear the bool when clicked)
                ImGui.Text("Hello from another window!");
                if (ImGui.Button("Close Me", default)) //BIOQUIRK: Default vector argument
                { showAnotherWindow = false; }
                ImGui.End();
            }

            // Rendering
            {
                ImGui.Render();
                GLFW.GetFramebufferSize(WindowPtr, out int displayW, out int displayH);
                GL.Viewport(0, 0, displayW, displayH);
                GL.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, 1f);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                RendererBackend.RenderDrawData(ImGui.GetDrawData());

                // Update and Render additional Platform Windows
                // (Platform functions may change the current OpenGL context, so we save/restore it to make it easier to paste this code elsewhere.
                //  For this specific demo app we could also call glfwMakeContextCurrent(window) directly)
                if (io->ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
                {
                    Window* backupCurrentContext = GLFW.GetCurrentContext();
                    ImGui.UpdatePlatformWindows();
                    ImGui.RenderPlatformWindowsDefault();
                    GLFW.MakeContextCurrent(backupCurrentContext);
                }

                GLFW.SwapBuffers(WindowPtr);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        // Cleanup
        // The managed backends do not have finalizers because it isn't safe for them to be disposed of in an unpredictable order relative to this class
        // As such we dispose of them regardless of `disposing`.
        RendererBackend.Dispose();
        PlatformBackend.Dispose();
        ImGui.DestroyContext();

        base.Dispose(disposing);
    }
}
