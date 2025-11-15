using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace DigitalSignage.Server.Controls;

/// <summary>
/// Adorner that displays smart alignment guides when dragging elements
/// </summary>
public class SmartGuidesAdorner : Adorner
{
    private readonly List<AlignmentGuide> _guides = new();
    private readonly Pen _guidePen;
    private const double SNAP_THRESHOLD = 5.0; // Snap within 5 pixels

    public SmartGuidesAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _guidePen = new Pen(Brushes.DeepSkyBlue, 1.0)
        {
            DashStyle = DashStyles.Dash
        };
    }

    /// <summary>
    /// Calculate guides for the dragging element against other elements
    /// </summary>
    public void CalculateGuides(Rect draggingRect, IEnumerable<Rect> otherRects)
    {
        _guides.Clear();

        foreach (var rect in otherRects)
        {
            // Left edge alignment
            if (Math.Abs(draggingRect.Left - rect.Left) < SNAP_THRESHOLD)
            {
                _guides.Add(new AlignmentGuide
                {
                    IsVertical = true,
                    Position = rect.Left,
                    Start = Math.Min(draggingRect.Top, rect.Top),
                    End = Math.Max(draggingRect.Bottom, rect.Bottom)
                });
            }

            // Right edge alignment
            if (Math.Abs(draggingRect.Right - rect.Right) < SNAP_THRESHOLD)
            {
                _guides.Add(new AlignmentGuide
                {
                    IsVertical = true,
                    Position = rect.Right,
                    Start = Math.Min(draggingRect.Top, rect.Top),
                    End = Math.Max(draggingRect.Bottom, rect.Bottom)
                });
            }

            // Center vertical alignment
            var draggingCenterX = draggingRect.Left + draggingRect.Width / 2;
            var rectCenterX = rect.Left + rect.Width / 2;
            if (Math.Abs(draggingCenterX - rectCenterX) < SNAP_THRESHOLD)
            {
                _guides.Add(new AlignmentGuide
                {
                    IsVertical = true,
                    Position = rectCenterX,
                    Start = Math.Min(draggingRect.Top, rect.Top),
                    End = Math.Max(draggingRect.Bottom, rect.Bottom)
                });
            }

            // Top edge alignment
            if (Math.Abs(draggingRect.Top - rect.Top) < SNAP_THRESHOLD)
            {
                _guides.Add(new AlignmentGuide
                {
                    IsVertical = false,
                    Position = rect.Top,
                    Start = Math.Min(draggingRect.Left, rect.Left),
                    End = Math.Max(draggingRect.Right, rect.Right)
                });
            }

            // Bottom edge alignment
            if (Math.Abs(draggingRect.Bottom - rect.Bottom) < SNAP_THRESHOLD)
            {
                _guides.Add(new AlignmentGuide
                {
                    IsVertical = false,
                    Position = rect.Bottom,
                    Start = Math.Min(draggingRect.Left, rect.Left),
                    End = Math.Max(draggingRect.Right, rect.Right)
                });
            }

            // Center horizontal alignment
            var draggingCenterY = draggingRect.Top + draggingRect.Height / 2;
            var rectCenterY = rect.Top + rect.Height / 2;
            if (Math.Abs(draggingCenterY - rectCenterY) < SNAP_THRESHOLD)
            {
                _guides.Add(new AlignmentGuide
                {
                    IsVertical = false,
                    Position = rectCenterY,
                    Start = Math.Min(draggingRect.Left, rect.Left),
                    End = Math.Max(draggingRect.Right, rect.Right)
                });
            }
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Calculate snapped position based on active guides
    /// </summary>
    public Point GetSnappedPosition(Point currentPosition, Size elementSize)
    {
        var snappedX = currentPosition.X;
        var snappedY = currentPosition.Y;

        foreach (var guide in _guides)
        {
            if (guide.IsVertical)
            {
                // Check left edge snap
                if (Math.Abs(currentPosition.X - guide.Position) < SNAP_THRESHOLD)
                {
                    snappedX = guide.Position;
                }
                // Check right edge snap
                else if (Math.Abs((currentPosition.X + elementSize.Width) - guide.Position) < SNAP_THRESHOLD)
                {
                    snappedX = guide.Position - elementSize.Width;
                }
                // Check center snap
                else if (Math.Abs((currentPosition.X + elementSize.Width / 2) - guide.Position) < SNAP_THRESHOLD)
                {
                    snappedX = guide.Position - elementSize.Width / 2;
                }
            }
            else
            {
                // Check top edge snap
                if (Math.Abs(currentPosition.Y - guide.Position) < SNAP_THRESHOLD)
                {
                    snappedY = guide.Position;
                }
                // Check bottom edge snap
                else if (Math.Abs((currentPosition.Y + elementSize.Height) - guide.Position) < SNAP_THRESHOLD)
                {
                    snappedY = guide.Position - elementSize.Height;
                }
                // Check center snap
                else if (Math.Abs((currentPosition.Y + elementSize.Height / 2) - guide.Position) < SNAP_THRESHOLD)
                {
                    snappedY = guide.Position - elementSize.Height / 2;
                }
            }
        }

        return new Point(snappedX, snappedY);
    }

    /// <summary>
    /// Clear all guides
    /// </summary>
    public void ClearGuides()
    {
        _guides.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        foreach (var guide in _guides)
        {
            if (guide.IsVertical)
            {
                // Draw vertical line
                drawingContext.DrawLine(_guidePen,
                    new Point(guide.Position, guide.Start),
                    new Point(guide.Position, guide.End));
            }
            else
            {
                // Draw horizontal line
                drawingContext.DrawLine(_guidePen,
                    new Point(guide.Start, guide.Position),
                    new Point(guide.End, guide.Position));
            }
        }
    }

    private class AlignmentGuide
    {
        public bool IsVertical { get; set; }
        public double Position { get; set; }
        public double Start { get; set; }
        public double End { get; set; }
    }
}
