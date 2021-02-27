using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.OutputGeneration.Metadata;
using Biohazrd.Transformation;
using System;

namespace InfectedImGui.Generator
{
    internal sealed class OrganizeOutputFilesByNamespaceTransformation : TransformationBase
    {
        protected override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
        {
            // This transformation only applies to root declarations
            if (context.ParentDeclaration is not null)
            { return declaration; }

            // This transformation only applies to declarations that are within a namespace
            const string rootNamespace = "InfectedImGui";
            if (declaration.Namespace is null or rootNamespace)
            { return declaration; }

            ReadOnlySpan<char> childNamespace = declaration.Namespace;
            const string rootNamespacePrefix = rootNamespace + ".";
            if (childNamespace.StartsWith(rootNamespacePrefix))
            { childNamespace = childNamespace.Slice(rootNamespacePrefix.Length); }

            string outputPath = "";
            while (true)
            {
                int separatorIndex = childNamespace.IndexOf('.');

                if (separatorIndex != 0) // Skip 0-length namespace segments.
                {
                    ReadOnlySpan<char> part = separatorIndex == -1 ? childNamespace : childNamespace.Slice(0, separatorIndex);
                    outputPath += $"{part.ToString()}/";
                }

                if (separatorIndex == -1)
                { break; }

                childNamespace = childNamespace.Slice(separatorIndex + 1);
            }

            // Add file name (using the existing name if there is one)
            if (declaration.Metadata.TryGet(out OutputFileName existingPlacement))
            { outputPath += existingPlacement.FileName; }
            else
            { outputPath += CSharpCodeWriter.SanitizeIdentifier(declaration.Name); }

            return declaration with
            {
                Metadata = declaration.Metadata.Set(new OutputFileName(outputPath))
            };
        }
    }
}
