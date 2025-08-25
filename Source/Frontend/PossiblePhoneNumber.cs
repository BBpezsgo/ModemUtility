using PhoneNumbers;

namespace ModemUtility.Frontend;

public readonly struct PossiblePhoneNumber(PhoneNumber? value, string raw)
{
    public PhoneNumber? Value { get; } = value;
    public string Raw { get; } = raw;

    public override string ToString() => Value is not null ? PhoneNumberUtil.GetInstance().Format(Value, PhoneNumberFormat.E164) : Raw;

    public bool Equals(PossiblePhoneNumber other)
    {
        if (other.Raw is null) return false;
        return ToString().Equals(other.ToString(), StringComparison.Ordinal); // TODO fix this bruh wtf
        //if (Value is not null && other.Value is not null)
        //{
        //    PhoneNumberUtil.MatchType match = PhoneNumberUtil.GetInstance().IsNumberMatch(Value, other.Value);
        //    return match is not PhoneNumberUtil.MatchType.NO_MATCH;
        //}
        //return string.Equals(Raw, other.Raw, StringComparison.Ordinal);
    }
}
