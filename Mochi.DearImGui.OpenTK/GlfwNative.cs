using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Runtime.InteropServices;

namespace Mochi.DearImGui.OpenTK;

// OpenTK exposes a more managed view of the GLFW callbacks API.
// It's easier for us to use C#9 function pointers instead so we re-expose relevant methods
// We also expose a few methods which are present in OpenTK's distribution of GLFW but aren't exposed from C#.
internal unsafe sealed class GlfwNative
{
    private const string LibraryName = "glfw3.dll";

    static GlfwNative()
        => NativeLibrary.SetDllImportResolver(typeof(GlfwNative).Assembly, (name, assembly, path) =>
        {
            if (name != LibraryName)
            { return IntPtr.Zero; }

            return NativeLibrary.Load(LibraryName, typeof(GLFW).Assembly, path);
        });

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<ErrorCode, byte*, void> glfwSetErrorCallback(delegate* unmanaged[Cdecl]<ErrorCode, byte*, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, int, void> glfwSetWindowFocusCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, int, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, int, void> glfwSetCursorEnterCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, int, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, MouseButton, InputAction, KeyModifiers, void> glfwSetMouseButtonCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, MouseButton, InputAction, KeyModifiers, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, double, double, void> glfwSetScrollCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, double, double, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, Keys, int, InputAction, KeyModifiers, void> glfwSetKeyCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, Keys, int, InputAction, KeyModifiers, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, uint, void> glfwSetCharCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, uint, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, void> glfwSetWindowCloseCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, int, int, void> glfwSetWindowPosCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, int, int, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, int, int, void> glfwSetWindowSizeCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, int, int, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Monitor*, ConnectedState, void> glfwSetMonitorCallback(delegate* unmanaged[Cdecl]<Monitor*, ConnectedState, void> callback);

    [DllImport(LibraryName)]
    public static extern void glfwGetMonitorWorkarea(Monitor* monitor, out int xpos, out int ypos, out int width, out int height);
}
