using System.Collections.Generic;

namespace DigitalSignage.Server.Commands;

/// <summary>
/// Manages command history for undo/redo operations
/// </summary>
public class CommandHistory
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();
    private readonly int _maxHistorySize;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? UndoDescription => CanUndo ? _undoStack.Peek().Description : null;
    public string? RedoDescription => CanRedo ? _redoStack.Peek().Description : null;

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public event EventHandler? HistoryChanged;

    public CommandHistory(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Executes a command and adds it to the undo stack
    /// </summary>
    public void ExecuteCommand(IUndoableCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        command.Execute();
        _undoStack.Push(command);

        // Clear redo stack when a new command is executed
        _redoStack.Clear();

        // Limit history size
        if (_undoStack.Count > _maxHistorySize)
        {
            // Remove the oldest command
            var tempStack = new Stack<IUndoableCommand>();
            while (_undoStack.Count > 1)
            {
                tempStack.Push(_undoStack.Pop());
            }
            _undoStack.Clear();
            while (tempStack.Count > 0)
            {
                _undoStack.Push(tempStack.Pop());
            }
        }

        OnHistoryChanged();
    }

    /// <summary>
    /// Undoes the last command
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
            return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);

        OnHistoryChanged();
    }

    /// <summary>
    /// Redoes the last undone command
    /// </summary>
    public void Redo()
    {
        if (!CanRedo)
            return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);

        OnHistoryChanged();
    }

    /// <summary>
    /// Clears all history
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnHistoryChanged();
    }

    protected virtual void OnHistoryChanged()
    {
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
