using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Services;

/// <summary>
/// Service for aligning and distributing elements
/// </summary>
public class AlignmentService
{
    /// <summary>
    /// Aligns all selected elements to the leftmost edge (minimum X position).
    /// Requires at least 2 elements to perform alignment.
    /// </summary>
    /// <param name="elements">The elements to align. Must contain at least 2 elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void AlignLeft(IEnumerable<DisplayElement> elements)
    {
        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var minX = elementList.Min(e => e.Position.X);
        foreach (var element in elementList)
        {
            element.Position.X = minX;
        }
    }

    /// <summary>
    /// Aligns all selected elements to the rightmost edge (maximum X + Width position).
    /// Requires at least 2 elements to perform alignment.
    /// </summary>
    /// <param name="elements">The elements to align. Must contain at least 2 elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void AlignRight(IEnumerable<DisplayElement> elements)
    {
        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var maxRight = elementList.Max(e => e.Position.X + e.Size.Width);
        foreach (var element in elementList)
        {
            element.Position.X = maxRight - element.Size.Width;
        }
    }

    /// <summary>
    /// Aligns all selected elements to the topmost edge (minimum Y position).
    /// Requires at least 2 elements to perform alignment.
    /// </summary>
    /// <param name="elements">The elements to align. Must contain at least 2 elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void AlignTop(IEnumerable<DisplayElement> elements)
    {
        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var minY = elementList.Min(e => e.Position.Y);
        foreach (var element in elementList)
        {
            element.Position.Y = minY;
        }
    }

    /// <summary>
    /// Aligns all selected elements to the bottommost edge (maximum Y + Height position).
    /// Requires at least 2 elements to perform alignment.
    /// </summary>
    /// <param name="elements">The elements to align. Must contain at least 2 elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void AlignBottom(IEnumerable<DisplayElement> elements)
    {
        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

        var elementList = elements.ToList();
        if (elementList.Count < 2) return;

        var maxBottom = elementList.Max(e => e.Position.Y + e.Size.Height);
        foreach (var element in elementList)
        {
            element.Position.Y = maxBottom - element.Size.Height;
        }
    }

    /// <summary>
    /// Centers all selected elements horizontally based on the bounding box of all elements.
    /// Each element is positioned at the horizontal center of the collective bounding box.
    /// Requires at least 2 elements to perform alignment.
    /// </summary>
    /// <param name="elements">The elements to center. Must contain at least 2 elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void CenterHorizontal(IEnumerable<DisplayElement> elements)
    {
        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

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
    /// Centers all selected elements vertically based on the bounding box of all elements.
    /// Each element is positioned at the vertical center of the collective bounding box.
    /// Requires at least 2 elements to perform alignment.
    /// </summary>
    /// <param name="elements">The elements to center. Must contain at least 2 elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void CenterVertical(IEnumerable<DisplayElement> elements)
    {
        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

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
    /// Centers a single element on the canvas based on the canvas dimensions.
    /// The element is positioned at the exact center of the canvas.
    /// </summary>
    /// <param name="element">The element to center on the canvas.</param>
    /// <param name="canvasWidth">The width of the canvas in pixels.</param>
    /// <param name="canvasHeight">The height of the canvas in pixels.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="element"/> is null.</exception>
    public void CenterOnCanvas(DisplayElement element, double canvasWidth, double canvasHeight)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        element.Position.X = (canvasWidth - element.Size.Width) / 2;
        element.Position.Y = (canvasHeight - element.Size.Height) / 2;
    }

    /// <summary>
    /// Distributes elements horizontally with equal spacing between them.
    /// Elements are ordered by their current X position (left to right).
    /// The leftmost and rightmost elements remain fixed, and intermediate elements are spaced evenly.
    /// Requires at least 3 elements to perform distribution.
    /// </summary>
    /// <param name="elements">The elements to distribute. Must contain at least 3 elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void DistributeHorizontal(IEnumerable<DisplayElement> elements)
    {
        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

        var elementList = elements.OrderBy(e => e.Position.X).ToList();
        if (elementList.Count < 3) return;

        var totalWidth = elementList.Sum(e => e.Size.Width);
        var firstElement = elementList[0]; // Index access - safe after Count check
        var lastElement = elementList[^1]; // Index from end - safe after Count check
        var minX = firstElement.Position.X;
        var maxRight = lastElement.Position.X + lastElement.Size.Width;
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
    /// Distributes elements vertically with equal spacing between them.
    /// Elements are ordered by their current Y position (top to bottom).
    /// The topmost and bottommost elements remain fixed, and intermediate elements are spaced evenly.
    /// Requires at least 3 elements to perform distribution.
    /// </summary>
    /// <param name="elements">The elements to distribute. Must contain at least 3 elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elements"/> is null.</exception>
    public void DistributeVertical(IEnumerable<DisplayElement> elements)
    {
        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

        var elementList = elements.OrderBy(e => e.Position.Y).ToList();
        if (elementList.Count < 3) return;

        var totalHeight = elementList.Sum(e => e.Size.Height);
        var firstElement = elementList[0]; // Index access - safe after Count check
        var lastElement = elementList[^1]; // Index from end - safe after Count check
        var minY = firstElement.Position.Y;
        var maxBottom = lastElement.Position.Y + lastElement.Size.Height;
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
