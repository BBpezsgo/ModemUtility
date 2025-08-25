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
