using InfectedImGui.Backends.Glfw;
using System.Runtime.InteropServices;

namespace InfectedImGui.Sample.Linux
{
    public static unsafe class Glfw
    {
        private const string DllName = "glfw";

        [DllImport(DllName)] public static extern delegate* unmanaged<int, byte*, void> glfwSetErrorCallback(delegate* unmanaged<int, byte*, void> cbfun);
        [DllImport(DllName)] public static extern int glfwInit();
        [DllImport(DllName)] public static extern void glfwWindowHint(Hint hint, int value);
        [DllImport(DllName)] public static extern GLFWwindow* glfwCreateWindow(int width, int height, byte* title, GLFWmonitor* monitor, GLFWwindow* share);
        [DllImport(DllName)] public static extern void glfwMakeContextCurrent(GLFWwindow* window);
        [DllImport(DllName)] public static extern GLFWwindow* glfwGetCurrentContext();
        [DllImport(DllName)] public static extern void glfwSwapInterval(int interval);
        [DllImport(DllName)] public static extern int glfwWindowShouldClose(GLFWwindow* window);
        [DllImport(DllName)] public static extern void glfwPollEvents();
        [DllImport(DllName)] public static extern void glfwGetFramebufferSize(GLFWwindow* window, int* width, int* height);
        [DllImport(DllName)] public static extern void glfwSwapBuffers(GLFWwindow* window);
        [DllImport(DllName)] public static extern void glfwDestroyWindow(GLFWwindow* window);
        [DllImport(DllName)] public static extern void glfwTerminate();


        public enum Hint : int
        {
            GLFW_CONTEXT_VERSION_MAJOR = 0x00022002,
            GLFW_CONTEXT_VERSION_MINOR = 0x00022003,
            GLFW_OPENGL_PROFILE         = 0x00022008,
            GLFW_OPENGL_FORWARD_COMPAT  = 0x00022006,
        }

        public const int GLFW_OPENGL_CORE_PROFILE = 0x00032001;
        public const int GLFW_FALSE = 0;
        public const int GLFW_TRUE = 1;
    }
}
