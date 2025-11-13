using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for aligning and distributing elements
/// </summary>
public class AlignmentService
{
    /// <summary>
    /// Aligns selected elements to the left
    /// </summary>
    public void AlignLeft(IEnumerable<DisplayElement> elements)
    {
        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var minX = elementList.Min(e => e.Position.X);
        foreach (var element in elementList)
        {
            element.Position.X = minX;
        }
    }

    /// <summary>
    /// Aligns selected elements to the right
    /// </summary>
    public void AlignRight(IEnumerable<DisplayElement> elements)
    {
        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var maxRight = elementList.Max(e => e.Position.X + e.Size.Width);
        foreach (var element in elementList)
        {
            element.Position.X = maxRight - element.Size.Width;
        }
    }

    /// <summary>
    /// Aligns selected elements to the top
    /// </summary>
    public void AlignTop(IEnumerable<DisplayElement> elements)
    {
        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var minY = elementList.Min(e => e.Position.Y);
        foreach (var element in elementList)
        {
            element.Position.Y = minY;
        }
    }

    /// <summary>
    /// Aligns selected elements to the bottom
    /// </summary>
    public void AlignBottom(IEnumerable<DisplayElement> elements)
    {
        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var maxBottom = elementList.Max(e => e.Position.Y + e.Size.Height);
        foreach (var element in elementList)
        {
            element.Position.Y = maxBottom - element.Size.Height;
        }
    }

    /// <summary>
    /// Centers selected elements horizontally
    /// </summary>
    public void CenterHorizontal(IEnumerable<DisplayElement> elements)
    {
        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var minX = elementList.Min(e => e.Position.X);
        var maxRight = elementList.Max(e => e.Position.X + e.Size.Width);
        var centerX = (minX + maxRight) / 2;

        foreach (var element in elementList)
        {
            element.Position.X = centerX - (element.Size.Width / 2);
        }
    }

    /// <summary>
    /// Centers selected elements vertically
    /// </summary>
    public void CenterVertical(IEnumerable<DisplayElement> elements)
    {
        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var minY = elementList.Min(e => e.Position.Y);
        var maxBottom = elementList.Max(e => e.Position.Y + e.Size.Height);
        var centerY = (minY + maxBottom) / 2;

        foreach (var element in elementList)
        {
            element.Position.Y = centerY - (element.Size.Height / 2);
        }
    }

    /// <summary>
    /// Centers element on canvas
    /// </summary>
    public void CenterOnCanvas(DisplayElement element, double canvasWidth, double canvasHeight)
    {
        element.Position.X = (canvasWidth - element.Size.Width) / 2;
        element.Position.Y = (canvasHeight - element.Size.Height) / 2;
    }

    /// <summary>
    /// Distributes elements horizontally with equal spacing
    /// </summary>
    public void DistributeHorizontal(IEnumerable<DisplayElement> elements)
    {
        var elementList = elements.OrderBy(e => e.Position.X).ToList();
        if (elementList.Count < 3) return;

        var totalWidth = elementList.Sum(e => e.Size.Width);
        var minX = elementList.First().Position.X;
        var maxRight = elementList.Last().Position.X + elementList.Last().Size.Width;
        var availableSpace = (maxRight - minX) - totalWidth;
        var spacing = availableSpace / (elementList.Count - 1);

        var currentX = minX;
        foreach (var element in elementList)
        {
            element.Position.X = currentX;
            currentX += element.Size.Width + spacing;
        }
    }

    /// <summary>
    /// Distributes elements vertically with equal spacing
    /// </summary>
    public void DistributeVertical(IEnumerable<DisplayElement> elements)
    {
        var elementList = elements.OrderBy(e => e.Position.Y).ToList();
        if (elementList.Count < 3) return;

        var totalHeight = elementList.Sum(e => e.Size.Height);
        var minY = elementList.First().Position.Y;
        var maxBottom = elementList.Last().Position.Y + elementList.Last().Size.Height;
        var availableSpace = (maxBottom - minY) - totalHeight;
        var spacing = availableSpace / (elementList.Count - 1);

        var currentY = minY;
        foreach (var element in elementList)
        {
            element.Position.Y = currentY;
            currentY += element.Size.Height + spacing;
        }
    }
}
