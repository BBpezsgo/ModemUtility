namespace ModemUtility.Frontend.Interface;

public class InterfaceRenderer
{
    public static void SetForeground(AnsiColor color) => Console.Write($"\e[38;5;{(byte)color}m");
    public static void SetBackground(AnsiColor color) => Console.Write($"\e[48;5;{(byte)color}m");
    public static void Reset() => Console.Write("\e[0m");

    public static void Render(Element element)
    {
        for (int line = 0; line < element.Height; line++)
        {
            element.RenderLine(line);
            Console.WriteLine();
        }
    }
}
