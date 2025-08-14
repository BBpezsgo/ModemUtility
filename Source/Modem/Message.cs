namespace ModemUtility.Modem;

public readonly record struct Message(MessageStatus Status, string FromAddress, string ReferenceAddress, string Time, string Data)
{
    public static Message ParseText(ref Reader reader)
    {
        MessageStatus status = reader.ReadStringValue().ToString() switch
        {
            "REC READ" => MessageStatus.RecRead,
            "STO UNSENT" => MessageStatus.StoUnsent,
            "STO SENT" => MessageStatus.StoSent,
            _ => throw new CommunicationException("Invalid response"),
        };
        string fromAddress = reader.ReadStringValue().ToString();
        string referenceAddress = reader.ReadStringValue().ToString();
        string time = reader.ReadStringValue().ToString();
        reader.ReadNewLine();
        ReadOnlySpan<char> rawData = reader.ReadNextValue();

        string data;
        try
        {
            data = PduDecoder.DecodeUserData(Convert.FromHexString(rawData));
        }
        catch
        {
            data = rawData.ToString();
        }

        return new Message(status, fromAddress, referenceAddress, time, data);
    }

    public static (MessageStatus Status, int Length, string PDU) ParsePdu(ref Reader reader)
    {
        int status1 = reader.ReadNumberValue();
        //MessageStatus status = status1 switch
        //{
        //    1 => MessageStatus.RecRead,
        //    _ => throw new CommunicationException($"Invalid response {status1}"),
        //};
        reader.ReadNextValue(); // empty
        int length = Convert.ToInt32(reader.ReadNextValue().ToString(), 16);
        reader.ReadNewLine();
        ReadOnlySpan<char> rawData = reader.ReadNextValue();

        return ((MessageStatus)status1, length, rawData.ToString());
    }
}
