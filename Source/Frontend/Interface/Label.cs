using System.Collections.Immutable;
using System.Text;

namespace ModemUtility.Frontend.Interface;

public class Label : Element
{
    readonly List<Token> Tokens = [];
    ImmutableArray<(string Text, int Width, ImmutableArray<Token> Tokens)> Lines = [];

    readonly StringBuilder CurrentToken = new();
    int CurrentWidth = 0;
    int PrefferedWidth = 0;

    public void ClearContent() => Clear();

    void Clear()
    {
        Tokens.Clear();
        CurrentToken.Clear();
        CurrentWidth = 0;
        PrefferedWidth = 0;
    }

    public void Style(string style)
    {
        FinishTextToken();
        Tokens.Add(new Token(style, TokenType.Control));
    }

    public void Write(string? text)
    {
        CurrentToken.Append(text);
    }

    void FinishTextToken()
    {
        if (CurrentToken.Length > 0)
        {
            string[] v = CurrentToken.ToString().Split(' ');
            for (int i = 0; i < v.Length; i++)
            {
                if (i > 0) Tokens.Add(new Token(" ", TokenType.Text));
                if (v[i].Length > 0) Tokens.Add(new Token(v[i], TokenType.Text));
            }
            CurrentWidth += CurrentToken.Length;
            CurrentToken.Clear();
        }
    }

    void FinishLine()
    {
        FinishTextToken();
        PrefferedWidth = Math.Max(PrefferedWidth, CurrentWidth);
        CurrentWidth = 0;
    }

    public void WriteLine(string? text)
    {
        Write(text?.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' '));
        WriteLine();
    }

    public void WriteLine()
    {
        FinishLine();
        Tokens.Add(new Token(string.Empty, TokenType.LineBreak));
    }

    public override void RecalculateLayout()
    {
        Width = 0;
        Height = 0;

        ImmutableArray<(string, int, ImmutableArray<Token>)>.Builder result = ImmutableArray.CreateBuilder<(string, int, ImmutableArray<Token>)>();

        StringBuilder currentLine = new();
        int currentWidth = 0;
        List<Token> currentLineTokens = [];

        Queue<Token> subtokens = [];

        void AppendToken(Token token)
        {
            switch (token.Type)
            {
                case TokenType.Text:
                    if (currentWidth + token.Text.Length > MaxWidth)
                    {
                        int l = Math.Max(0, MaxWidth - currentWidth);
                        if (l == 0) return;

                        if (token.Text.Length <= MaxWidth)
                        {
                            subtokens.Enqueue(new Token(string.Empty, TokenType.LineBreak));
                            subtokens.Enqueue(new Token(token.Text, TokenType.Text));
                            break;
                        }

                        currentLine.Append(token.Text.AsSpan(0, l));
                        currentLineTokens.Add(new Token(token.Text[..l], TokenType.Text));
                        currentWidth += l;

                        subtokens.Enqueue(new Token(string.Empty, TokenType.LineBreak));
                        subtokens.Enqueue(new Token(token.Text[l..], TokenType.Text));
                    }
                    else
                    {
                        currentLine.Append(token.Text);
                        currentLineTokens.Add(token);
                        currentWidth += token.Text.Length;
                    }
                    break;
                case TokenType.Control:
                    currentLine.Append(token.Text);
                    currentLineTokens.Add(token);
                    break;
                case TokenType.LineBreak:
                    Width = Math.Max(Width, currentWidth);
                    Height++;

                    currentLine.Append("\e[0m");
                    currentLineTokens.Add(new Token("\e[0m", TokenType.Control));

                    result.Add((currentLine.ToString(), currentWidth, currentLineTokens.ToImmutableArray()));

                    currentLine.Clear();
                    currentLineTokens.Clear();
                    currentWidth = 0;
                    break;
            }
        }

        foreach (Token token in Tokens)
        {
            while (subtokens.TryDequeue(out Token subtoken))
            {
                AppendToken(subtoken);
            }
            AppendToken(token);
        }

        Width = Math.Max(Width, MinWidth);
        Height = Math.Max(Height, MinHeight);

        Lines = result.ToImmutable();
    }

    public override void RenderLine(int line)
    {
        if (line >= 0 && line < Lines.Length)
        {
            Console.Write(Lines[line].Text);
            Console.Write(new string(' ', Math.Max(0, Width - Lines[line].Width)));
        }
        else
        {
            Console.Write(new string(' ', Width));
        }
    }
}
