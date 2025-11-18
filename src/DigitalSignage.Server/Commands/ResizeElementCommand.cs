using DigitalSignage.Core.Models;

namespace DigitalSignage.Server.Commands;

/// <summary>
/// Command for resizing an element
/// </summary>
public class ResizeElementCommand : IUndoableCommand
{
    private readonly DisplayElement _element;
    private readonly Size _oldSize;
    private readonly Size _newSize;

    public string Description => $"Resize '{_element.Name}' to {_newSize.Width}x{_newSize.Height}";

    public ResizeElementCommand(DisplayElement element, Size oldSize, Size newSize)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _oldSize = oldSize ?? throw new ArgumentNullException(nameof(oldSize));
        _newSize = newSize ?? throw new ArgumentNullException(nameof(newSize));
    }

    public void Execute()
    {
        _element.Size = _newSize;
    }

    public void Undo()
    {
        _element.Size = _oldSize;
    }
}
