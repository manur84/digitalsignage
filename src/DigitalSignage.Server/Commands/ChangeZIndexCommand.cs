using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Commands;

/// <summary>
/// Command for changing the Z-Index of an element
/// </summary>
public class ChangeZIndexCommand : IUndoableCommand
{
    private readonly DisplayElement _element;
    private readonly int _oldZIndex;
    private readonly int _newZIndex;

    public string Description => $"Change Z-Index of '{_element.Name}' to {_newZIndex}";

    public ChangeZIndexCommand(DisplayElement element, int oldZIndex, int newZIndex)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _oldZIndex = oldZIndex;
        _newZIndex = newZIndex;
    }

    public void Execute()
    {
        _element.ZIndex = _newZIndex;
    }

    public void Undo()
    {
        _element.ZIndex = _oldZIndex;
    }
}
