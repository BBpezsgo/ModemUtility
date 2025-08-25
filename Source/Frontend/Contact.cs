namespace ModemUtility.Frontend;

public class Contact(int index, PossiblePhoneNumber address, string? name)
{
    public int Index { get; set; } = index;
    public PossiblePhoneNumber Address { get; set; } = address;
    public string? Name { get; set; } = name;
    public int Type { get; set; }
}
