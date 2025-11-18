using System.Collections.Generic;
using System.Reflection;
using System.Collections.Concurrent;

namespace DigitalSignage.Server.Helpers;

public interface IUndoRedoCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

public class UndoRedoManager
{
    // Use LinkedList for O(1) head/tail operations
    private readonly LinkedList<IUndoRedoCommand> _undoList = new();
    private readonly LinkedList<IUndoRedoCommand> _redoList = new();
    private readonly int _maxStackSize;

    public event EventHandler? StateChanged;

    public bool CanUndo => _undoList.Count > 0;
    public bool CanRedo => _redoList.Count > 0;
    public string? NextUndoDescription => CanUndo ? _undoList.First!.Value.Description : null;
    public string? NextRedoDescription => CanRedo ? _redoList.First!.Value.Description : null;

    public UndoRedoManager(int maxStackSize = 100)
    {
        _maxStackSize = Math.Max(1, maxStackSize);
    }

    public void ExecuteCommand(IUndoRedoCommand command)
    {
        command.Execute();

        // Push to undo (front)
        _undoList.AddFirst(command);
        // Clear redo on new command
        _redoList.Clear();

        // Enforce capacity by removing the oldest (tail)
        if (_undoList.Count > _maxStackSize)
        {
            _undoList.RemoveLast();
        }

        OnStateChanged();
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var node = _undoList.First!;
        _undoList.RemoveFirst();

        var command = node.Value;
        command.Undo();

        // Push to redo
        _redoList.AddFirst(command);
        OnStateChanged();
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var node = _redoList.First!;
        _redoList.RemoveFirst();

        var command = node.Value;
        command.Execute();

        // Push back to undo
        _undoList.AddFirst(command);
        OnStateChanged();
    }

    public void Clear()
    {
        _undoList.Clear();
        _redoList.Clear();
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

    // Cache PropertyInfo to avoid repeated reflection lookups
    private static readonly ConcurrentDictionary<(Type type, string name), PropertyInfo?> PropertyCache = new();
    private readonly PropertyInfo? _propertyInfo;

    public PropertyChangeCommand(object target, string propertyName, object oldValue, object newValue)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        _oldValue = oldValue;
        _newValue = newValue;

        var key = (_target.GetType(), _propertyName);
        _propertyInfo = PropertyCache.GetOrAdd(key, k => k.type.GetProperty(k.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
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
        if (_propertyInfo != null)
        {
            _propertyInfo.SetValue(_target, value);
        }
        else
        {
            // Fallback if property not found (should be rare)
            var prop = _target.GetType().GetProperty(_propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            prop?.SetValue(_target, value);
        }
    }

    public string Description => $"Change {_propertyName}";
}
