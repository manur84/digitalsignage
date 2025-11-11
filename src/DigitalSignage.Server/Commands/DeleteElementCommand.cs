using DigitalSignage.Core.Models;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.Commands;

/// <summary>
/// Command for deleting an element from the canvas
/// </summary>
public class DeleteElementCommand : IUndoableCommand
{
    private readonly ObservableCollection<DisplayElement> _elements;
    private readonly DisplayElement _element;
    private int _originalIndex;

    public string Description => $"Delete {_element.Type} element '{_element.Name}'";

    public DeleteElementCommand(ObservableCollection<DisplayElement> elements, DisplayElement element)
    {
        _elements = elements ?? throw new ArgumentNullException(nameof(elements));
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _originalIndex = _elements.IndexOf(_element);
    }

    public void Execute()
    {
        _originalIndex = _elements.IndexOf(_element);
        _elements.Remove(_element);
    }

    public void Undo()
    {
        if (_originalIndex >= 0 && _originalIndex <= _elements.Count)
        {
            _elements.Insert(_originalIndex, _element);
        }
        else
        {
            _elements.Add(_element);
        }
    }
}
