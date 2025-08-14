namespace ModemUtility;

public ref struct BinaryReader
{
    readonly ReadOnlySpan<byte> Span;
    public int Index { get; private set; }
    public readonly int Length => Span.Length;
    public readonly int Remaining => Length - Index;

    public BinaryReader(ReadOnlySpan<byte> span)
    {
        Span = span;
        Index = 0;
    }

    public ReadOnlySpan<byte> ReadBytes(int length) => Span.Slice(Index, Index += length);

    public byte ReadByte() => Span[Index++];

    public void Skip(int count) => Index += count;

    public ushort ReadUInt16() => BitConverter.ToUInt16(ReadBytes(2));
    public uint ReadUInt32() => BitConverter.ToUInt32(ReadBytes(4));
    public short ReadInt16() => BitConverter.ToInt16(ReadBytes(2));
    public int ReadInt32() => BitConverter.ToInt32(ReadBytes(4));
}
