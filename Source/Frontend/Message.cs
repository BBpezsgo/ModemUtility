namespace ModemUtility.Frontend;

public class Message
{
    public int Index { get; }
    public int? Reference { get; }
    public DateTimeOffset Time { get; set; }

    public Contact? Source { get; set; }
    public Contact? Destination { get; set; }

    public string? Text { get; private set; }
    readonly List<(int Sequence, string Text)>? Parts;

    public Message(int index, Contact? source, Contact? destination, DateTimeOffset time, string? text)
    {
        Index = index;
        Reference = null;
        Time = time;
        Source = source;
        Destination = destination;
        Text = text;
        Parts = null;
    }

    public Message(int index, Contact? source, Contact? destination, DateTimeOffset time, int reference)
    {
        Index = index;
        Reference = reference;
        Time = time;
        Source = source;
        Destination = destination;
        Text = null;
        Parts = [];
    }

    public void InsertPart(int sequence, string? text)
    {
        if (Parts is null) throw new InvalidOperationException($"Message is not segmented");
        if (text is null) return;

        int index = 0;
        int minNextSequence = int.MaxValue;
        for (int i = 0; i < Parts.Count; i++)
        {
            if (Parts[i].Sequence == sequence)
            {
                Parts[i] = (sequence, text);
                goto end;
            }

            if (Parts[i].Sequence > sequence && Parts[i].Sequence < minNextSequence)
            {
                index = i + 1;
                minNextSequence = Parts[i].Sequence;
            }
        }

        Parts.Insert(index, (sequence, text));

    end:;
        Text = Parts.Count == 1 ? Parts[0].Text : string.Join(null, Parts.Select(v => v.Text));
    }
}
