using Biohazrd;
using Biohazrd.Transformation;
using System.Collections.Immutable;

namespace InfectedImGui.Generator
{
    public sealed class ImGuiEnumTransformation : TransformationBase
    {
        //TODO: Associate typedefs
        protected override TransformationResult TransformEnum(TransformationContext context, TranslatedEnum declaration)
        {
            string name = declaration.Name;

            // By convention, ImGui enums end with an underscore and they have a corresponding typedef to make them explicit int-sized.
            if (name.EndsWith('_'))
            {
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
                };
            }

            return base.TransformEnum(context, declaration);
        }
    }
}
