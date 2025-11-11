using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Commands;

/// <summary>
/// Command for moving an element
/// </summary>
public class MoveElementCommand : IUndoableCommand
{
    private readonly DisplayElement _element;
    private readonly Position _oldPosition;
    private readonly Position _newPosition;

    public string Description => $"Move '{_element.Name}' to ({_newPosition.X}, {_newPosition.Y})";

    public MoveElementCommand(DisplayElement element, Position oldPosition, Position newPosition)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _oldPosition = oldPosition ?? throw new ArgumentNullException(nameof(oldPosition));
        _newPosition = newPosition ?? throw new ArgumentNullException(nameof(newPosition));
    }

    public void Execute()
    {
        _element.Position = _newPosition;
    }

    public void Undo()
    {
        _element.Position = _oldPosition;
    }
}
