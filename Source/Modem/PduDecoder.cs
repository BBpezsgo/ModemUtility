using System.Runtime.InteropServices;
using System.Text;

namespace ModemUtility.Modem;

public static class PduDecoder
{
    public static SmsMessage Decode(ReadOnlySpan<byte> pdu)
    {
        int i = 0;

        int smscLength = pdu[i++];
        string? smsc = null;
        if (smscLength > 0)
        {
            byte typeOfAddress = pdu[i++];

            int addrOctets = smscLength - 1;
            ReadOnlySpan<byte> addrBytes = pdu.Slice(i, addrOctets);

            i += addrOctets;
            smsc = DecodeAddress(typeOfAddress, addrBytes);
        }

        byte first = pdu[i++];
        bool replyPath = (first & 0x80) != 0;
        bool udHeaderIndicator = (first & 0x40) != 0;
        bool statusReportIndication = (first & 0x20) != 0;
        bool unused1 = (first & 0x10) != 0;
        bool unused2 = (first & 0x08) != 0;
        bool moreMessagesToSend = (first & 0x04) != 0;
        SmsType mti = (SmsType)(first & 0x03);

        return mti switch
        {
            SmsType.Deliver => DecodeDeliver(pdu, i, smsc, first, udHeaderIndicator),
            SmsType.Submit => DecodeSubmit(pdu, i, smsc, first, udHeaderIndicator),
            _ => new SmsMessage(mti, smsc, default, default, default, default, SmsAlphabet.Unknown, null, udHeaderIndicator, [], null, default),
        };
    }

    public static string DecodeUserData(ReadOnlySpan<byte> ud)
    {
        if (ud.Length == 0) return string.Empty;

        int udhLength = ud[0];
        int udhTotal = 1 + udhLength;

        try
        {
            string attempt = UnpackGsm7WithUdh(ud, udhLength);
            if (!string.IsNullOrWhiteSpace(attempt) && !attempt.Contains('\r') && !attempt.Contains('@')) return attempt;
        }
        catch
        {

        }

        if (ud.Length > udhTotal)
        {
            ReadOnlySpan<byte> payload = ud[udhTotal..];
            if ((payload.Length % 2) != 0) payload = payload[..^1];
            return Encoding.BigEndianUnicode.GetString(payload);
        }

        return string.Empty;
    }

    static SmsMessage DecodeDeliver(ReadOnlySpan<byte> pdu, int i, string? smsc, byte first, bool udhi)
    {
        string sender = DecodeAddress(pdu, ref i);

        byte protocolIdentifier = pdu[i++];
        byte dataCodingScheme = pdu[i++];
        SmsAlphabet alphabet = DecodeAlphabet(dataCodingScheme);

        // SCTS (7 octets semi-octet)
        DateTimeOffset? scts = DecodeScts(pdu, i);
        i += 7;

        int udl = pdu[i++];
        UserData userData = DecodeUserData(pdu, i, udl, udhi, alphabet);
        int udOctets = ComputeUdOctets(alphabet, udl, udhi, userData.Raw);
        i += udOctets;

        return new SmsMessage(SmsType.Deliver, smsc, sender, default, protocolIdentifier, dataCodingScheme, alphabet, scts, udhi, userData.Raw.ToArray(), userData.Text, userData.Concat);
    }

    static SmsMessage DecodeSubmit(ReadOnlySpan<byte> pdu, int i, string? smsc, byte first, bool udhi)
    {
        // MR
        byte mr = pdu[i++];

        string dest = DecodeAddress(pdu, ref i);

        byte tpPid = pdu[i++];
        byte tpDcs = pdu[i++];
        SmsAlphabet alphabet = DecodeAlphabet(tpDcs);

        // VP field presence depends on VPF (bits 4-3) in first octet
        int vpf = (first >> 3) & 0x3;
        if (vpf == 2) i += 1;  // relative VP
        else if (vpf == 3) i += 7;  // absolute VP (SCTS-like)
                                    // vpf == 0: VP absent; vpf == 1: enhanced (rare, skip or implement as needed)

        int udl = pdu[i++];
        UserData userData = DecodeUserData(pdu, i, udl, udhi, alphabet);
        int udOctets = ComputeUdOctets(alphabet, udl, udhi, userData.Raw);
        i += udOctets;

        return new SmsMessage(SmsType.Submit, smsc, default, dest, tpPid, tpDcs, alphabet, null, udhi, userData.Raw.ToArray(), userData.Text, userData.Concat);
    }

