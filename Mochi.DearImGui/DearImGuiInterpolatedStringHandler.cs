using System;
using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace Mochi.DearImGui;

//TODO: Consider using Encoder.Convert with a shared Encoder for stuff instead since it allows iterative conversions
// This is loosely based on the API shape of DefaultInterpolatedStringHandler except it lacks overloads that implicitly cause allocations
[EditorBrowsable(EditorBrowsableState.Never)]
[InterpolatedStringHandler]
public ref struct DearImGuiInterpolatedStringHandler
{
    private readonly byte[] BufferArray;
    private readonly char[] TemporaryCharArray;
    private Span<byte> Buffer => BufferArray;
    private int UsedLength;

    private Span<byte> UnusedBuffer => Buffer.Slice(UsedLength);

    public DearImGuiInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        //TODO: Use a dedicated frame allocator for this instead, could use the same guessing logic as DefaultInterpolatedStringHandler
        // We could also use a central block of memory and when a new string handler is created we assume the previous one is complete and steal its remaining buffer for the next string
        // Generally speaking, we only expect one thread at a time to use this interpolated string handler and only a few strings will be "in flight" at any given time
        //
        // It's a bit unfortunate we don't have a way to allocate on our calee's stack somehow since that'd be ideal for our situation.
        // This was considered by LDM but they decided it was out of scope for C# 10:
        // https://github.com/dotnet/csharplang/blob/6b0f5be210662d0f0a5b388c23c59fdfd9d6d5f1/proposals/csharp-10.0/improved-interpolated-strings.md#incorporating-spans-for-heap-less-strings
        // (Which is understandable since it's a complicated issue.)
        //
        // Although maybe the processor would be smart enough to consider our buffer warm in the cache if we keep using the same chunk of memory for string interpolations.
        // (IE: If we stop using ArrayPool)
        BufferArray = ArrayPool<byte>.Shared.Rent(1024 * 8);
        TemporaryCharArray = ArrayPool<char>.Shared.Rent(4096);
        UsedLength = 0;
    }

    public bool AppendLiteral(ReadOnlySpan<char> s)
    {
        //TODO: Allow graceful failure or reallocating the underlying buffer
        UsedLength += Encoding.UTF8.GetBytes(s, UnusedBuffer);
        return true;
    }

    public bool AppendFormatted(ReadOnlySpan<char> s)
        => AppendLiteral(s);

    private bool AppendUtf8Chars(byte c, int count)
    {
        if (UnusedBuffer.Length < count)
        { return false; }

        UnusedBuffer.Slice(0, count).Fill(c);
        UsedLength += count;
        return true;
    }

    public bool AppendFormatted(ReadOnlySpan<char> s, int alignment)
    {
        const byte space = 0x20;
        bool leftAlign = false;
        if (alignment < 0)
        {
            alignment = -alignment;
            leftAlign = true;
        }

        // The alignment is meant to be for UTF16 characters so we can use the length of the UTF16 char buffer
        int paddingRequired = alignment - s.Length;

        if (paddingRequired <= 0)
        { return AppendFormatted(s); }
        else if (leftAlign)
        { return AppendFormatted(s) && AppendUtf8Chars(space, paddingRequired); }
        else
        { return AppendUtf8Chars(space, paddingRequired) && AppendFormatted(s); }
    }

    //TODO: We could offer specialized overloads for things like int to allow faster debug builds at the expense of metadata bloat
    // (For release builds we expect the JIT to optimize things to the point where this is effectively specialized)
    // We should consider adding DebuggableAttribute to our assembly so this is always optimized.
    public bool AppendFormatted<T>(T value)
        where T : ISpanFormattable // We're trying to only allow span formttables to force the developer to notice they're hitting an allocating path
    {
        int charsWritten;
        if (!value.TryFormat(TemporaryCharArray, out charsWritten, default, null))
        { return false; }

        ReadOnlySpan<char> valueString = TemporaryCharArray.AsSpan().Slice(0, charsWritten);
        return AppendLiteral(valueString);
    }

    public bool AppendFormatted<T>(T value, ReadOnlySpan<char> format)
        where T : ISpanFormattable
    {
        int charsWritten;
        if (!value.TryFormat(TemporaryCharArray, out charsWritten, format, null))
        { return false; }

        ReadOnlySpan<char> valueString = TemporaryCharArray.AsSpan().Slice(0, charsWritten);
        return AppendLiteral(valueString);
    }

    public bool AppendFormatted<T>(T value, int alignment)
        where T : ISpanFormattable
    {
        int charsWritten;
        if (!value.TryFormat(TemporaryCharArray, out charsWritten, default, null))
        { return false; }

        ReadOnlySpan<char> valueString = TemporaryCharArray.AsSpan().Slice(0, charsWritten);
        return AppendFormatted(valueString, alignment);
    }

    public bool AppendFormatted<T>(T value, int alignment, ReadOnlySpan<char> format)
        where T : ISpanFormattable
    {
        int charsWritten;
        if (!value.TryFormat(TemporaryCharArray, out charsWritten, format, null))
        { return false; }

        ReadOnlySpan<char> valueString = TemporaryCharArray.AsSpan().Slice(0, charsWritten);
        return AppendFormatted(valueString, alignment);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DearImGuiInterpolatedStringHandler(ReadOnlySpan<char> s)
    {
        DearImGuiInterpolatedStringHandler result = new(s.Length, 0);
        result.AppendLiteral(s);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator DearImGuiInterpolatedStringHandler(string? s)
        // If the string is null, return a defaulted handler, this will result in BufferArray being null which will cause NullTerminateAndGetString to return a null span.
        => s is null ? default : s.AsSpan();

    internal ReadOnlySpan<byte> GetString()
        => Buffer.Slice(0, UsedLength);

    internal ReadOnlySpan<byte> NullTerminateAndGetString()
    {
        if (BufferArray is null)
        { return default; }

        //TODO: This doesn't totally align with our previous logic of aborting if we run out of buffer space
        if (UsedLength >= Buffer.Length)
        { UsedLength--; }

        Buffer[UsedLength] = 0;
        // The +1 here is to include the null terminator. This might seem a bit odd since it's not really part of the string, but if we don't do this then fixing this span when it's
        // empty will result in a null pointer.
        return Buffer.Slice(0, UsedLength + 1);
    }

    public override string ToString()
        => Encoding.UTF8.GetString(Buffer.Slice(0, UsedLength));

    public void Dispose()
    {
        if (BufferArray is not null)
        {
            ArrayPool<byte>.Shared.Return(BufferArray);
        }

        if (TemporaryCharArray is not null)
        {
            ArrayPool<char>.Shared.Return(TemporaryCharArray);
        }
    }
}
