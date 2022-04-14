using Biohazrd.CSharp;
using Biohazrd.CSharp.Trampolines;
using System;

namespace Mochi.DearImGui.Generator;

internal sealed class ImGuiStringEndAdapter : Adapter
{
    private ImGuiStringAdapter Sibling { get; }

    public ImGuiStringEndAdapter(ImGuiStringAdapter sibling, Adapter target)
        : base(target)
    {
        Sibling = sibling;
        Sibling.NeedsSpanTemporary = true;

        // This adapter eliminates the `*_end` parameter from the native method
        AcceptsInput = false;
    }

    public override void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    { }

    public override bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer)
        => false;

    public override void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.WriteIdentifier(Sibling.TemporaryPointerName);
        writer.Write(" + ");
        writer.WriteIdentifier(Sibling.TemporarySpanName);
        writer.Write(".Length - 1");
    }
}
