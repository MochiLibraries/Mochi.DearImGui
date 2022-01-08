using Biohazrd;
using Biohazrd.Transformation;

namespace Mochi.DearImGui.Generator
{
    public sealed class RemoveUnneededDeclarationsTransformation : TransformationBase
    {
        protected override TransformationResult TransformUnsupportedDeclaration(TransformationContext context, TranslatedUnsupportedDeclaration declaration)
        {
            switch (declaration.Name)
            {
                // IM_DELETE is (probably?) only necessary if you manually used IM_NEW
                case "IM_DELETE":
                // ImVector<T> can't be translated by Biohazrd easily so it is translated manually
                case "ImVector":
                    return null;
                default:
                    return declaration;
            }
        }
    }
}
