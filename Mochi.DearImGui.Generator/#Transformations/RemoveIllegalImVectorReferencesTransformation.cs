using Biohazrd;
using Biohazrd.Transformation;

namespace Mochi.DearImGui.Generator
{
    internal sealed class RemoveIllegalImVectorReferencesTransformation : TransformationBase
    {
        protected override TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
        {
            // Remove any ImVector<T> field which references a forward-defined record.
            if (declaration.Type is ImVectorTypeReference { ElementType: TranslatedTypeReference elementType } && elementType.TryResolve(context.Library) is TranslatedUndefinedRecord)
            { return null; }
            else
            { return declaration; }
        }
    }
}
