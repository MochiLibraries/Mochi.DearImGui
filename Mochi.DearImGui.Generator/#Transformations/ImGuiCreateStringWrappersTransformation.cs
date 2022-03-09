using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.CSharp.Trampolines;
using Biohazrd.Transformation;

namespace Mochi.DearImGui.Generator
{
    public sealed class ImGuiCreateStringWrappersTransformation : TransformationBase
    {
        private bool ShouldOptOut(TranslatedFunction declaration)
        {
            switch (declaration.Name)
            {
                // Skip PushID(const char*) and prefer PushID(const char*, const char*)
                case "PushID":
                // Skip GetID(const char*) and prefer GetID(const char*, const char*)
                case "GetID":
                    return declaration.Parameters.Length == 1;
                // This function takes a string, but not one that's written by humans.
                // If we were to call this from C# we'd want to actually pass in a ReadOnlySpan<byte> or something.
                case "AddFontFromMemoryCompressedBase85TTF":
                    return true;
                // There is a UTF16 equivalent of this function, so it doesn't make sense to generate a wrapper
                case "AddInputCharactersUTF8":
                    return true;
                // This function returns a pointer into the buffer you provide it so it has special usage
                case "CalcWordWrapPositionA":
                    return true;
                default:
                    return false;
            }
        }

        // This transformation is an opt-out heuristic
        // It'd be not-ideal if it was wrong, but it's expected that the user will be smart enough to avoid calling the nonsense overload
        // In the long term we'll rely on a parameter being explicitly marked as a string once https://github.com/ocornut/imgui/pull/3038 is merged
        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            // Opt out of specific functions that shouldn't get overloads
            if (ShouldOptOut(declaration))
            { return declaration; }

            // Get the primary trampoline
            if (declaration.TryGetPrimaryTrampoline() is not Trampoline targetTrampoline)
            { return declaration; }

            // Build a string trampoline if needed
            TrampolineBuilder builder = new(targetTrampoline, useAsTemplate: true);

            // Iterate through the parameter adapters on the trampoline and see if any can be adapted to a string helper
            ImGuiStringAdapter? lastStringAdapter = null;
            foreach (Adapter parameter in targetTrampoline.Adapters)
            {
                // Skip parameters which don't accept input
                if (!parameter.AcceptsInput)
                { continue; }

                // If the parameter is a `const byte*`, it's a string parameter
                if (parameter.InputType is PointerTypeReference { Inner: CSharpBuiltinTypeReference cSharpPointee, InnerIsConst: true } && cSharpPointee == CSharpBuiltinType.Byte)
                {
                    //TODO: If we modified this to be lookahead instead of lookbehind, we could avoid having the weird mutable property on ImGuiStringAdapter.
                    // If this parameter ends with `_end` and the previous parameter was a string, we treat this as a string end parameter
                    if (lastStringAdapter is not null && parameter.Name.EndsWith("_end"))
                    {
                        builder.AdaptParameter(parameter, new ImGuiStringEndAdapter(lastStringAdapter, parameter));
                        lastStringAdapter = null;
                    }
                    else
                    {
                        lastStringAdapter = new ImGuiStringAdapter(parameter);
                        builder.AdaptParameter(parameter, lastStringAdapter);
                    }
                }
                else
                { lastStringAdapter = null; }
            }

            // Add a string helper overload if appropriate
            if (builder.HasAdapters)
            { return declaration.WithSecondaryTrampoline(builder.Create()); }

            return declaration; 
        }
    }
}
