namespace ModemUtility.Frontend.Interface;

public abstract class Element
{
    public int Width { get; protected set; }
    public int Height { get; protected set; }

    public int MinWidth { get; set; } = 0;
    public int MinHeight { get; set; } = 0;

    public int MaxWidth { get; set; } = int.MaxValue;
    public int MaxHeight { get; set; } = int.MaxValue;

    public virtual int FlexBias { get; set; }

    public abstract void RecalculateLayout();
    public abstract void RenderLine(int line);
}
