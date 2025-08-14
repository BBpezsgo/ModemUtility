namespace ModemUtility.Modem;

public readonly record struct Contact(int Index, string Address, int Type, string Name)
{
    public static Contact Parse(ref Reader reader)
    {
        int index = reader.ReadNumberValue();
        string address = reader.ReadStringValue().ToString();
        int type = reader.ReadNumberValue();
        string name = reader.ReadStringValue().ToString().Replace('\u0005', '?');
        return new Contact(index, address, type, name);
    }
}
