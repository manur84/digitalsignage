using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Adorner that displays alignment guides (snap lines) when moving elements
/// Provides visual feedback similar to Figma, Canva, Adobe XD
/// </summary>
public class AlignmentGuidesAdorner : Adorner
{
    private readonly List<AlignmentGuide> _guides = new();
    private const double SNAP_THRESHOLD = 5.0; // pixels

    public AlignmentGuidesAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false; // Don't interfere with mouse events
    }

    /// <summary>
    /// Adds a guide to display
    /// </summary>
    public void AddGuide(AlignmentGuide guide)
    {
        _guides.Add(guide);
        InvalidateVisual();
    }

    /// <summary>
    /// Clears all guides
    /// </summary>
    public void ClearGuides()
    {
        _guides.Clear();
        InvalidateVisual();
    }

    /// <summary>
    /// Calculates alignment guides for an element being moved
    /// </summary>
    public Point CalculateSnappedPosition(Rect movingElementBounds, IEnumerable<Rect> otherElementBounds, Rect canvasBounds)
    {
        ClearGuides();

        var snappedPoint = new Point(movingElementBounds.X, movingElementBounds.Y);
        bool snappedX = false;
        bool snappedY = false;

        // Check alignment with canvas edges
        CheckCanvasAlignment(movingElementBounds, canvasBounds, ref snappedPoint, ref snappedX, ref snappedY);

        // Check alignment with other elements
        foreach (var otherBounds in otherElementBounds)
        {
            CheckElementAlignment(movingElementBounds, otherBounds, ref snappedPoint, ref snappedX, ref snappedY);
        }

        InvalidateVisual();
        return snappedPoint;
    }

    private void CheckCanvasAlignment(Rect movingBounds, Rect canvasBounds, ref Point snappedPoint, ref bool snappedX, ref bool snappedY)
    {
        // Left edge
        if (!snappedX && Math.Abs(movingBounds.Left - canvasBounds.Left) < SNAP_THRESHOLD)
        {
            snappedPoint.X = canvasBounds.Left;
            AddGuide(new AlignmentGuide
            {
                X1 = canvasBounds.Left,
                Y1 = canvasBounds.Top,
                X2 = canvasBounds.Left,
                Y2 = canvasBounds.Bottom,
                Type = AlignmentType.Left
            });
            snappedX = true;
        }

        // Right edge
        if (!snappedX && Math.Abs(movingBounds.Right - canvasBounds.Right) < SNAP_THRESHOLD)
        {
            snappedPoint.X = canvasBounds.Right - movingBounds.Width;
            AddGuide(new AlignmentGuide
            {
                X1 = canvasBounds.Right,
                Y1 = canvasBounds.Top,
                X2 = canvasBounds.Right,
                Y2 = canvasBounds.Bottom,
                Type = AlignmentType.Right
            });
            snappedX = true;
        }

        // Horizontal center
        if (!snappedX && Math.Abs(movingBounds.Left + movingBounds.Width / 2 - canvasBounds.Left - canvasBounds.Width / 2) < SNAP_THRESHOLD)
        {
            snappedPoint.X = canvasBounds.Left + canvasBounds.Width / 2 - movingBounds.Width / 2;
            AddGuide(new AlignmentGuide
            {
                X1 = canvasBounds.Left + canvasBounds.Width / 2,
                Y1 = canvasBounds.Top,
                X2 = canvasBounds.Left + canvasBounds.Width / 2,
                Y2 = canvasBounds.Bottom,
                Type = AlignmentType.CenterHorizontal
            });
            snappedX = true;
        }

        // Top edge
        if (!snappedY && Math.Abs(movingBounds.Top - canvasBounds.Top) < SNAP_THRESHOLD)
        {
            snappedPoint.Y = canvasBounds.Top;
            AddGuide(new AlignmentGuide
            {
                X1 = canvasBounds.Left,
                Y1 = canvasBounds.Top,
                X2 = canvasBounds.Right,
                Y2 = canvasBounds.Top,
                Type = AlignmentType.Top
            });
            snappedY = true;
        }

        // Bottom edge
        if (!snappedY && Math.Abs(movingBounds.Bottom - canvasBounds.Bottom) < SNAP_THRESHOLD)
        {
            snappedPoint.Y = canvasBounds.Bottom - movingBounds.Height;
            AddGuide(new AlignmentGuide
            {
                X1 = canvasBounds.Left,
                Y1 = canvasBounds.Bottom,
                X2 = canvasBounds.Right,
                Y2 = canvasBounds.Bottom,
                Type = AlignmentType.Bottom
            });
            snappedY = true;
        }

        // Vertical center
        if (!snappedY && Math.Abs(movingBounds.Top + movingBounds.Height / 2 - canvasBounds.Top - canvasBounds.Height / 2) < SNAP_THRESHOLD)
        {
            snappedPoint.Y = canvasBounds.Top + canvasBounds.Height / 2 - movingBounds.Height / 2;
            AddGuide(new AlignmentGuide
            {
                X1 = canvasBounds.Left,
                Y1 = canvasBounds.Top + canvasBounds.Height / 2,
                X2 = canvasBounds.Right,
                Y2 = canvasBounds.Top + canvasBounds.Height / 2,
                Type = AlignmentType.CenterVertical
            });
            snappedY = true;
        }
    }

    private void CheckElementAlignment(Rect movingBounds, Rect otherBounds, ref Point snappedPoint, ref bool snappedX, ref bool snappedY)
    {
        // Left edges align
        if (!snappedX && Math.Abs(movingBounds.Left - otherBounds.Left) < SNAP_THRESHOLD)
        {
            snappedPoint.X = otherBounds.Left;
            AddGuide(new AlignmentGuide
            {
                X1 = otherBounds.Left,
                Y1 = Math.Min(movingBounds.Top, otherBounds.Top),
                X2 = otherBounds.Left,
                Y2 = Math.Max(movingBounds.Bottom, otherBounds.Bottom),
                Type = AlignmentType.Left
            });
            snappedX = true;
        }

        // Right edges align
        if (!snappedX && Math.Abs(movingBounds.Right - otherBounds.Right) < SNAP_THRESHOLD)
        {
            snappedPoint.X = otherBounds.Right - movingBounds.Width;
            AddGuide(new AlignmentGuide
            {
                X1 = otherBounds.Right,
                Y1 = Math.Min(movingBounds.Top, otherBounds.Top),
                X2 = otherBounds.Right,
                Y2 = Math.Max(movingBounds.Bottom, otherBounds.Bottom),
                Type = AlignmentType.Right
            });
            snappedX = true;
        }

        // Horizontal centers align
        double movingCenterX = movingBounds.Left + movingBounds.Width / 2;
        double otherCenterX = otherBounds.Left + otherBounds.Width / 2;
        if (!snappedX && Math.Abs(movingCenterX - otherCenterX) < SNAP_THRESHOLD)
        {
            snappedPoint.X = otherCenterX - movingBounds.Width / 2;
            AddGuide(new AlignmentGuide
            {
                X1 = otherCenterX,
                Y1 = Math.Min(movingBounds.Top, otherBounds.Top),
                X2 = otherCenterX,
                Y2 = Math.Max(movingBounds.Bottom, otherBounds.Bottom),
                Type = AlignmentType.CenterHorizontal
            });
            snappedX = true;
        }

        // Top edges align
        if (!snappedY && Math.Abs(movingBounds.Top - otherBounds.Top) < SNAP_THRESHOLD)
        {
            snappedPoint.Y = otherBounds.Top;
            AddGuide(new AlignmentGuide
            {
                X1 = Math.Min(movingBounds.Left, otherBounds.Left),
                Y1 = otherBounds.Top,
                X2 = Math.Max(movingBounds.Right, otherBounds.Right),
                Y2 = otherBounds.Top,
                Type = AlignmentType.Top
            });
            snappedY = true;
        }

        // Bottom edges align
        if (!snappedY && Math.Abs(movingBounds.Bottom - otherBounds.Bottom) < SNAP_THRESHOLD)
        {
            snappedPoint.Y = otherBounds.Bottom - movingBounds.Height;
            AddGuide(new AlignmentGuide
            {
                X1 = Math.Min(movingBounds.Left, otherBounds.Left),
                Y1 = otherBounds.Bottom,
                X2 = Math.Max(movingBounds.Right, otherBounds.Right),
                Y2 = otherBounds.Bottom,
                Type = AlignmentType.Bottom
            });
            snappedY = true;
        }

        // Vertical centers align
        double movingCenterY = movingBounds.Top + movingBounds.Height / 2;
        double otherCenterY = otherBounds.Top + otherBounds.Height / 2;
        if (!snappedY && Math.Abs(movingCenterY - otherCenterY) < SNAP_THRESHOLD)
        {
            snappedPoint.Y = otherCenterY - movingBounds.Height / 2;
            AddGuide(new AlignmentGuide
            {
                X1 = Math.Min(movingBounds.Left, otherBounds.Left),
                Y1 = otherCenterY,
                X2 = Math.Max(movingBounds.Right, otherBounds.Right),
                Y2 = otherCenterY,
                Type = AlignmentType.CenterVertical
            });
            snappedY = true;
        }

        // Spacing indicators (distance between elements)
        AddSpacingIndicators(movingBounds, otherBounds);
    }

    private void AddSpacingIndicators(Rect movingBounds, Rect otherBounds)
    {
        const double MIN_SPACING_TO_SHOW = 2.0;
        const double MAX_SPACING_TO_SHOW = 100.0;

        // Horizontal spacing
        double horizontalSpacing = 0;
        if (movingBounds.Right < otherBounds.Left)
        {
            horizontalSpacing = otherBounds.Left - movingBounds.Right;
        }
        else if (movingBounds.Left > otherBounds.Right)
        {
            horizontalSpacing = movingBounds.Left - otherBounds.Right;
        }

        if (horizontalSpacing > MIN_SPACING_TO_SHOW && horizontalSpacing < MAX_SPACING_TO_SHOW)
        {
            double y = Math.Max(movingBounds.Top, otherBounds.Top) + Math.Min(movingBounds.Height, otherBounds.Height) / 2;
            AddGuide(new AlignmentGuide
            {
                X1 = movingBounds.Right < otherBounds.Left ? movingBounds.Right : otherBounds.Right,
                Y1 = y,
                X2 = movingBounds.Right < otherBounds.Left ? otherBounds.Left : movingBounds.Left,
                Y2 = y,
                Type = AlignmentType.Spacing,
                SpacingValue = horizontalSpacing
            });
        }

        // Vertical spacing
        double verticalSpacing = 0;
        if (movingBounds.Bottom < otherBounds.Top)
        {
            verticalSpacing = otherBounds.Top - movingBounds.Bottom;
        }
        else if (movingBounds.Top > otherBounds.Bottom)
        {
            verticalSpacing = movingBounds.Top - otherBounds.Bottom;
        }

        if (verticalSpacing > MIN_SPACING_TO_SHOW && verticalSpacing < MAX_SPACING_TO_SHOW)
        {
            double x = Math.Max(movingBounds.Left, otherBounds.Left) + Math.Min(movingBounds.Width, otherBounds.Width) / 2;
            AddGuide(new AlignmentGuide
            {
                X1 = x,
                Y1 = movingBounds.Bottom < otherBounds.Top ? movingBounds.Bottom : otherBounds.Bottom,
                X2 = x,
                Y2 = movingBounds.Bottom < otherBounds.Top ? otherBounds.Top : movingBounds.Top,
                Type = AlignmentType.Spacing,
                SpacingValue = verticalSpacing
            });
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        foreach (var guide in _guides)
        {
            DrawGuide(drawingContext, guide);
        }
    }

    private void DrawGuide(DrawingContext dc, AlignmentGuide guide)
    {
        Pen pen;

        if (guide.Type == AlignmentType.Spacing)
        {
            // Orange dashed line for spacing indicators
            pen = new Pen(Brushes.Orange, 1.5);
            pen.DashStyle = new DashStyle(new[] { 2.0, 2.0 }, 0);

            // Draw spacing line
            dc.DrawLine(pen, new Point(guide.X1, guide.Y1), new Point(guide.X2, guide.Y2));

            // Draw spacing value text
            var formattedText = new FormattedText(
                $"{guide.SpacingValue:F0}px",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                10,
                Brushes.Orange,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double textX = (guide.X1 + guide.X2) / 2 - formattedText.Width / 2;
            double textY = (guide.Y1 + guide.Y2) / 2 - formattedText.Height / 2;

            // Background for text
            dc.DrawRectangle(
                new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                null,
                new Rect(textX - 2, textY - 1, formattedText.Width + 4, formattedText.Height + 2));

            dc.DrawText(formattedText, new Point(textX, textY));
        }
        else
        {
            // Magenta/Red dashed line for alignment guides
            pen = new Pen(Brushes.Magenta, 1.5);
            pen.DashStyle = new DashStyle(new[] { 4.0, 2.0 }, 0);

            dc.DrawLine(pen, new Point(guide.X1, guide.Y1), new Point(guide.X2, guide.Y2));
        }
    }
}

/// <summary>
/// Represents an alignment guide line
/// </summary>
public class AlignmentGuide
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }
    public AlignmentType Type { get; set; }
    public double SpacingValue { get; set; }
}

/// <summary>
/// Type of alignment
/// </summary>
public enum AlignmentType
{
    Left,
    Right,
    Top,
    Bottom,
    CenterHorizontal,
    CenterVertical,
    Spacing
}
