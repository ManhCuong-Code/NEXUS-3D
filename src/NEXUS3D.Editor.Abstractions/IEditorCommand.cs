namespace NEXUS3D.Editor.Abstractions;

/// <summary>
/// Reversible editor action supporting execute and undo.
/// </summary>
public interface IEditorCommand
{
    /// <summary>
    /// Human-readable title describing the action (e.g. "Translate Node", "Rotate Selection").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes or re-applies the edit action.
    /// </summary>
    void Execute();

    /// <summary>
    /// Reverts the effects of this edit action back to the preceding state.
    /// </summary>
    void Undo();
}

