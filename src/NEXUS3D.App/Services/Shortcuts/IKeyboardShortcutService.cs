using System.Windows.Input;

namespace NEXUS3D.App.Services.Shortcuts;

public readonly record struct ShortcutConflictResult(bool HasConflict, KeyboardShortcutDefinition? ConflictingShortcut);

/// <summary>
/// Service contract for centralized management, input interception, conflict detection,
/// and persistence of application-wide keyboard shortcuts.
/// </summary>
public interface IKeyboardShortcutService
{
    IReadOnlyList<KeyboardShortcutDefinition> Shortcuts { get; }

    event EventHandler<string>? ShortcutTriggered;

    bool ProcessKeyDown(Key key, ModifierKeys modifiers, bool isTextInputFocused, ShortcutScope currentScope);

    bool TryTrigger(string shortcutId);

    ShortcutConflictResult CheckConflict(string shortcutId, Key key, ModifierKeys modifiers, ShortcutScope scope);

    void UpdateShortcut(string shortcutId, Key newKey, ModifierKeys newModifiers);

    void ResetShortcut(string shortcutId);

    void ResetAllToDefaults();

    void SetShortcutEnabled(string shortcutId, bool isEnabled);

    Task LoadSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(CancellationToken cancellationToken = default);

    string ExportSettingsJson();

    bool ImportSettingsJson(string json);
}

