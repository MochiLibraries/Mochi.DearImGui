namespace Mochi.DearImGui.Infrastructure
{
    public unsafe readonly struct Pointer<T>
        where T : unmanaged
    {
        private readonly T* Value;

        private Pointer(T* value)
            => Value = value;

        public static implicit operator T*(Pointer<T> pointer)
            => pointer.Value;

        public static implicit operator Pointer<T>(T* pointer)
            => new Pointer<T>(pointer);
    }
}
