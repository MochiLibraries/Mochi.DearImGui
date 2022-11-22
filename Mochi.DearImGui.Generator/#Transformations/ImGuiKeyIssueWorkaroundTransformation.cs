using Biohazrd;
using Biohazrd.Transformation;

namespace Mochi.DearImGui.Generator;

// This is a temporary workaround until https://github.com/MochiLibraries/Mochi.DearImGui/issues/6 can be properly investigated.
internal sealed class ImGuiKeyIssueWorkaroundTransformation : TransformationBase
{
    protected override TransformationResult TransformEnum(TransformationContext context, TranslatedEnum declaration)
    {
        if (declaration.Name == "ImGuiKey" && declaration.Values.Count == 0)
        { return null; }

        return declaration;
    }

    protected override TransformationResult TransformEnumConstant(TransformationContext context, TranslatedEnumConstant declaration)
    {
        const string prefix = "ImGuiKey_";
        if (context.ParentDeclaration is { Name: "ImGuiKey" } && declaration.Name.Length > prefix.Length && declaration.Name.StartsWith(prefix))
        {
            string newName = declaration.Name.Substring(prefix.Length);

            if (newName[0] is >= '0' and <= '9')
            { newName = $"_{newName}"; }

            return declaration with { Name = newName };
        }

        return declaration;
    }
}
