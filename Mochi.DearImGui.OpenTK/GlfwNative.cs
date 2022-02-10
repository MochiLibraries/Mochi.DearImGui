using OpenTK.Windowing.GraphicsLibraryFramework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Mochi.DearImGui.OpenTK;

// OpenTK exposes a more managed view of the GLFW callbacks API.
// It's easier for us to use C#9 function pointers instead so we re-expose relevant methods
// We also expose a few methods which are present in OpenTK's distribution of GLFW but aren't exposed from C#.
internal unsafe sealed class GlfwNative
{
    private const string LibraryName = "glfw3.dll";
    private static readonly IntPtr GlfwHandle;

    static GlfwNative()
    {
        GlfwHandle = LoadLibrary("glfw", new Version(3, 3), typeof(GlfwNative).Assembly, null);
        NativeLibrary.SetDllImportResolver(typeof(GlfwNative).Assembly, (name, assembly, path) => name == LibraryName ? GlfwHandle : IntPtr.Zero);

        // This is copy+pasted from OpenTK so that we resolve GLFW using the same logic it does
        // https://github.com/opentk/opentk/blob/273348c5e8a5f8a602bdfc93c0f28aca3474049c/src/OpenTK.Windowing.GraphicsLibraryFramework/GLFWNative.cs#L31-L70
        static IntPtr LoadLibrary(string libraryName, Version version, Assembly assembly, DllImportSearchPath? searchPath)
        {
            IEnumerable<string> GetNextVersion()
            {
                for (var i = 2; i >= 0; i--)
                {
                    yield return version.ToString(i);
                }
            }

            Func<string, string, string> libNameFormatter;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libNameFormatter = (libName, ver) =>
                    libName + ".so" + (string.IsNullOrEmpty(ver) ? string.Empty : "." + ver);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libNameFormatter = (libName, ver) =>
                    libName + (string.IsNullOrEmpty(ver) ? string.Empty : "." + ver) + ".dylib";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libNameFormatter = (libName, ver) =>
                    libName + (string.IsNullOrEmpty(ver) ? string.Empty : ver) + ".dll";
            }
            else
            {
                return IntPtr.Zero;
            }

            foreach (string ver in GetNextVersion())
            {
                if (NativeLibrary.TryLoad(libNameFormatter(libraryName, ver), assembly, searchPath, out var handle))
                {
                    return handle;
                }
            }

            return NativeLibrary.Load(libraryName, assembly, searchPath);
        }
    }

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<ErrorCode, byte*, void> glfwSetErrorCallback(delegate* unmanaged[Cdecl]<ErrorCode, byte*, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, int, void> glfwSetWindowFocusCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, int, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, int, void> glfwSetCursorEnterCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, int, void> callback);

    [DllImport(LibraryName)]
    public static extern delegate* unmanaged[Cdecl]<Window*, double, double, void> glfwSetCursorPosCallback(Window* window, delegate* unmanaged[Cdecl]<Window*, double, double, void> callback);

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
