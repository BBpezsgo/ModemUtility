using System.Collections.Immutable;
using System.Text;

namespace ModemUtility.Frontend.Interface;

public readonly struct Sides
{
    public int Top { get; }
    public int Left { get; }
    public int Bottom { get; }
    public int Right { get; }

    public Sides(int all)
    {
        Top = all;
        Left = all;
        Bottom = all;
        Right = all;
    }

    public Sides(int vertical, int horizontal)
    {
        Top = vertical;
        Left = horizontal;
        Bottom = vertical;
        Right = horizontal;
    }

    public Sides(int top, int left, int bottom, int right)
    {
        Top = top;
        Left = left;
        Bottom = bottom;
        Right = right;
    }

    public static implicit operator Sides(int all) => new(all);
}

public enum ContainerDirection
{
    Horizontal,
    Vertical,
}

public enum TokenType
{
    Text,
    Control,
    LineBreak,
}

readonly struct Token(string text, TokenType type)
{
    public readonly string Text = text;
    public readonly TokenType Type = type;
}

public class Label : Element
{
    readonly List<Token> tokens = [];
    readonly StringBuilder currentToken = new();

    ImmutableArray<(string Text, int Width)> lines = [];
    int currentWidth = 0;

    int prefferedWidth = 0;

    public void Clear()
    {
        tokens.Clear();
        currentToken.Clear();

        currentWidth = 0;
        prefferedWidth = 0;
    }

    void FinishTextToken()
    {
        if (currentToken.Length > 0)
        {
            tokens.Add(new Token(currentToken.ToString(), TokenType.Text));
            currentWidth += currentToken.Length;
            currentToken.Clear();
        }
    }

    public void Style(string style)
    {
        FinishTextToken();
        tokens.Add(new Token(style, TokenType.Control));
    }

    public void Write(string? text)
    {
        currentToken.Append(text);
    }

    public void WriteLine(string? text)
    {
        Write(text);
        WriteLine();
    }

    public void WriteLine()
    {
        FinishTextToken();

        tokens.Add(new Token(string.Empty, TokenType.LineBreak));

        prefferedWidth = Math.Max(prefferedWidth, currentWidth);
        currentWidth = 0;
    }

    public override void RecalculateLayout()
    {
        Width = 0;
        Height = 0;

        ImmutableArray<(string, int)>.Builder result = ImmutableArray.CreateBuilder<(string, int)>();
        StringBuilder currentLine = new();
        int currentWidth = 0;
        Queue<Token> subtokens = [];

        void AppendToken(Token token)
        {
            switch (token.Type)
            {
                case TokenType.Text:
                    if (currentWidth + token.Text.Length > MaxWidth)
                    {
                        int l = MaxWidth - currentWidth;
                        if (l == 0) throw new Exception();

                        currentLine.Append(token.Text.AsSpan(0, l));
                        currentWidth += l;

                        subtokens.Enqueue(new Token(string.Empty, TokenType.LineBreak));
                        subtokens.Enqueue(new Token(token.Text[l..], TokenType.Text));
                    }
                    else
                    {
                        currentLine.Append(token.Text);
                        currentWidth += token.Text.Length;
                    }
                    break;
                case TokenType.Control:
                    currentLine.Append(token.Text);
                    break;
                case TokenType.LineBreak:
                    Width = Math.Max(Width, currentWidth);
                    Height++;

                    currentLine.Append("\e[0m");
                    result.Add((currentLine.ToString(), currentWidth));
                    currentLine.Clear();
                    currentWidth = 0;
                    break;
            }
        }

        foreach (Token token in tokens)
        {
            while (subtokens.TryDequeue(out Token subtoken))
            {
                AppendToken(subtoken);
            }
            AppendToken(token);
        }

        Width = Math.Max(Width, MinWidth);
        Height = Math.Max(Height, MinHeight);

        lines = result.ToImmutable();
    }

    public override void RenderLine(int line)
    {
        if (line >= 0 && line < lines.Length)
        {
            Console.Write(lines[line].Text);
            Console.Write(new string(' ', Math.Max(0, Width - lines[line].Width)));
        }
        else
        {
            Console.Write(new string(' ', Width));
        }
    }
}

public class Container(ContainerDirection direction) : Element
{
    readonly List<Element> children = [];

    public T Add<T>(T element) where T : Element
    {
        children.Add(element);
        return element;
    }