    static string DecodeAddress(ReadOnlySpan<byte> pdu, ref int i)
    {
        int digits = pdu[i++];
        byte typeOfAddress = pdu[i++];
        int octets = (digits + 1) / 2;

        ReadOnlySpan<byte> bytes = pdu.Slice(i, octets);
        i += octets;

        string dest = DecodeAddress(typeOfAddress, bytes, digits);

        return dest;
    }

    static UserData DecodeUserData(ReadOnlySpan<byte> pdu, int udStart, int udl, bool udhi, SmsAlphabet alphabet)
    {
        int udOctets;
        if (alphabet == SmsAlphabet.GSM7)
        {
            int available = pdu.Length - udStart;
            udOctets = Math.Min(available, (((udl * 7) + 7) / 8) + 10); // include some slack; will trim after UDH parse
        }
        else
        {
            udOctets = Math.Min(pdu.Length - udStart, udl);
        }

        ReadOnlySpan<byte> rawCandidate = pdu.Slice(udStart, udOctets);

        SmsConcatInfo? concat = null;
        int udhLenOctets = 0;
        if (udhi && rawCandidate.Length > 0)
        {
            udhLenOctets = rawCandidate[0] + 1;
            if (rawCandidate.Length >= udhLenOctets)
            {
                concat = TryParseConcat(rawCandidate, udhLenOctets);
            }
        }

        string? text;

        switch (alphabet)
        {
            case SmsAlphabet.GSM7:
                {
                    Span<byte> septets = UnpackGsm7Septets(rawCandidate, udl);
                    int udhSeptets = udhi ? CeilDiv(udhLenOctets * 8, 7) : 0;
                    if (udhSeptets > septets.Length) udhSeptets = septets.Length;
                    Span<byte> textSeptets = septets[udhSeptets..];

                    text = MapGsmSeptetsToString(textSeptets);
                    int exactUdOctets = CeilDiv(udl * 7, 8);
                    rawCandidate = rawCandidate[..Math.Min(rawCandidate.Length, exactUdOctets)];
                    break;
                }
            case SmsAlphabet.UCS2:
                {
                    ReadOnlySpan<byte> udBytes = rawCandidate[..Math.Min(udl, rawCandidate.Length)];
                    if (udhi && udBytes.Length >= udhLenOctets)
                        udBytes = udBytes[udhLenOctets..];
                    text = Encoding.BigEndianUnicode.GetString(udBytes);
                    rawCandidate = rawCandidate[..Math.Min(rawCandidate.Length, udl)];
                    break;
                }
            case SmsAlphabet.EightBit:
            case SmsAlphabet.Reserved:
            case SmsAlphabet.Unknown:
            default:
                text = null;
                rawCandidate = rawCandidate[..Math.Min(rawCandidate.Length, udl)];
                break;
        }

        return new UserData(text, rawCandidate, concat);
    }

    static SmsConcatInfo? TryParseConcat(ReadOnlySpan<byte> ud, int udhTotalLen)
    {
        int idx = 1;
        while (idx + 1 < udhTotalLen)
        {
            byte iei = ud[idx++];
            byte iedl = ud[idx++];
            if (idx + iedl > udhTotalLen) break;

            if (iei == 0x00 && iedl == 3)
            {
                int reference = ud[idx];
                int total = ud[idx + 1];
                int seq = ud[idx + 2];
                return new SmsConcatInfo(reference, total, seq, false);
            }
            else if (iei == 0x08 && iedl == 4)
            {
                int reference = (ud[idx] << 8) | ud[idx + 1];
                int total = ud[idx + 2];
                int seq = ud[idx + 3];
                return new SmsConcatInfo(reference, total, seq, true);
            }
            idx += iedl;
        }
        return null;
    }

    static int ComputeUdOctets(SmsAlphabet alphabet, int udl, bool udhi, ReadOnlySpan<byte> rawUd)
    {
        if (alphabet == SmsAlphabet.GSM7)
        {
            return Math.Min(rawUd.Length, CeilDiv(udl * 7, 8));
        }
        else
        {
            return Math.Min(rawUd.Length, udl);
        }
    }

