namespace DigitalSignage.Server.Commands;

/// <summary>
/// Interface for commands that can be undone and redone
/// </summary>
public interface IUndoableCommand
{
    /// <summary>
    /// Executes the command
    /// </summary>
    void Execute();

    /// <summary>
    /// Undoes the command, restoring the previous state
    /// </summary>
    void Undo();

    /// <summary>
    /// Gets a description of the command for display purposes
    /// </summary>
    string Description { get; }
}
