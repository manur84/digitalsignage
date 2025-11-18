using DigitalSignage.Core.Models;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.Commands;

/// <summary>
/// Command for adding an element to the canvas
/// </summary>
public class AddElementCommand : IUndoableCommand
{
    private readonly ObservableCollection<DisplayElement> _elements;
    private readonly DisplayElement _element;

    public string Description => $"Add {_element.Type} element '{_element.Name}'";

    public AddElementCommand(ObservableCollection<DisplayElement> elements, DisplayElement element)
    {
        _elements = elements ?? throw new ArgumentNullException(nameof(elements));
        _element = element ?? throw new ArgumentNullException(nameof(element));
    }

    public void Execute()
    {
        _elements.Add(_element);
    }

    public void Undo()
    {
        _elements.Remove(_element);
    }
}
