namespace ModemUtility.Frontend.Interface;

public enum ContainerDirection
{
    Horizontal,
    Vertical,
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

                    if (child.FlexBias != 0)
                    {
                        child.MinWidth = child.FlexBias;
                    }
                    else if (i == children.Count - 1)
                    {
                        child.MinWidth = remainingW;
                    }
                    break;
                case ContainerDirection.Vertical:
                    int remainingH = Math.Max(0, MaxHeight - height);
                    remainingH = Math.Min(remainingH, remainingH / (children.Count - i));

                    child.MaxWidth = MaxWidth;
                    child.MaxHeight = remainingH;

                    if (child.FlexBias != 0)
                    {
                        child.MinHeight = child.FlexBias;
                    }
                    else if (i == children.Count - 1)
                    {
                        child.MinHeight = remainingH;
                    }
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
                    child.MinHeight = Height;
                    break;
                case ContainerDirection.Vertical:
                    child.MinWidth = Width;
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
