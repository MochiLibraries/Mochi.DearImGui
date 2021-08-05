using System.Runtime.InteropServices;

namespace InfectedImGui.Sample.Linux
{
    public static unsafe class Gl3w
    {
        private const string DllName = "InfectedImGui.Native";

        [DllImport(DllName)] public static extern int gl3wInit();
    }
}
