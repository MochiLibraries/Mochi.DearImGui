using Biohazrd;
using Biohazrd.Transformation;
using ClangSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Mochi.DearImGui.Generator
{
    internal sealed class ImGuiInternalFixupTransformation : TransformationBase
    {
        private readonly List<TranslationDiagnostic> MiscDiagnostics = new();
        private TranslatedFile? ImGuiInternalFile;

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            Debug.Assert(MiscDiagnostics.Count == 0);
            MiscDiagnostics.Clear();

            ImGuiInternalFile = library.Files.FirstOrDefault(f => Path.GetFileName(f.FilePath) == "imgui_internal.h");

            return library;
        }

        protected override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
        {
            if (declaration.File != ImGuiInternalFile)
            { return declaration; }

            // This typedef is defined as FILE*, which we don't have in C# and it doesn't really make sense to translate it as such
            //TODO: Translate this as an opaque pointer
            if (declaration.Name == "ImFileHandle")
            {
                return declaration with
                {
                    UnderlyingType = VoidTypeReference.PointerInstance
                };
            }

            return declaration;
        }

        protected override TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
        {
            if (declaration.File != ImGuiInternalFile)
            { return declaration; }

            string MakeDiagnosticMessage<T>(T type)
            {
                string friendlyName = declaration.Name;

                for (int i = context.Parents.Length - 1; i >= 0; i--)
                {
                    TranslatedDeclaration parent = context.Parents[i];
                    friendlyName = $"{parent.Name}.{friendlyName}";

                    if (i == 0 && parent.Namespace is not null)
                    { friendlyName = $"{parent.Namespace}.{friendlyName}"; }
                }

                return $"Field '{friendlyName}' was removed since it references '{type}', which is currently unsupported.";
            }

            // Special case: This field is missed with the logic below because it uses a typedef
            if (declaration.Name == "ActiveIdUsingKeyInputMask" && context.ParentDeclaration?.Name == "ImGuiContext")
            {
                MiscDiagnostics.Add(Severity.Warning, MakeDiagnosticMessage(declaration.Type));
                return null;
            }

            ClangTypeReference? clangType;

            switch (declaration.Type)
            {
                case ClangTypeReference clangTypeReference:
                    clangType = clangTypeReference;
                    break;
                case TranslatedTypeReference translatedTypeReference when translatedTypeReference.TryResolve(context.Library) is null:
                    clangType = (declaration.Original as TranslatedNormalField)?.Type as ClangTypeReference;
                    break;
                default:
                    clangType = null;
                    break;
            }

            if (clangType is not null)
            {
                MiscDiagnostics.Add(Severity.Warning, MakeDiagnosticMessage(clangType));
                return null;
            }
            else
            { return declaration; }
        }

        protected override TransformationResult TransformUnsupportedDeclaration(TransformationContext context, TranslatedUnsupportedDeclaration declaration)
        {
            if (declaration.File != ImGuiInternalFile)
            { return declaration; }

            // imgui_internal has a handful of templated functions, which are not easily supported by Biohazrd.
            // These functions are generally just generic math implementations, so we could special-case them if they're deemed to be necessary.
            if (declaration.Declaration is FunctionTemplateDecl)
            {
                switch (declaration.Name)
                {
                    case "ImMin":
                    case "ImMax":
                    case "ImClamp":
                    case "ImLerp":
                    case "ImSwap":
                    case "ImAddClampOverflow":
                    case "ImSubClampOverflow":
                    case "ScaleRatioFromValueT":
                    case "ScaleValueFromRatioT":
                    case "DragBehaviorT":
                    case "SliderBehaviorT":
                    case "RoundScalarWithFormatT":
                    case "CheckboxFlagsT":
                        return null;
                    default:
                        MiscDiagnostics.Add
                        (
                            Severity.Warning,
                            $"Unrecognized templated function '{declaration.Name}' was removed. It should probably be added to {nameof(ImGuiInternalFixupTransformation)}."
                        );
                        return null;
                }
            }
            // imgui_internal has a handful of templated types and functions, which are not easily supported by Biohazrd.
            // A few of these actually appear on the internal API surface, so we might want to translate them eventually.
            // I didn't manually translate them as I might decide to use them to experiment with Biohazrd's template support eventually.
            // Plus these internal APIs should generally not be used anyways, so it's not really worth putting a ton of effort into them.
            else if (declaration.Declaration is ClassTemplateDecl)
            {
                switch (declaration.Name)
                {
                    // These two are helper types which really should probably never be used from C#
                    case "ImSpanAllocator":
                    case "ImBitArray":
                    // These three actually appear on ImGui's internal API surface.
                    // Not translating them for now, will visit translated them as needed.
                    case "ImChunkStream":
                    case "ImPool":
                    case "ImSpan":
                        return null;
                    default:
                        MiscDiagnostics.Add
                        (
                            Severity.Warning,
                            $"Unrecognized templated class '{declaration.Name}' was removed. It should probably be added to {nameof(ImGuiInternalFixupTransformation)}."
                        );
                        return null;
                }
            }

            return declaration;
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            if (MiscDiagnostics.Count > 0)
            {
                library = library with
                {
                    ParsingDiagnostics = library.ParsingDiagnostics.AddRange(MiscDiagnostics)
                };
            }

            return library;
        }
    }
}
