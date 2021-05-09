using Biohazrd;
using Biohazrd.Transformation;
using ClangSharp;
using ClangType = ClangSharp.Type;

namespace InfectedImGui.Generator
{
    // This transformation is a workaround for https://github.com/InfectedLibraries/Biohazrd/issues/147
    internal class __HACK__FixupFunctionPointerReturnTypes : TransformationBase
    {
        protected override TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
        {
            if (declaration.Type is not FunctionPointerTypeReference functionPointer)
            { return declaration; }

            if (declaration.Original is not TranslatedNormalField originalField)
            { return declaration; }

            if (originalField.Type is not ClangTypeReference clangTypeReference)
            { return declaration; }

            ClangType clangType = clangTypeReference.ClangType.CanonicalType;

            if (clangType is PointerType pointerType)
            { clangType = pointerType.PointeeType; }

            if (clangType is not FunctionProtoType clangFunctionPointerType)
            { return declaration; }

            // Is isForInstanceMethodReturnValue misnamed? ImVec2 is not getting picked up when it is false.
            if (!clangFunctionPointerType.ReturnType.MustBePassedByReference(isForInstanceMethodReturnValue: true))
            { return declaration; }

            PointerTypeReference returnBufferType = new(functionPointer.ReturnType);
            return declaration with
            {
                Type = functionPointer with
                {
                    ReturnType = returnBufferType,
                    ParameterTypes = functionPointer.ParameterTypes.Insert(0, returnBufferType)
                }
            };
        }
    }
}
