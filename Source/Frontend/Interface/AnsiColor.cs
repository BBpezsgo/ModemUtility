namespace ModemUtility.Frontend.Interface;

public readonly struct AnsiColor(byte color)
{
    readonly byte Color = color;

    public static implicit operator byte(AnsiColor v) => v.Color;
    public static implicit operator AnsiColor(byte v) => new(v);

    public static readonly AnsiColor Black = 0;
    public static readonly AnsiColor Red = 1;
    public static readonly AnsiColor Green = 2;
    public static readonly AnsiColor Yellow = 3;
    public static readonly AnsiColor Blue = 4;
    public static readonly AnsiColor Magenta = 5;
    public static readonly AnsiColor Cyan = 6;
    public static readonly AnsiColor Silver = 7;
    public static readonly AnsiColor Gray = 8;
    public static readonly AnsiColor BrightRed = 9;
    public static readonly AnsiColor BrightGreen = 10;
    public static readonly AnsiColor BrightYellow = 11;
    public static readonly AnsiColor BrightBlue = 12;
    public static readonly AnsiColor BrightMagenta = 13;
    public static readonly AnsiColor BrightCyan = 14;
    public static readonly AnsiColor White = 15;

    public static AnsiColor RealColor(float r, float g, float b) => (byte)(16 + (36 * (r * 5)) + (6 * (g * 5)) + (b * 5));
    public static AnsiColor RealColor(byte r, byte g, byte b) => (byte)(16 + (36 * (r * 5 / 255)) + (6 * (g * 5 / 255)) + (b * 5 / 255));
}
