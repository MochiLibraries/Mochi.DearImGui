using Biohazrd;
using Biohazrd.CSharp;
using Biohazrd.CSharp.Trampolines;
using Biohazrd.Transformation;
using System;

namespace Mochi.DearImGui.Generator;

internal sealed class ImGuiStringAdapter : Adapter, IAdapterWithEpilogue
{
    internal string TemporarySpanName { get; }
    internal string TemporaryPointerName { get; }
    internal bool NeedsSpanTemporary { get; set; } = false;

    private static readonly ExternallyDefinedTypeReference InterpolatedStringHandlerType = new("Mochi.DearImGui", "DearImGuiInterpolatedStringHandler");

    public ImGuiStringAdapter(Adapter target)
        : base(target)
    {
        if (!(target.InputType is PointerTypeReference { Inner: CSharpBuiltinTypeReference cSharpPointee } && cSharpPointee == CSharpBuiltinType.Byte))
        { throw new ArgumentException("Target expected to be a `const char*`!", nameof(target)); }

        InputType = InterpolatedStringHandlerType;
        TemporarySpanName = $"__{Name}";
        TemporaryPointerName = $"{TemporarySpanName}P";
    }

    public override bool CanEmitDefaultValue(TranslatedLibrary library)
        => false;

    public override void WritePrologue(TrampolineContext context, CSharpCodeWriter writer)
    {
        if (!NeedsSpanTemporary)
        { return; }

        writer.Using("System"); // ReadOnlySpan<T>
        writer.Write("ReadOnlySpan<byte> ");
        writer.WriteIdentifier(TemporarySpanName);
        writer.Write(" = ");
        writer.WriteIdentifier(Name);
        writer.WriteLine(".NullTerminateAndGetString();");
    }

    public override bool WriteBlockBeforeCall(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.Write("fixed (byte* ");
        writer.WriteIdentifier(TemporaryPointerName);
        writer.Write(" = ");
        if (NeedsSpanTemporary)
        { writer.WriteIdentifier(TemporarySpanName); }
        else
        {
            writer.WriteIdentifier(Name);
            writer.Write(".NullTerminateAndGetString()");
        }
        writer.WriteLine(')');
        return true;
    }

    public override void WriteOutputArgument(TrampolineContext context, CSharpCodeWriter writer)
        => writer.WriteIdentifier(TemporaryPointerName);

    public void WriteEpilogue(TrampolineContext context, CSharpCodeWriter writer)
    {
        writer.WriteIdentifier(Name);
        writer.WriteLine(".Dispose();");
    }
}
