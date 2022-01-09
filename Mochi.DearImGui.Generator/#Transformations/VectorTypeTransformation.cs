using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.Transformation;
using System;
using System.Diagnostics;

namespace Mochi.DearImGui.Generator
{
    internal sealed class VectorTypeTransformation : CSharpTransformationBase
    {
        protected override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
        {
            if (declaration.Name is "ImVec4" or "ImColor")
            {
                Debug.Assert(declaration.Size == sizeof(float) * 4);
                return new ExternallyDefinedTypeDeclaration("Vector4", declaration)
                {
                    Namespace = "System.Numerics"
                };
            }
            else if (declaration.Name is "ImVec2")
            {
                Debug.Assert(declaration.Size == sizeof(float) * 2);
                return new ExternallyDefinedTypeDeclaration("Vector2", declaration)
                {
                    Namespace = "System.Numerics"
                };
            }
            else if (declaration.Name is "ImVec1")
            {
                Debug.Assert(declaration.Size == sizeof(float));
                //TODO: Would be nice if we could just synthesize a typedef here.
                return new ExternallyDefinedTypeDeclaration("float", declaration);
            }
            else if (declaration.Name is "ImVec2ih")
            {
                Debug.Assert(declaration.Size == sizeof(short) * 2);
                return new ExternallyDefinedTypeDeclaration("(short x, short y)", declaration);
            }
            else
            { return declaration; }
        }

        protected override TransformationResult TransformConstantArrayType(TransformationContext context, ConstantArrayTypeDeclaration declaration)
        {
            if (declaration.ElementCount is >= 2 and <= 4 && declaration.Type is CSharpBuiltinTypeReference { Type: CSharpBuiltinType cSharpType })
            {
                if (cSharpType == CSharpBuiltinType.Float)
                {
                    return new ExternallyDefinedTypeDeclaration($"Vector{declaration.ElementCount}", declaration)
                    {
                        Namespace = "System.Numerics"
                    };
                }
                else if (cSharpType == CSharpBuiltinType.Bool
                    || cSharpType == CSharpBuiltinType.Int
                    || cSharpType == CSharpBuiltinType.UInt)
                {
                    string tuple = $"({cSharpType.CSharpKeyword} x";
                    for (int i = 1; i < declaration.ElementCount; i++)
                    {
                        char elementName = i switch
                        {
                            1 => 'y',
                            2 => 'z',
                            3 => 'w',
                            _ => throw new Exception("Branch thought to be unreachable.")
                        };
                        tuple += $", {cSharpType.CSharpKeyword} {elementName}";
                    }
                    tuple += ")";

                    return new ExternallyDefinedTypeDeclaration(tuple, declaration);
                }
            }

            return declaration;
        }
    }
}
