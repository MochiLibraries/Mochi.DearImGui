using System.Numerics;

namespace Mochi.DearImGui;

partial class ImGui
{
    public unsafe static void CHECKVERSION()
        => DebugCheckVersionAndDataLayout(IMGUI_VERSION, (ulong)sizeof(ImGuiIO), (ulong)sizeof(ImGuiStyle), (ulong)sizeof(Vector2), (ulong)sizeof(Vector4), (ulong)sizeof(ImDrawVert), sizeof(ushort));
}
