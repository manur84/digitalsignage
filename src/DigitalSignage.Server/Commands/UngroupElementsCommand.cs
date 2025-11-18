using DigitalSignage.Core.Models;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.Commands;

/// <summary>
/// Command to ungroup elements from a group
/// </summary>
public class UngroupElementsCommand : IUndoableCommand
{
    private readonly ObservableCollection<DisplayElement> _elements;
    private readonly DisplayElement _groupElement;
    private readonly List<DisplayElement> _childElements;
    private readonly int _groupIndex;

    public string Description => $"Ungroup {_groupElement.Name}";

    public UngroupElementsCommand(
        ObservableCollection<DisplayElement> elements,
        DisplayElement groupElement)
    {
        _elements = elements ?? throw new ArgumentNullException(nameof(elements));
        _groupElement = groupElement ?? throw new ArgumentNullException(nameof(groupElement));

        if (!_groupElement.IsGroup)
        {
            throw new ArgumentException("Element must be a group to ungroup", nameof(groupElement));
        }

        _childElements = _groupElement.Children.ToList();
        _groupIndex = _elements.IndexOf(_groupElement);
    }

    public void Execute()
    {
        // Remove the group element
        _elements.Remove(_groupElement);

        // Add child elements back to layout with absolute positions
        foreach (var element in _childElements)
        {
            // Convert position back to absolute
            element.Position.X += _groupElement.Position.X;
            element.Position.Y += _groupElement.Position.Y;
            element.ParentId = null;

            // Add at the same position where the group was
            _elements.Insert(_groupIndex, element);
        }

        // Clear children from group (but keep reference for undo)
        _groupElement.Children.Clear();
    }

    public void Undo()
    {
        // Remove child elements from layout
        foreach (var element in _childElements)
        {
            _elements.Remove(element);

            // Convert position back to relative
            element.Position.X -= _groupElement.Position.X;
            element.Position.Y -= _groupElement.Position.Y;
            element.ParentId = _groupElement.Id;
        }

        // Restore children to group
        _groupElement.Children.Clear();
        foreach (var element in _childElements)
        {
            _groupElement.Children.Add(element);
        }

        // Re-add the group element
        if (_groupIndex >= _elements.Count)
        {
            _elements.Add(_groupElement);
        }
        else
        {
            _elements.Insert(_groupIndex, _groupElement);
        }
    }
}