    static SmsAlphabet DecodeAlphabet(byte dcs)
    {
        if (dcs is >= 0b01000000 and <= 0b10110000)
        {
            return SmsAlphabet.Reserved;
        }

        // General Data Coding indication (bit7=0)
        if ((dcs & 0x80) == 0)
        {
            int alphabetBits = (dcs >> 2) & 0x03;
            return alphabetBits switch
            {
                0 => SmsAlphabet.GSM7,
                1 => SmsAlphabet.EightBit,
                2 => SmsAlphabet.UCS2,
                _ => SmsAlphabet.Reserved
            };
        }

        // Bit7=1: Message Waiting or other
        byte high = (byte)(dcs & 0xF0);
        if (high == 0xF0)
        {
            int alphabetBits = (dcs >> 2) & 0x03;
            return alphabetBits switch
            {
                0 => SmsAlphabet.GSM7,
                1 => SmsAlphabet.EightBit,
                2 => SmsAlphabet.UCS2,
                _ => SmsAlphabet.Reserved
            };
        }
        else if (high is 0xC0 or 0xD0) // Message Waiting Indication group
        {
            bool ucs2 = (dcs & 0x04) != 0;
            return ucs2 ? SmsAlphabet.UCS2 : SmsAlphabet.GSM7;
        }

        return SmsAlphabet.Unknown;
    }

    enum TypeOfNumber
    {
        Unknown = 0b000, // This is used when the user or network has no a priori information about the numbering plan. In this case, the Address-Value field is organized according to the network dialling plan, e.g. prefix or escape digits might be present.
        International = 0b001,
        National = 0b010, // Prefix or escape digits shall not be included.
        NetworkSpecific = 0b011, // This is used to indicate administration/service number specific to the serving network, e.g. used to access an operator.
        Subscriber = 0b100, // This is used when a specific short number representation is stored in one or more SCs as part of a higher layer application. (Note that "Subscriber number" shall only be used in connection with the proper PID referring to this application).
        Alphanumeric = 0b101, // coded according to GSM TS 03.38 7-bit default alphabet
        Abbreviated = 0b110,
    }

    enum NumberingPlanIdentification
    {
        Unknown = 0b0000,
        ISDNOrTelephone = 0b0001, // E.164/E.163
        Data = 0b0011, // X.121
        Telex = 0b0100,
        National = 0b1000,
        Private = 0b1001,
        ERMES = 0b1010, // ETSI DE/PS 3 01-3
    }

    static string DecodeAddress(byte toa, ReadOnlySpan<byte> semiOctets, int? digitsCountOpt = null)
    {
        if ((toa & 0x80) == 0) throw new FormatException($"First bit of TOA should be 1");

        TypeOfNumber typeOfNumber = (TypeOfNumber)((toa & 0x70) >> 4);
        NumberingPlanIdentification numberPlanIdentification = (NumberingPlanIdentification)((toa & 0x0F) >> 4);

        int digitsCount = digitsCountOpt ?? (semiOctets.Length * 2);
        StringBuilder builder = new();

        if (typeOfNumber == TypeOfNumber.International) builder.Append('+');

        for (int i = 0; i < semiOctets.Length; i++)
        {
            int lo = semiOctets[i] & 0x0F;
            int hi = (semiOctets[i] >> 4) & 0x0F;
            if (builder.Length < digitsCount) builder.Append((char)((lo <= 9) ? ('0' + lo) : ('A' + (lo - 10))));
            if (builder.Length < digitsCount && hi != 0x0F) builder.Append((char)((hi <= 9) ? ('0' + hi) : ('A' + (hi - 10))));
        }

        return builder.ToString();
    }

    static DateTimeOffset? DecodeScts(ReadOnlySpan<byte> pdu, int i)
    {
        try
        {
            int yy = Bcd(pdu[i]);
            int mm = Bcd(pdu[i + 1]);
            int dd = Bcd(pdu[i + 2]);
            int hh = Bcd(pdu[i + 3]);
            int mi = Bcd(pdu[i + 4]);
            int ss = Bcd(pdu[i + 5]);
            int tzByte = pdu[i + 6];
            int tz = ((tzByte & 0x0F) * 10) + ((tzByte >> 4) & 0x07);
            int tzMinutes = tz * 15;
            bool tzNegative = (tzByte & 0x80) != 0;
            TimeSpan offset = TimeSpan.FromMinutes(tzNegative ? -tzMinutes : tzMinutes);

            int year = 2000 + yy;
            DateTimeOffset dto = new(year, mm, dd, hh, mi, ss, offset);
            return dto;
        }
        catch
        {
            return null;
        }
    }

