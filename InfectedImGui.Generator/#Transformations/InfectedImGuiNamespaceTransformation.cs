using Biohazrd;
using Biohazrd.Transformation;

namespace InfectedImGui.Generator
{
    internal sealed class InfectedImGuiNamespaceTransformation : TransformationBase
    {
        protected override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
            //TODO
            => declaration with { Namespace = null };
    }
}
