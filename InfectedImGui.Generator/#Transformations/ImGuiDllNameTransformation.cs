using Biohazrd;
using Biohazrd.Transformation;

namespace InfectedImGui.Generator
{
    internal sealed class ImGuiDllNameTransformation : TransformationBase
    {
        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
            => declaration with
            {
                DllFileName = "InfectedImGui.Native.dll"
            };
    }
}
