using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Transformation;
using Biohazrd.Transformation.Infrastructure;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Mochi.DearImGui.Generator
{
    internal sealed record ImGuiStringFunctionWrapperDeclaration : TranslatedDeclaration, ICustomTranslatedDeclaration, ICustomCSharpTranslatedDeclaration
    {
        public DeclarationReference WrappedFunction { get; init; }
        public ImmutableArray<int> StringParameterIndices { get; init; }
        public bool ParameterFollowingFirstIsStringEnd { get; init; } = false;

        public ImGuiStringFunctionWrapperDeclaration(TranslatedFunction targetFunction, ImmutableArray<int> stringParameterIndices)
            : base(targetFunction.File)
        {
            if (stringParameterIndices.Length <= 0)
            { throw new ArgumentException("There must be at least one string parameter index."); }
            else if (stringParameterIndices.Any(i => i < 0 || i >= targetFunction.Parameters.Length))
            { throw new ArgumentException("One or more stirng parameter indicies is invalid."); }

            WrappedFunction = new DeclarationReference(targetFunction);
            StringParameterIndices = stringParameterIndices;

            Accessibility = targetFunction.Accessibility;
            Name = $"{targetFunction.Name}__StringWrapper{Id}"; // This is a dummy name to avoid being deduplicated
        }

        TransformationResult ICustomTranslatedDeclaration.TransformChildren(ITransformation transformation, TransformationContext context)
            => this;

        TransformationResult ICustomTranslatedDeclaration.TransformTypeChildren(ITypeTransformation transformation, TransformationContext context)
            => this;

        void ICustomCSharpTranslatedDeclaration.GenerateOutput(ICSharpOutputGenerator outputGenerator, VisitorContext context, CSharpCodeWriter writer)
        {
            // Resolve our function
            if (WrappedFunction.TryResolve(context.Library) is not TranslatedFunction function)
            {
                //TODO: Can't emit emit diagnostics from custom declarations
                writer.EnsureSeparation();
                writer.WriteLine($"// Failed to resolve {Name} via {WrappedFunction} to generate generic interface out method.");
                return;
            }

            // Automated naming conventions
            static string GetParameterBufferName(TranslatedParameter parameter)
                => SanitizeIdentifier($"__{parameter.Name}");

            static string GetParameterPointerName(TranslatedParameter parameter)
                => SanitizeIdentifier($"__{parameter.Name}P");

            const string temporaryReturnValueName = "__result";

            // Write out the function signature
            VisitorContext functionContext = context.MakePrevious().Add(function);
            VisitorContext parameterContext = functionContext.Add(function);

            writer.EnsureSeparation();
            writer.Using("System"); // Span<T>, ReadOnlySpan<T>
            writer.Using("System.Diagnostics"); // DebuggerStepThroughAttribute, DebuggerHiddenAttribute
            writer.Using("Mochi.DearImGui"); // DearImGuiInterpolatedStringHandler
            const string interpolatedStringHandlerType = "DearImGuiInterpolatedStringHandler";

            string? constructorName;
            if (function.SpecialFunctionKind == SpecialFunctionKind.Constructor && context.ParentDeclaration is TranslatedRecord constructorType)
            {
                constructorName = constructorType.Name;
                writer.Write($"{Accessibility.ToCSharpKeyword()} ");
                writer.WriteIdentifier(constructorName);
            }
            else
            {
                constructorName = null;
                writer.Write($"{Accessibility.ToCSharpKeyword()} ");
                if (!function.IsInstanceMethod)
                { writer.Write("static "); }
                writer.Write($"{outputGenerator.GetTypeAsString(functionContext, function, function.ReturnType)} ");
                writer.WriteIdentifier(function.Name);
            }
            writer.Write('(');

            ImmutableArray<int> sortedStringParameterIndices = StringParameterIndices.Sort();

            void WriteOutParameters(bool forArguments)
            {
                int nextStringParameterIndex = sortedStringParameterIndices[0];
                int nextStringParameterIndexIndex = 1;

                for (int i = 0; i < function.Parameters.Length; i++)
                {
                    if (i > 0)
                    { writer.Write(", "); }

                    TranslatedParameter parameter = function.Parameters[i];

                    if (i == nextStringParameterIndex)
                    {
                        bool isFirstStringParameter = nextStringParameterIndexIndex == 1;

                        if (!forArguments)
                        {
                            writer.Write($"{interpolatedStringHandlerType} {SanitizeIdentifier(parameter.Name)}");
                        }
                        else
                        { writer.Write(GetParameterPointerName(parameter)); }

                        // Skip the next parameter in the case that it's a end parameter
                        if (isFirstStringParameter && ParameterFollowingFirstIsStringEnd)
                        {
                            if (forArguments)
                            {
                                writer.Write($", {GetParameterPointerName(parameter)} + {GetParameterBufferName(parameter)}.Length");
                            }

                            i++;
                        }

                        // Update state for detecting the next string parameter
                        if (nextStringParameterIndexIndex >= sortedStringParameterIndices.Length)
                        { nextStringParameterIndex = -1; }
                        else
                        {
                            nextStringParameterIndex = sortedStringParameterIndices[nextStringParameterIndexIndex];
                            nextStringParameterIndexIndex++;
                            Debug.Assert(nextStringParameterIndex > i, "The string parameter indices list must not contain duplicates!");
                        }
                    }
                    else
                    {
                        if (!forArguments)
                        {
                            writer.Write(outputGenerator.GetTypeAsString(parameterContext, parameter, parameter.Type));

                            //TODO: It seems like ideally we shouldn't have to worry about this here...
                            if (parameter.ImplicitlyPassedByReference)
                            { writer.Write('*'); }

                            writer.Write(' ');
                        }

                        writer.WriteIdentifier(parameter.Name);

                        // Don't emit defaults before the final string parameter since they won't be valid.
                        // --------------------------------------------------------------------------------------------
                        // In theory we could emit this for string parameters too and avoid this condition, but:
                        // A) We use ReadOnlySpan<char> to allow callers to use spans, so we'd need to generate a string overload too.
                        // B) We'd need to support nullable strings
                        // C) We'd have to add logic to pevent creating fully ambiguous overloads
                        //TODO: We should add this capability eventually though since it'd be nice to be able to allow default string arguments such as the ones used in SliderFloat.
                        if (nextStringParameterIndex < 0)
                        {
                            if (!forArguments && parameter.DefaultValue is not null)
                            { writer.Write($" = {outputGenerator.GetConstantAsString(parameterContext, parameter, parameter.DefaultValue, parameter.Type)}"); }
                        }
                    }
                }
            }

            WriteOutParameters(forArguments: false);
            writer.WriteLine(')');

            using (writer.Block())
            {
                // Allocate temporary for the return value if necessary
                if (function.ReturnType is not VoidTypeReference)
                { writer.WriteLine($"{outputGenerator.GetTypeAsString(functionContext, function, function.ReturnType)} {temporaryReturnValueName};"); }

                // Null terminate and fetch the strings
                for (int ii = 0; ii < sortedStringParameterIndices.Length; ii++)
                {
                    TranslatedParameter parameter = function.Parameters[sortedStringParameterIndices[ii]];
                    writer.WriteLine($"ReadOnlySpan<byte> {GetParameterBufferName(parameter)} = {SanitizeIdentifier(parameter.Name)}.NullTerminateAndGetString();");
                }

                writer.WriteLine();

                // Pin the buffers
                for (int ii = 0; ii < sortedStringParameterIndices.Length; ii++)
                {
                    TranslatedParameter parameter = function.Parameters[sortedStringParameterIndices[ii]];
                    writer.WriteLine($"fixed (byte* {GetParameterPointerName(parameter)} = {GetParameterBufferName(parameter)})");
                }

                using (writer.Block())
                {
                    if (constructorName is not null)
                    { writer.Write($"this = new {SanitizeIdentifier(constructorName)}"); }
                    else
                    {
                        if (function.ReturnType is not VoidTypeReference)
                        { writer.Write($"{temporaryReturnValueName} = "); }

                        writer.WriteIdentifier(function.Name);
                    }
                    writer.Write('(');
                    WriteOutParameters(forArguments: true);
                    writer.WriteLine(");");
                }

                // Dispose of the interpolated strings' buffers
                // We don't bother using a try..finally block for this, it's not critical that the buffers are returned to the pool
                writer.WriteLine();
                for (int ii = 0; ii < sortedStringParameterIndices.Length; ii++)
                {
                    TranslatedParameter parameter = function.Parameters[sortedStringParameterIndices[ii]];
                    writer.WriteLine($"{SanitizeIdentifier(parameter.Name)}.Dispose();");
                }

                // Return the result if necessary
                if (function.ReturnType is not VoidTypeReference)
                {
                    writer.WriteLine();
                    writer.WriteLine($"return {temporaryReturnValueName};");
                }
            }
        }
    }
}
