using System.Collections.Generic;

namespace DigitalSignage.Server.Helpers;

public interface IUndoRedoCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

public class UndoRedoManager
{
    private readonly Stack<IUndoRedoCommand> _undoStack = new();
    private readonly Stack<IUndoRedoCommand> _redoStack = new();
    private readonly int _maxStackSize;

    public event EventHandler? StateChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string? NextUndoDescription => CanUndo ? _undoStack.Peek().Description : null;
    public string? NextRedoDescription => CanRedo ? _redoStack.Peek().Description : null;

    public UndoRedoManager(int maxStackSize = 100)
    {
        _maxStackSize = maxStackSize;
    }

    public void ExecuteCommand(IUndoRedoCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();

        // Limit stack size
        if (_undoStack.Count > _maxStackSize)
        {
            var items = _undoStack.ToList();
            items.RemoveAt(items.Count - 1);
            _undoStack.Clear();
            foreach (var item in items.AsEnumerable().Reverse())
            {
                _undoStack.Push(item);
            }
        }

        OnStateChanged();
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);

        OnStateChanged();
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);

        OnStateChanged();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnStateChanged();
    }

    protected virtual void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

// Example command implementations
public class AddElementCommand : IUndoRedoCommand
{
    private readonly Action _execute;
    private readonly Action _undo;
    private readonly string _description;

    public AddElementCommand(Action execute, Action undo, string description = "Add Element")
    {
        _execute = execute;
        _undo = undo;
        _description = description;
    }

    public void Execute() => _execute();
    public void Undo() => _undo();
    public string Description => _description;
}

public class PropertyChangeCommand : IUndoRedoCommand
{
    private readonly object _target;
    private readonly string _propertyName;
    private readonly object _oldValue;
    private readonly object _newValue;

    public PropertyChangeCommand(object target, string propertyName, object oldValue, object newValue)
    {
        _target = target;
        _propertyName = propertyName;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute()
    {
        SetProperty(_newValue);
    }

    public void Undo()
    {
        SetProperty(_oldValue);
    }

    private void SetProperty(object value)
    {
        var property = _target.GetType().GetProperty(_propertyName);
        property?.SetValue(_target, value);
    }

    public string Description => $"Change {_propertyName}";
}
