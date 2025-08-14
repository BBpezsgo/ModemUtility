namespace ModemUtility.Modem;

public sealed record SmsConcatInfo(int Reference, int TotalParts, int Sequence, bool Is16Bit)
{
    public override string ToString() => $"(Ref: {Reference} Total: {TotalParts} Seq: {Sequence} Is16Bit: {Is16Bit})";
}
