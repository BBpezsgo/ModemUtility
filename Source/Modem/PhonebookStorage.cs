namespace ModemUtility.Modem;

public readonly record struct PhonebookStorage(string Name, int Used, int Total)
{
    public static PhonebookStorage Parse(ref Reader reader)
    {
        string name = reader.ReadStringValue().ToString();
        int used = reader.ReadNumberValue();
        int total = reader.ReadNumberValue();

        return new PhonebookStorage(name, used, total);
    }
}
