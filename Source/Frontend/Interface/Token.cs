namespace ModemUtility.Frontend.Interface;

public enum TokenType
{
    Text,
    Control,
    LineBreak,
}

public readonly struct Token(string text, TokenType type)
{
    public string Text { get; } = text;
    public TokenType Type { get; } = type;
}
