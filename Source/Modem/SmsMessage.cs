namespace ModemUtility.Modem;

public sealed record SmsMessage(SmsType Type, string? Smsc, string? Sender, string? Destination, byte ProtocolIdentifier, byte DataCodingScheme, SmsAlphabet Alphabet, DateTimeOffset? ServiceCenterTimestamp, bool Udhi, byte[] RawUserData, string? Text, SmsConcatInfo? Concat)
{
    public override string ToString() => $"(Mti: {Type} Smsc: {Smsc} Sender: {Sender} Destination: {Destination} TpPid: {ProtocolIdentifier} TpDcs: {DataCodingScheme} Alphabet: {Alphabet} SCT: {ServiceCenterTimestamp} Udhi: {Udhi} Text: \"{Text}\" Concat: {Concat})";
}
