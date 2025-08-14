namespace ModemUtility.Modem;

public readonly ref struct UserData(string? text, ReadOnlySpan<byte> raw, SmsConcatInfo? concat)
{
    public string? Text { get; } = text;
    public ReadOnlySpan<byte> Raw { get; } = raw;
    public SmsConcatInfo? Concat { get; } = concat;
}
