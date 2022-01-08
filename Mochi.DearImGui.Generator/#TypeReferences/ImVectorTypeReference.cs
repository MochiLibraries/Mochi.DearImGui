using Biohazrd;
using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Transformation;
using Biohazrd.Transformation.Infrastructure;
using System.Diagnostics;

namespace Mochi.DearImGui.Generator
{
    public sealed record ImVectorTypeReference : TypeReference, ICustomTypeReference, ICustomCSharpTypeReference
    {
        public TypeReference ElementType { get; init; }

        public ImVectorTypeReference(TypeReference elementType)
            => ElementType = elementType;

        string ICustomCSharpTypeReference.GetTypeAsString(ICSharpOutputGenerator outputTranslator, VisitorContext context, TranslatedDeclaration declaration)
        {
            // A long-standing bug in the runtime means structs cannot use generic types with themselves as a type parameter
            // This is a dirty hack to work around that issue
            // https://github.com/dotnet/runtime/issues/6924
            if (declaration.Name is "ChildWindows" && context.ParentDeclaration?.Name is "ImGuiWindowTempData")
            {
                Debug.Assert(outputTranslator.GetTypeAsString(context, declaration, ElementType) == "ImGuiWindow*");
                outputTranslator.AddUsing("Mochi.DearImGui.Internal");
                return "ImGuiWindowVector";
            }

            // Get the string for the element type
            // We have to wrap pointers in a special Pointer<T> type because C# doesn't support pointers in generics.
            int levelsOfIndirection = 0;
            TypeReference elementType = ElementType;

            while (elementType is PointerTypeReference pointerType)
            {
                levelsOfIndirection++;
                elementType = pointerType.Inner;
            }

            string elementTypeString;
            if (elementType is VoidTypeReference && levelsOfIndirection > 0)
            {
                elementTypeString = "nint";
                levelsOfIndirection--;
            }
            else
            { elementTypeString = outputTranslator.GetTypeAsString(context, declaration, elementType); }

            for (int i = 0; i < levelsOfIndirection; i++)
            {
                outputTranslator.AddUsing("Mochi.DearImGui.Infrastructure"); // Pointer<T>
                elementTypeString = $"Pointer<{elementTypeString}>";
            }

            // Return the type string
            outputTranslator.AddUsing("Mochi.DearImGui"); // ImVector<T>
            return $"ImVector<{elementTypeString}>";
        }

        TypeTransformationResult ICustomTypeReference.TransformChildren(ITypeTransformation transformation, TypeTransformationContext context)
        {
            DiagnosticAccumulator diagnostics = new();
            SingleTypeTransformHelper newElementType = new(ElementType, ref diagnostics);

            // Transform element type
            newElementType.SetValue(transformation.TransformTypeRecursively(context, ElementType));

            // Create the result
            TypeTransformationResult result;

            if (newElementType.WasChanged)
            {
                result = this with
                {
                    ElementType = newElementType.NewValue
                };
            }
            else
            { result = this; }

            // Add any diagnostics to the result
            result.AddDiagnostics(diagnostics.MoveToImmutable());

            // Return the result
            return result;
        }
    }
}
