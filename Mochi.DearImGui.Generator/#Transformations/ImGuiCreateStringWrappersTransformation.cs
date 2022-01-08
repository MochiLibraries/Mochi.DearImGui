using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.Transformation;
using ClangSharp;
using System.Collections.Immutable;

namespace Mochi.DearImGui.Generator
{
    public sealed class ImGuiCreateStringWrappersTransformation : TransformationBase
    {
        private bool ShouldOptOut(TranslatedFunction declaration)
        {
            switch (declaration.Name)
            {
                // Skip PushID(const char*) and prefer PushID(const char*, const char*)
                case "PushID":
                // Skip GetID(const char*) and prefer GetID(const char*, const char*)
                case "GetID":
                    return declaration.Parameters.Length == 1;
                // This function takes a string, but not one that's written by humans.
                // If we were to call this from C# we'd want to actually pass in a ReadOnlySpan<byte> or something.
                case "AddFontFromMemoryCompressedBase85TTF":
                    return true;
                // There is a UTF16 equivalent of this function, so it doesn't make sense to generate a wrapper
                case "AddInputCharactersUTF8":
                    return true;
                default:
                    return false;
            }
        }

        // This transformation is an opt-out heuristic
        // It'd be not-ideal if it was wrong, but it's expected that the user will be smart enough to avoid calling the nonsense overload
        // In the long term we'll rely on a parameter being explicitly marked as a string once https://github.com/ocornut/imgui/pull/3038 is merged
        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            // Opt out of specific functions that shouldn't get overloads
            if (ShouldOptOut(declaration))
            { return declaration; }

            // Check if this function has a string parameter
            ImmutableArray<int>.Builder? stringParameters = null;
            bool firstParameterHasEnd = false;

            for (int i = 0; i < declaration.Parameters.Length; i++)
            {
                TranslatedParameter parameter = declaration.Parameters[i];

                // If the parameter is a `byte*`, it's a string parameter
                if (parameter.Type is PointerTypeReference { Inner: CSharpBuiltinTypeReference cSharpPointee } && cSharpPointee == CSharpBuiltinType.Byte)
                {
                    // If the parameter was not const, skip it
                    //TODO: C#10: This mess could be improved by the new pattern matching enhancements
                    if (parameter.Original is not TranslatedParameter { Type: ClangTypeReference { ClangType: { CanonicalType: PointerType { PointeeType: { IsLocalConstQualified: true } } } } })
                    { continue; }

                    if (stringParameters is null)
                    { stringParameters = ImmutableArray.CreateBuilder<int>(initialCapacity: 1); }

                    // If this parameter ends with `_end` and the previous parameter was the first string, we treat this as a string end parameter
                    if (stringParameters.Count == 1 && stringParameters[0] == (i - 1) && parameter.Name.EndsWith("_end"))
                    {
                        firstParameterHasEnd = true;
                        continue;
                    }

                    stringParameters.Add(i);
                }
            }

            // Add a string helper overload if appropriate
            TransformationResult result = declaration;

            if (stringParameters is not null)
            {
                result = result.Add(new ImGuiStringFunctionWrapperDeclaration(declaration, stringParameters.MoveToImmutableSafe())
                {
                    ParameterFollowingFirstIsStringEnd = firstParameterHasEnd
                });
            }

            return result;
        }
    }
}
