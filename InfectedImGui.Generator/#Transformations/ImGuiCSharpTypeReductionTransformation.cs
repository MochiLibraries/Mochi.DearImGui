using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.Transformation;
using ClangSharp;
using ClangType = ClangSharp.Type;

namespace InfectedImGui.Generator
{
    public class ImGuiCSharpTypeReductionTransformation : CSharpTypeReductionTransformation
    {
        protected override TypeTransformationResult TransformClangTypeReference(TypeTransformationContext context, ClangTypeReference type)
        {
            if (type.ClangType is TemplateSpecializationType templateSpecializationType
                && context.Library.FindClangCursor(templateSpecializationType.Handle.Declaration) is ClassTemplateSpecializationDecl templateSpecialization
                && templateSpecialization.Spelling == "ImVector")
            {
                if (templateSpecialization.TemplateArgs.Count != 1)
                {
                    TypeTransformationResult result = type;
                    result.AddDiagnostic(Severity.Error, "ImVector should have exactly one template argument.");
                    return result;
                }

                ClangType elementType = templateSpecialization.TemplateArgs[0];
                return new ImVectorTypeReference(new ClangTypeReference(elementType));
            }
            else
            { return base.TransformClangTypeReference(context, type); }
        }
    }
}
