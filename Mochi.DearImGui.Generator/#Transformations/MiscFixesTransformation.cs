using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.Transformation;
using System.Diagnostics;

namespace Mochi.DearImGui.Generator
{
    internal sealed class MiscFixesTransformation : TransformationBase
    {
        protected override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
        {
            // ImWchar16 is defined as being ushort, but it makes more sense for it to be a C# char.
            if (declaration.Name is "ImWchar16")
            {
                Debug.Assert(declaration.UnderlyingType == CSharpBuiltinType.UShort);
                return declaration with
                {
                    UnderlyingType = CSharpBuiltinType.Char
                };
            }
            // ImWchar32 is defined as being uint, but it makes more sense for it to be a .NET System.Rune.
            // (In theory System.Rune is slightly different since it should only be used with valid unicode characters, but I think this is probably fine.)
            else if (declaration.Name is "ImWchar32")
            {
                Debug.Assert(declaration.UnderlyingType == CSharpBuiltinType.UInt);
                return declaration with
                {
                    UnderlyingType = new ExternallyDefinedTypeReference("System.Text", "Rune")
                };
            }
            else
            { return declaration; }
        }
    }
}