    public override void RecalculateLayout()
    {
        int width = 0;
        int height = 0;

        for (int i = 0; i < children.Count; i++)
        {
            Element child = children[i];
            child.MinWidth = 0;
            child.MinHeight = 0;

            switch (direction)
            {
                case ContainerDirection.Horizontal:
                    int remainingW = Math.Max(0, MaxWidth - width);
                    remainingW = Math.Min(remainingW, remainingW / (children.Count - i));

                    child.MaxWidth = remainingW;
                    child.MaxHeight = MaxHeight;
                    break;
                case ContainerDirection.Vertical:
                    int remainingH = Math.Max(0, MaxHeight - height);
                    remainingH = Math.Min(remainingH, remainingH / (children.Count - i));

                    child.MaxWidth = MaxWidth;
                    child.MaxHeight = remainingH;
                    break;
            }

            child.RecalculateLayout();

            switch (direction)
            {
                case ContainerDirection.Horizontal:
                    width = Math.Min(MaxWidth, width + child.Width);
                    height = Math.Min(MaxHeight, Math.Max(height, child.Height));
                    break;
                case ContainerDirection.Vertical:
                    width = Math.Min(MaxWidth, Math.Max(width, child.Width));
                    height = Math.Min(MaxHeight, height + child.Height);
                    break;
            }
        }

        Width = Math.Max(MinWidth, width);
        Height = Math.Max(MinHeight, height);

        foreach (Element child in children)
        {
            switch (direction)
            {
                case ContainerDirection.Horizontal:
                    child.MinHeight = height;
                    break;
                case ContainerDirection.Vertical:
                    child.MinWidth = width;
                    break;
            }
            child.RecalculateLayout();
        }
    }

    public override void RenderLine(int line)
    {
        switch (direction)
        {
            case ContainerDirection.Horizontal:
                foreach (Element child in children)
                {
                    child.RenderLine(line);
                }
                break;
            case ContainerDirection.Vertical:
                int childY = 0;
                foreach (Element child in children)
                {
                    if (line >= childY && line < childY + child.Height)
                    {
                        child.RenderLine(line - childY);
                        break;
                    }
                    childY += child.Height;
                }
                break;
        }
    }
}

public class Border<TChild>(TChild child, Sides margin = default, Sides padding = default) : Element
    where TChild : Element
{
    public TChild Child { get; } = child;
    public Sides Margin { get; } = margin;
    public Sides Padding { get; } = padding;

    public override void RecalculateLayout()
    {
        int extraWidth = 2 + Margin.Left + Margin.Right + Padding.Left + Padding.Right;
        int extraHeight = 2 + Margin.Top + Margin.Bottom + Padding.Top + Padding.Bottom;

        Child.MinWidth = Math.Max(0, MinWidth - extraWidth);
        Child.MinHeight = Math.Max(0, MinHeight - extraHeight);
        Child.MaxWidth = Math.Max(0, MaxWidth - extraWidth);
        Child.MaxHeight = Math.Max(0, MaxHeight - extraHeight);
        Child.RecalculateLayout();

        Width = Math.Min(MaxWidth - extraWidth, Child.Width) + extraWidth;
        Height = Math.Min(MaxHeight - extraHeight, Child.Height) + extraHeight;
    }

    public override void RenderLine(int line)
    {
        if (Width <= 1) return;
        if (Height <= 1) return;

        if (line < Margin.Top)
        {
            Console.Write(new string(' ', Width));
        }
        else if (line == Margin.Top)
        {
            Console.Write(new string(' ', Margin.Left));
            Console.Write('╭');
            Console.Write(new string('─', Width - 2 - Margin.Left - Margin.Right));
            Console.Write('╮');
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line < Margin.Top + Padding.Top + 1)
        {
            Console.Write(new string(' ', Margin.Left));
            Console.Write('│');
            Console.Write(new string(' ', Width - 2 - Margin.Left - Margin.Right));
            Console.Write('│');
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line < Height - 1 - Margin.Top - Padding.Top)
        {
            Console.Write(new string(' ', Margin.Left));
            Console.Write('│');
            Console.Write(new string(' ', Padding.Left));
            Child.RenderLine(line - 1 - Margin.Top - Padding.Top);
            Console.Write(new string(' ', Padding.Right));
            Console.Write('│');
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line < Height - 1 - Margin.Bottom)
        {
            Console.Write(new string(' ', Margin.Left));
            Console.Write('│');
            Console.Write(new string(' ', Width - 2 - Margin.Left - Margin.Right));
            Console.Write('│');
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line == Height - 1 - Margin.Bottom)
        {
            Console.Write(new string(' ', Margin.Left));
            Console.Write('╰');
            Console.Write(new string('─', Width - 2 - Margin.Left - Margin.Right));
            Console.Write('╯');
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line < Height - 1)
        {
            Console.Write(new string(' ', Width));
        }
    }
}

public class Border(Element child) : Border<Element>(child);

public abstract class Element
{
    public int Width { get; protected set; }
    public int Height { get; protected set; }

    public int MinWidth { get; set; } = 0;
    public int MinHeight { get; set; } = 0;

    public int MaxWidth { get; set; } = int.MaxValue;
    public int MaxHeight { get; set; } = int.MaxValue;

    public bool NoShrink { get; init; }

    public abstract void RecalculateLayout();
    public abstract void RenderLine(int line);
}

public class InterfaceRenderer
{
    public static void Render(Element element)
    {
        for (int line = 0; line < element.Height; line++)
        {
            element.RenderLine(line);
            Console.WriteLine();
        }
    }
}
