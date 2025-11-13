using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Commands;

/// <summary>
/// Command for changing a property of an element
/// </summary>
public class ChangePropertyCommand : IUndoableCommand
{
    private readonly DisplayElement _element;
    private readonly string _propertyName;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public string Description => $"Change '{_element.Name}' {_propertyName}";

    public ChangePropertyCommand(DisplayElement element, string propertyName, object? oldValue, object? newValue)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute()
    {
        // Use indexer to trigger PropertyChanged notifications
        _element[_propertyName] = _newValue;
    }

    public void Undo()
    {
        // Use indexer to trigger PropertyChanged notifications
        _element[_propertyName] = _oldValue;
    }
}
