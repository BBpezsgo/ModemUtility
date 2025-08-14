namespace ModemUtility.Frontend;

public class Contact(int index, string address, string name)
{
    public int Index { get; set; } = index;
    public string Address { get; set; } = address;
    public string Name { get; set; } = name;
}
