using Biohazrd;
using Biohazrd.Transformation;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Mochi.DearImGui.Generator
{
    //TODO: The enums in imgui_internal.h do not follow the same convention as the ones in imgui.h
    public sealed class ImGuiEnumTransformation : TransformationBase
    {
        private Dictionary<TranslatedEnum, TranslatedTypedef> Enums = new(ReferenceEqualityComparer.Instance);
        private Dictionary<TranslatedTypedef, TranslatedEnum> Typedefs = new(ReferenceEqualityComparer.Instance);

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            Debug.Assert(Enums.Count == 0 && Typedefs.Count == 0, "There should be no enums or typedefs at this point.");
            Enums.Clear();
            Typedefs.Clear();

            List<TranslatedEnum> candidateEnums = new();
            List<TranslatedTypedef> candidateTypedefs = new();

            // Find all candidate typedefs and enums in the library
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is TranslatedTypedef typedef)
                { candidateTypedefs.Add(typedef); }
                // By convention, ImGui enums end with an underscore and they have a corresponding typedef to make them explicit int-sized.
                else if (declaration is TranslatedEnum translatedEnum && translatedEnum.Name.EndsWith('_'))
                { candidateEnums.Add(translatedEnum); }
            }

            // Associate the enums and typedefs
            foreach (TranslatedEnum candidateEnum in candidateEnums)
            {
                // Try to find a typedef that matches the enum
                TranslatedTypedef? typedef = candidateTypedefs.FirstOrDefault(t => t.Name == candidateEnum.Name.Substring(0, candidateEnum.Name.Length - 1));

                // If there's no matching typedef, we don't touch this enum
                if (typedef is null)
                { continue; }

                // Log this enum and typedef to be removed
                Enums.Add(candidateEnum, typedef);
                Typedefs.Add(typedef, candidateEnum);
            }

            return base.PreTransformLibrary(library);
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            Enums.Clear();
            Typedefs.Clear();

            return base.PostTransformLibrary(library);
        }

        protected override TransformationResult TransformEnum(TransformationContext context, TranslatedEnum declaration)
        {
            if (Enums.TryGetValue(declaration, out TranslatedTypedef? typedef))
            {
                string name = declaration.Name;

                // Strip the name off of all of the constants
                ImmutableList<TranslatedEnumConstant>.Builder newValues = ImmutableList.CreateBuilder<TranslatedEnumConstant>();

                foreach (TranslatedEnumConstant value in declaration.Values)
                {
                    if (value.Name.StartsWith(name))
                    { newValues.Add(value with { Name = value.Name.Substring(name.Length) }); }
                    else
                    { newValues.Add(value); }
                }

                // Return the modified enum
                return declaration with
                {
                    Name = name.Substring(0, name.Length - 1),
                    Values = newValues.ToImmutable(),
                    // By convention, ImGui flags enums end with "Flags_"
                    IsFlags = name.EndsWith("Flags_"),
                    UnderlyingType = typedef.UnderlyingType,
                    ReplacedDeclarations = ImmutableArray.Create<TranslatedDeclaration>(typedef)
                };
            }

            return base.TransformEnum(context, declaration);
        }

        protected override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
        {
            // Remove the typedef if it will be replaced by an enum
            if (Typedefs.ContainsKey(declaration))
            { return null; }
            else
            { return declaration; }
        }
    }
}
