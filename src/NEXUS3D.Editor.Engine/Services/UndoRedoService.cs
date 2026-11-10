using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.Editor.Engine.Services;

/// <summary>
/// Implements standard stack-based undo/redo history management for editor commands.
/// </summary>
public sealed class UndoRedoService : IUndoRedoService
{
    private readonly int _maxHistory;
    private readonly List<IEditorCommand> _undoStack = [];
    private readonly List<IEditorCommand> _redoStack = [];

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public IReadOnlyList<IEditorCommand> UndoHistory => _undoStack;
    public IReadOnlyList<IEditorCommand> RedoHistory => _redoStack;

    public event EventHandler? HistoryChanged;

    public UndoRedoService(int maxHistory = 100)
    {
        _maxHistory = Math.Max(10, maxHistory);
    }

    public void Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        _undoStack.Add(command);

        if (_undoStack.Count > _maxHistory)
        {
            _undoStack.RemoveAt(0);
        }

        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo) return;

        int lastIdx = _undoStack.Count - 1;
        var command = _undoStack[lastIdx];
        _undoStack.RemoveAt(lastIdx);

        command.Undo();
        _redoStack.Add(command);

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;

        int lastIdx = _redoStack.Count - 1;
        var command = _redoStack[lastIdx];
        _redoStack.RemoveAt(lastIdx);

        command.Execute();
        _undoStack.Add(command);

        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}

