namespace ModemUtility.Frontend.Interface;

public abstract class Border(Sides margin = default, Sides padding = default) : Element
{
    public abstract Element Child { get; }
    public Sides Margin { get; } = margin;
    public Sides Padding { get; } = padding;
    public string? Title { get; set; }
    public AnsiColor Color { get; set; } = AnsiColor.Silver;

    public override int FlexBias { get => Child.FlexBias; set => Child.FlexBias = value; }

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
            InterfaceRenderer.SetForeground(Color);
            Console.Write('╭');
            int w = Width - 2 - Margin.Left - Margin.Right;
            if (Title is not null)
            {
                int titleLength = Math.Min(Title.Length, w - 2);
                if (titleLength > 0)
                {
                    Console.Write('┐');
                    Console.Write(Title.AsSpan(0, titleLength));
                    Console.Write('┌');
                    Console.Write(new string('─', w - 2 - titleLength));
                }
                else
                {
                    Console.Write(new string('─', w));
                }
            }
            else
            {
                Console.Write(new string('─', w));
            }
            Console.Write('╮');
            InterfaceRenderer.Reset();
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line < Margin.Top + Padding.Top + 1)
        {
            Console.Write(new string(' ', Margin.Left));
            InterfaceRenderer.SetForeground(Color);
            Console.Write('│');
            InterfaceRenderer.Reset();
            Console.Write(new string(' ', Width - 2 - Margin.Left - Margin.Right));
            InterfaceRenderer.SetForeground(Color);
            Console.Write('│');
            InterfaceRenderer.Reset();
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line < Height - 1 - Margin.Top - Padding.Top)
        {
            Console.Write(new string(' ', Margin.Left));
            InterfaceRenderer.SetForeground(Color);
            Console.Write('│');
            InterfaceRenderer.Reset();
            Console.Write(new string(' ', Padding.Left));
            Child.RenderLine(line - 1 - Margin.Top - Padding.Top);
            Console.Write(new string(' ', Padding.Right));
            InterfaceRenderer.SetForeground(Color);
            Console.Write('│');
            InterfaceRenderer.Reset();
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line < Height - 1 - Margin.Bottom)
        {
            Console.Write(new string(' ', Margin.Left));
            InterfaceRenderer.SetForeground(Color);
            Console.Write('│');
            InterfaceRenderer.Reset();
            Console.Write(new string(' ', Width - 2 - Margin.Left - Margin.Right));
            InterfaceRenderer.SetForeground(Color);
            Console.Write('│');
            InterfaceRenderer.Reset();
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line == Height - 1 - Margin.Bottom)
        {
            Console.Write(new string(' ', Margin.Left));
            InterfaceRenderer.SetForeground(Color);
            Console.Write('╰');
            Console.Write(new string('─', Width - 2 - Margin.Left - Margin.Right));
            Console.Write('╯');
            InterfaceRenderer.Reset();
            Console.Write(new string(' ', Margin.Right));
        }
        else if (line < Height - 1)
        {
            Console.Write(new string(' ', Width));
        }
    }
}

public class Border<TChild>(TChild child, Sides margin = default, Sides padding = default) : Border(margin, padding)
    where TChild : Element
{
    public override TChild Child { get; } = child;
}
