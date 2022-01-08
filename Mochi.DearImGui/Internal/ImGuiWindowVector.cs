namespace Mochi.DearImGui.Internal
{
    // This type exists as a workaround for https://github.com/dotnet/runtime/issues/6924
    // Ideally we'd just use ImVector<Pointer<ImGuiWindow>> like normal.
    public unsafe struct ImGuiWindowVector
    {
        public int Size;
        public int Capacity;
        public ImGuiWindow** Data;
    }
}
