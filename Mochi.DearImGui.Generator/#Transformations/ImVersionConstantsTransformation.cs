using Biohazrd;
using Biohazrd.Expressions;
using Biohazrd.Transformation;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Mochi.DearImGui.Generator
{
    internal sealed class ImVersionConstantsTransformation : TransformationBase
    {
        private readonly TranslatedConstant? IMGUI_VERSION;
        private readonly TranslatedConstant? IMGUI_VERSION_NUM;

        public ImVersionConstantsTransformation(TranslatedLibrary library, TranslatedLibraryConstantEvaluator constantEvaluator)
        {
            //TODO: Handle the macros missing
            TranslatedMacro[] macros = new[]
            {
                library.Macros.First(m => m.Name == nameof(IMGUI_VERSION)),
                library.Macros.First(m => m.Name == nameof(IMGUI_VERSION_NUM))
            };

            ImmutableArray<ConstantEvaluationResult> result = constantEvaluator.EvaluateBatch(macros);

            Debug.Assert(result.Length == 2);
            IMGUI_VERSION = new TranslatedConstant(nameof(IMGUI_VERSION), result[0]);
            IMGUI_VERSION_NUM = new TranslatedConstant(nameof(IMGUI_VERSION_NUM), result[1]);
        }

        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            TransformationResult result = declaration;

            if (declaration.Name == "DebugCheckVersionAndDataLayout")
            {
                if (IMGUI_VERSION is not null)
                { result.Add(IMGUI_VERSION); }

                if (IMGUI_VERSION_NUM is not null)
                { result.Add(IMGUI_VERSION_NUM); }
            }

            return result;
        }
    }
}
