// Note: This is a quick and dirty mechanical translation of the Dear ImGui example_glfw_opengl3 sample.
// It is not intended to demonstrate good interop practices
// Things that are quirks of using Biohazrd and could stand to be improved are marked with `BIOQUIRK`
#nullable disable
using InfectedImGui;
using InfectedImGui.Backends.Glfw;
using InfectedImGui.Infrastructure;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static InfectedImGui.Backends.OpenGL3.Globals;
using static InfectedImGui.Backends.Glfw.Globals;
using static InfectedImGui.Sample.Linux.LazyPin;
using static InfectedImGui.Sample.Linux.Gl3w;
using static InfectedImGui.Sample.Linux.Glfw;
using InfectedImGui.Sample.Linux;

public unsafe static class Program
{
    [UnmanagedCallersOnly]
    private static void glfw_error_callback(int error, byte* description)
    {
        byte* descriptionEnd = description;
        for (; *descriptionEnd != 0; descriptionEnd++);
        ReadOnlySpan<byte> descriptionSpan = new ReadOnlySpan<byte>(description, (int)(descriptionEnd - description));
        Console.Error.WriteLine($"Glfw Error {error}: {Encoding.ASCII.GetString(descriptionSpan)}");
    }

    // Main code
    public static int Main()
    {
        // Setup  window
        glfwSetErrorCallback(&glfw_error_callback);
        if (glfwInit() != GLFW_TRUE)
        {
            Console.Error.WriteLine("Failed to initialize GLFW.");
            return 1;
        }

        // Decide GL+GLSL versions
        byte* glsl_version;
        if (OperatingSystem.IsMacOS())
        {
            glsl_version = PinnedUtf8("#version 150");
            glfwWindowHint(Hint.GLFW_CONTEXT_VERSION_MAJOR, 3);
            glfwWindowHint(Hint.GLFW_CONTEXT_VERSION_MINOR, 2);
            glfwWindowHint(Hint.GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE); // 3.2+ only
            glfwWindowHint(Hint.GLFW_OPENGL_FORWARD_COMPAT, GLFW_TRUE); // Required on Mac
        }
        else
        {
            glsl_version = PinnedUtf8("#version 130");
            glfwWindowHint(Hint.GLFW_CONTEXT_VERSION_MAJOR, 3);
            glfwWindowHint(Hint.GLFW_CONTEXT_VERSION_MINOR, 0);
        }

        // Create window with graphics context
        GLFWwindow* window = glfwCreateWindow(1280, 720, PinnedUtf8("Dear ImGui GLFW+OpenGL3 example"), null, null);
        if (window == null)
        {
            Console.Error.WriteLine("Failed to create GLFW window.");
            return 1;
        }
        glfwMakeContextCurrent(window);
        glfwSwapInterval(1); // Enable vsync

        // Initialize OpenGL loader
        if (gl3wInit() != 0)
        {
            Console.Error.WriteLine("Failed to initialize GL3W!");
            return 1;
        }

        // Setup Dear ImGui context
        // IMGUI_CHECKVERSION() //BIOQUIRK: No macro translation
        ImGui.DebugCheckVersionAndDataLayout(PinnedUtf8("1.82 WIP"), (ulong)sizeof(ImGuiIO), (ulong)sizeof(ImGuiStyle), (ulong)sizeof(ImVec2), (ulong)sizeof(ImVec4), (ulong)sizeof(ImDrawVert), sizeof(ushort));
        ImGui.CreateContext();
        ImGuiIO* io = ImGui.GetIO();
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
        ImGui.StyleColorsDark();
        //ImGui.StyleColorsClassic(null);

        // When viewports are enabled we tweak WindowRounding/WindowBg so platform windows can look identical to regular ones.
        ImGuiStyle* style = ImGui.GetStyle();
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
        ImGui_ImplGlfw_InitForOpenGL(window, true);
        ImGui_ImplOpenGL3_Init(glsl_version);

        // Our state
        bool show_demo_window = true;
        bool show_another_window = false;
        ImVec4 clear_color = new() { x = 0.45f, y = 0.55f, z = 0.6f, w = 1f };

        float slider_f = 0f;
        int counter = 0;

        // Main loop
        while (glfwWindowShouldClose(window) == 0)
        {
            // Poll and handle events (inputs, window resize, etc.)
            // You can read the io.WantCaptureMouse, io.WantCaptureKeyboard flags to tell if dear imgui wants to use your inputs.
            // - When io.WantCaptureMouse is true, do not dispatch mouse input data to your main application.
            // - When io.WantCaptureKeyboard is true, do not dispatch keyboard input data to your main application.
            // Generally you may always pass all inputs to dear imgui, and hide them from your application based on those two flags.
            glfwPollEvents();

            // Start the Dear ImGui frame
            ImGui_ImplOpenGL3_NewFrame();
            ImGui_ImplGlfw_NewFrame();
            ImGui.NewFrame();

            // 1. Show the big demo window (Most of the sample code is in ImGui::ShowDemoWindow()! You can browse its code to learn more about Dear ImGui!).
            if (show_demo_window)
            { ImGui.ShowDemoWindow(&show_demo_window); }

            // 2. Show a simple window that we create ourselves. We use a Begin/End pair to created a named window.
            {
                ImGui.Begin(PinnedUtf8("Hello, world!"));

                ImGui.Text(PinnedUtf8("This is some useful text."));
                ImGui.Checkbox(PinnedUtf8("Demo Window"), &show_demo_window);
                ImGui.Checkbox(PinnedUtf8("Another Window"), &show_another_window);

                ImGui.SliderFloat(PinnedUtf8("float"), &slider_f, 0f, 1f, PinnedUtf8("%.3f")); //BIOQUIRK: Default string arguments
                ImGui.ColorEdit3(PinnedUtf8("clear color"), (ConstantArray_float_3*)&clear_color, 0); //BIOQUIRK: This isn't being translated correctly, see https://github.com/InfectedLibraries/Biohazrd/issues/73

                ImVec2 defaultVec2 = default;
                if (ImGui.Button(PinnedUtf8("Button"), &defaultVec2)) //BIOQUIRK: Default non-const arguments
                { counter++; }
                ImGui.SameLine();
                //BIOQUIRK: This kludge does not work on Linux
                //ImGui.Text(PinnedUtf8("counter = %d"), counter); //BIOQUIRK: This variable argument function is not translated correctly!
                //ImGui.TextV(PinnedUtf8("counter = %d"), (byte*)&counter); //BIOQUIRK: This is a manual ABI kludge of va_list
                ImGui.TextUnformatted(PinnedUtf8($"counter = {counter}"));

                //BIOQUIRK: This kludge does not work on Linux
                // ImGui::Text("Application average %.3f ms/frame (%.1f FPS)", 1000.0f / ImGui::GetIO().Framerate, ImGui::GetIO().Framerate);
                //(double, double) parameters = (1000.0f / ImGui.GetIO()->Framerate, ImGui.GetIO()->Framerate);
                //ImGui.TextV(PinnedUtf8("Application average %.3f ms/frame (%.1f FPS)"), (byte*)&parameters);
                ImGui.TextUnformatted(PinnedUtf8($"Application average {1000.0f / ImGui.GetIO()->Framerate:0.000} ms/frame ({ImGui.GetIO()->Framerate:0.0} FPS)"));

                ImGui.End();
            }

            // 3. Show another simple window
            if (show_another_window)
            {
                ImGui.Begin(PinnedUtf8("Another Window"), &show_another_window);
                ImGui.Text(PinnedUtf8("Hello from another window!"));
                ImVec2 defaultVec2 = default;

                if (ImGui.Button(PinnedUtf8("Close Me"), &defaultVec2)) //BIOQUIRK: Default non-const arguments
                { show_another_window = false; }

                ImGui.End();
            }

            // Rendering
            ImGui.Render();
            int display_w, display_h;
            glfwGetFramebufferSize(window, &display_w, &display_h);
            //TODO
            // glViewport(0, 0, display_w, display_h);
            // glClearColor(clear_color.x * clear_color.w, clear_color.y * clear_color.w, clear_color.z * clear_color.w, clear_color.w);
            // glClear(GL_COLOR_BUFFER_BIT);
            ImGui_ImplOpenGL3_RenderDrawData(ImGui.GetDrawData());

            // Update and Render additional Platform Windows
            // (Platform functions may change the current OpenGL context, so we save/restore it to make it easier to paste this code elsewhere.
            //  For this specific demo app we could also call glfwMakeContextCurrent(window) directly)
            if (io->ConfigFlags.HasFlag(ImGuiConfigFlags.ViewportsEnable))
            {
                GLFWwindow* backup_current_context = glfwGetCurrentContext();
                ImGui.UpdatePlatformWindows();
                ImGui.RenderPlatformWindowsDefault();
                glfwMakeContextCurrent(backup_current_context);
            }

            glfwSwapBuffers(window);
        }

        // Cleanup
        ImGui_ImplOpenGL3_Shutdown();
        ImGui_ImplGlfw_Shutdown();
        ImGui.DestroyContext();

        glfwDestroyWindow(window);
        glfwTerminate();

        return 0;
    }
}
