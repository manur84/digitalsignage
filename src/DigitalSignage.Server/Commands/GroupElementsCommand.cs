using DigitalSignage.Core.Models;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.Commands;

/// <summary>
/// Command to group multiple selected elements into a single group
/// </summary>
public class GroupElementsCommand : IUndoableCommand
{
    private readonly ObservableCollection<DisplayElement> _elements;
    private readonly List<DisplayElement> _selectedElements;
    private readonly DisplayElement _groupElement;
    private readonly ConcurrentDictionary<DisplayElement, int> _originalIndices = new();

    public string Description => $"Group {_selectedElements.Count} elements";

    public GroupElementsCommand(
        ObservableCollection<DisplayElement> elements,
        IEnumerable<DisplayElement> selectedElements)
    {
        _elements = elements ?? throw new ArgumentNullException(nameof(elements));
        _selectedElements = selectedElements?.ToList() ?? throw new ArgumentNullException(nameof(selectedElements));

        if (_selectedElements.Count < 2)
        {
            throw new ArgumentException("At least 2 elements must be selected to create a group", nameof(selectedElements));
        }

        // Create a new group element
        _groupElement = new DisplayElement
        {
            Id = Guid.NewGuid().ToString(),
            Type = "group",
            Name = $"Group ({_selectedElements.Count} elements)",
            Children = new List<DisplayElement>()
        };

        _groupElement.InitializeDefaultProperties();

        // Calculate group bounds
        CalculateGroupBounds();
    }

    public void Execute()
    {
        // Store original indices and remove elements from layout
        foreach (var element in _selectedElements)
        {
            _originalIndices[element] = _elements.IndexOf(element);
            _elements.Remove(element);

            // Adjust element positions relative to group
            element.Position.X -= _groupElement.Position.X;
            element.Position.Y -= _groupElement.Position.Y;
            element.ParentId = _groupElement.Id;

            // Add to group
            _groupElement.Children.Add(element);
        }

        // Add the group element to the layout
        _elements.Add(_groupElement);
    }

    public void Undo()
    {
        // Remove the group element
        _elements.Remove(_groupElement);

        // Restore original elements at their original positions
        foreach (var element in _selectedElements.OrderBy(e => _originalIndices[e]))
        {
            // Convert position back to absolute
            element.Position.X += _groupElement.Position.X;
            element.Position.Y += _groupElement.Position.Y;
            element.ParentId = null;

            // Insert at original index
            var originalIndex = _originalIndices[element];
            if (originalIndex >= _elements.Count)
            {
                _elements.Add(element);
            }
            else
            {
                _elements.Insert(originalIndex, element);
            }
        }

        // Clear children from group
        _groupElement.Children.Clear();
    }

    /// <summary>
    /// Calculates the bounding box that encompasses all selected elements
    /// </summary>
    private void CalculateGroupBounds()
    {
        var minX = _selectedElements.Min(e => e.Position.X);
        var minY = _selectedElements.Min(e => e.Position.Y);
        var maxX = _selectedElements.Max(e => e.Position.X + e.Size.Width);
        var maxY = _selectedElements.Max(e => e.Position.Y + e.Size.Height);

        _groupElement.Position.X = minX;
        _groupElement.Position.Y = minY;
        _groupElement.Size.Width = maxX - minX;
        _groupElement.Size.Height = maxY - minY;

        // Use the highest ZIndex from selected elements
        _groupElement.ZIndex = _selectedElements.Max(e => e.ZIndex);
    }
}
