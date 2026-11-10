namespace NEXUS3D.Editor.Abstractions;

/// <summary>
/// Service managing the undo/redo history stacks for interactive editor actions.
/// </summary>
public interface IUndoRedoService
{
    /// <summary>
    /// Gets whether an action is available to be undone.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Gets whether an action is available to be redone.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Current undo history stack.
    /// </summary>
    IReadOnlyList<IEditorCommand> UndoHistory { get; }

    /// <summary>
    /// Current redo history stack.
    /// </summary>
    IReadOnlyList<IEditorCommand> RedoHistory { get; }

    /// <summary>
    /// Executes a command and pushes it onto the undo stack, clearing the redo stack.
    /// </summary>
    void Execute(IEditorCommand command);

    /// <summary>
    /// Reverts the top command on the undo stack and moves it to the redo stack.
    /// </summary>
    void Undo();

    /// <summary>
    /// Re-applies the top command on the redo stack and moves it to the undo stack.
    /// </summary>
    void Redo();

    /// <summary>
    /// Clears both undo and redo stacks.
    /// </summary>
    void Clear();

    /// <summary>
    /// Fired when undo/redo state or command history changes.
    /// </summary>
    event EventHandler HistoryChanged;
}