    static int Bcd(byte value) => ((value & 0x0F) * 10) + ((value >> 4) & 0x0F);

    static Span<byte> UnpackGsm7Septets(ReadOnlySpan<byte> ud, int udlSeptets)
    {
        int totalBitsNeeded = udlSeptets * 7;
        int availableBits = ud.Length * 8;
        int bitsToRead = Math.Min(totalBitsNeeded, availableBits);
        List<byte> septets = new(udlSeptets);

        int bitIndex = 0;
        for (int s = 0; s < udlSeptets; s++)
        {
            int val = 0;
            for (int b = 0; b < 7; b++)
            {
                if (bitIndex >= bitsToRead) break;
                int oct = bitIndex / 8;
                int off = bitIndex % 8;
                int bit = (ud[oct] >> off) & 1;
                val |= bit << b;
                bitIndex++;
            }
            septets.Add((byte)(val & 0x7F));
        }

        return CollectionsMarshal.AsSpan(septets);
    }

    static string MapGsmSeptetsToString(ReadOnlySpan<byte> septets)
    {
        StringBuilder sb = new(septets.Length);
        for (int i = 0; i < septets.Length; i++)
        {
            byte v = septets[i];
            if (v == 0x1B)
            {
                if (i + 1 < septets.Length)
                {
                    byte ext = septets[++i];
                    if (GsmExtension.TryGetValue(ext, out char ech)) sb.Append(ech);
                    else sb.Append('?');
                }
                else
                {
                    sb.Append('?');
                }
            }
            else
            {
                if (v >= 0 && v < GsmDefault.Length) sb.Append(GsmDefault[v]);
                else sb.Append('?');
            }
        }
        return sb.ToString();
    }

    static string UnpackGsm7WithUdh(ReadOnlySpan<byte> ud, int udhl) => MapGsmSeptetsToString((ReadOnlySpan<byte>)GetBytes7(1 + udhl, GetBits(ud)));

    static int CeilDiv(int a, int b) => (a + b - 1) / b;

    static int[] GetBits(ReadOnlySpan<byte> ud)
    {
        int[] bits = new int[ud.Length * 8];
        for (int i = 0; i < ud.Length; i++)
        {
            byte b = ud[i];
            for (int j = 0; j < 8; j++)
            {
                bits[(i * 8) + j] = (b >> j) & 1;
            }
        }

        return bits;
    }

    static byte[] GetBytes7(int udhTotal, ReadOnlySpan<int> bits)
    {
        int offsetBits = udhTotal * 7;
        int septetCount = (bits.Length - offsetBits) / 7;
        byte[] septets = new byte[septetCount];

        for (int s = 0; s < septetCount; s++)
        {
            int val = 0;
            for (int bit = 0; bit < 7; bit++)
            {
                val |= bits[offsetBits + (s * 7) + bit] << bit;
            }
            septets[s] = (byte)(val & 0x7F);
        }

        return septets;
    }

    internal static readonly char[] GsmDefault = [
        '@', '£', '$', '¥', 'è', 'é', 'ù', 'ì', 'ò', 'Ç', '\x0A', 'Ø', 'ø', '\x0D', 'Å', 'å',
        'Δ', '_', 'Φ', 'Γ', 'Λ', 'Ω', 'Π', 'Ψ', 'Σ', 'Θ', 'Ξ', '\x1B', 'Æ', 'æ', 'ß', 'É',
        ' ', '!', '"', '#', '¤', '%', '&', '\'', '(', ')', '*', '+', ',', '-', '.', '/',
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ':', ';', '<', '=', '>', '?',
        '¡', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O',
        'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'Ä', 'Ö', 'Ñ', 'Ü', '§',
        '¿', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o',
        'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'ä', 'ö', 'ñ', 'ü', 'à',
    ];

    static readonly Dictionary<byte, char> GsmExtension = new()
    {
        { 0x0A, '\f' }, { 0x14, '^' }, { 0x28, '{' }, { 0x29, '}' },
        { 0x2F, '\\' }, { 0x3C, '[' }, { 0x3D, '~' }, { 0x3E, ']' },
        { 0x40, '|' }, { 0x65, '€' },
    };
}
