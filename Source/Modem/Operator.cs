namespace ModemUtility.Modem;

public enum OperatorMode
{
    Automatic = 0,
    Manual = 1,
    Deregister = 2,
    SetOnly = 3,
    ManualOrAutomatic = 4,
}

public enum OperatorFormat
{
    LongAlphanumeric = 0,
    ShortAlphanumeric = 1,
    Numeric = 2,
}

public readonly record struct Operator(OperatorMode Mode, OperatorFormat Format, string Name)
{
    public static Operator Parse(ref Reader reader)
    {
        OperatorMode mode = (OperatorMode)reader.ReadNumberValue();
        OperatorFormat format = (OperatorFormat)reader.ReadNumberValue();
        string name = reader.ReadStringValue().ToString();

        return new Operator(mode, format, name);
    }
}
