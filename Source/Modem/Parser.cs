using System.Buffers;
using System.Globalization;

namespace ModemUtility.Modem;

public ref struct Reader
{
    public readonly bool IsEnd => Position >= Span.Length;
    public int Position { get; private set; }
    public readonly ReadOnlySpan<char> Span { get; }

    public Reader(ReadOnlySpan<char> span)
    {
        Span = span;
        Position = 0;
    }

    public ReadOnlySpan<char> ReadUntil(char character)
    {
        if (IsEnd) throw new CommunicationException($"Invalid response");
        int i = Span[Position..].IndexOf(character);
        if (i == -1) throw new CommunicationException($"Invalid response");
        ReadOnlySpan<char> result = Span[Position..(i + Position)];
        Position += i + 1;
        return result;
    }

    public ReadOnlySpan<char> ReadUntilAny(params ReadOnlySpan<char> character)
    {
        if (IsEnd) throw new CommunicationException($"Invalid response");
        int i = Span[Position..].IndexOfAny(character);
        if (i == -1) throw new CommunicationException($"Invalid response");
        ReadOnlySpan<char> result = Span[Position..(i + Position)];
        Position += i + 1;
        return result;
    }

    public void ReadAll(char c)
    {
        while (!IsEnd && Span[Position] == c)
        {
            Position++;
        }
    }

    public void ReadNewLine()
    {
        if (IsEnd) throw new CommunicationException($"Invalid response");
        if (Span[Position] == '\r') Position++;
        if (IsEnd) throw new CommunicationException($"Invalid response");
        if (Span[Position] != '\n') throw new CommunicationException($"Invalid response");
        Position++;
    }

    static readonly SearchValues<char> ValueSeparators = SearchValues.Create(',', '\r', '\n', '\0');

    public ReadOnlySpan<char> ReadNextValue()
    {
        if (IsEnd) throw new CommunicationException($"Invalid response");
        if (Span[Position] is '\r' or '\n') return [];
        int i = Span[Position..].IndexOfAny(ValueSeparators);
        if (i == -1)
        {
            ReadOnlySpan<char> result = Span[Position..];
            Position = Span.Length;
            return result;
        }
        else
        {
            ReadOnlySpan<char> result = Span[Position..(i + Position)];
            if (Span[Position + i] is '\r' or '\n') Position += i;
            else Position += i + 1;
            ReadAll(' ');
            return result;
        }
    }

    public ReadOnlySpan<char> ReadStringValue()
    {
        ReadOnlySpan<char> v = ReadNextValue();
        if (v.Length == 0) return [];
        if (!v.StartsWith('"')) throw new CommunicationException($"Invalid response");
        return v[1..^1];
    }

    public int ReadNumberValue() => int.Parse(ReadNextValue(), CultureInfo.InvariantCulture);
}