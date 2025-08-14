namespace ModemUtility.Modem;

public readonly record struct MessageStorage(string Name, int Used, int Total)
{
    public static MessageStorage Parse(ref Reader reader)
    {
        string name = reader.ReadStringValue().ToString();
        int used = reader.ReadNumberValue();
        int total = reader.ReadNumberValue();

        return new MessageStorage(name, used, total);
    }
}
