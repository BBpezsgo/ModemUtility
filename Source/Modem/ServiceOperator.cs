namespace ModemUtility.Modem;

public enum ServiceOperatorMode
{
    Automatic = 0,
    Manual = 1,
    Deregister = 2,
    SetOnly = 3,
    ManualOrAutomatic = 4,
}

public enum ServiceOperatorFormat
{
    LongAlphanumeric = 0,
    ShortAlphanumeric = 1,
    Numeric = 2,
}

public readonly record struct ServiceOperator(ServiceOperatorMode Mode, ServiceOperatorFormat Format, string Name)
{
    public static ServiceOperator Parse(ref Reader reader)
    {
        ServiceOperatorMode mode = (ServiceOperatorMode)reader.ReadNumberValue();
        ServiceOperatorFormat format = (ServiceOperatorFormat)reader.ReadNumberValue();
        string name = reader.ReadStringValue().ToString();

        return new ServiceOperator(mode, format, name);
    }
}
