using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;

namespace NEXUS3D.App.Services.Shortcuts;

/// <summary>
/// Production implementation of <see cref="IKeyboardShortcutService"/> providing centralized
/// shortcut routing, input guarding for text controls, conflict detection, and JSON persistence.
/// </summary>
public sealed class KeyboardShortcutService : IKeyboardShortcutService
{
    private readonly IApplicationPathsService _pathsService;
    private readonly ILogger<KeyboardShortcutService> _logger;
    private readonly List<KeyboardShortcutDefinition> _shortcuts = [];
    private readonly string _settingsFilePath;

    public IReadOnlyList<KeyboardShortcutDefinition> Shortcuts => _shortcuts;

    public event EventHandler<string>? ShortcutTriggered;

    public KeyboardShortcutService(
        IApplicationPathsService pathsService,
        ILogger<KeyboardShortcutService> logger)
    {
        _pathsService = pathsService;
        _logger = logger;
        _settingsFilePath = Path.Combine(_pathsService.AppDataRootDirectory, "shortcuts.json");

        InitializeDefaultShortcuts();
    }

    private void InitializeDefaultShortcuts()
    {
        _shortcuts.Clear();

        // 1. File Management
        Add("file.new", "New Project", "Create a new empty 3D workspace project", ShortcutCategory.FileManagement, ShortcutScope.Global, Key.N, ModifierKeys.Control);
        Add("file.open", "Open File", "Open an existing 3D model, schematic or JSON file", ShortcutCategory.FileManagement, ShortcutScope.Global, Key.O, ModifierKeys.Control);
        Add("file.save", "Save Project", "Save the active model or scene", ShortcutCategory.FileManagement, ShortcutScope.Global, Key.S, ModifierKeys.Control);
        Add("file.save_as", "Save As...", "Save current model to a new target destination", ShortcutCategory.FileManagement, ShortcutScope.Global, Key.S, ModifierKeys.Control | ModifierKeys.Shift);
        Add("file.import", "Import Model", "Import external 3D geometry into current workspace", ShortcutCategory.FileManagement, ShortcutScope.Global, Key.I, ModifierKeys.Control);
        Add("file.export", "Export Model", "Export scene to OBJ, Schematic, JSON or Bedrock", ShortcutCategory.FileManagement, ShortcutScope.Global, Key.E, ModifierKeys.Control);

        // 2. Model & Selection Editing
        Add("edit.undo", "Undo", "Revert last editing or transform operation", ShortcutCategory.ModelEditing, ShortcutScope.Global, Key.Z, ModifierKeys.Control);
        Add("edit.redo", "Redo", "Reapply previously undone operation", ShortcutCategory.ModelEditing, ShortcutScope.Global, Key.Y, ModifierKeys.Control);
        Add("edit.cut", "Cut", "Cut selected geometry nodes to clipboard", ShortcutCategory.ModelEditing, ShortcutScope.Global, Key.X, ModifierKeys.Control);
        Add("edit.copy", "Copy", "Copy selected geometry nodes to clipboard", ShortcutCategory.ModelEditing, ShortcutScope.Global, Key.C, ModifierKeys.Control);
        Add("edit.paste", "Paste", "Paste geometry from clipboard", ShortcutCategory.ModelEditing, ShortcutScope.Global, Key.V, ModifierKeys.Control);
        Add("edit.delete", "Delete Selected", "Remove currently selected object or voxels", ShortcutCategory.ModelEditing, ShortcutScope.Global, Key.Delete, ModifierKeys.None);
        Add("edit.select_all", "Select All", "Select all root objects and elements in active scene", ShortcutCategory.ModelEditing, ShortcutScope.Global, Key.A, ModifierKeys.Control);
        Add("edit.search", "Search Workspace", "Focus search bar for models, formats or settings", ShortcutCategory.ModelEditing, ShortcutScope.Global, Key.F, ModifierKeys.Control);

        // 3. General & System
        Add("help.shortcuts", "Keyboard Shortcuts", "Display interactive keyboard shortcut cheatsheet", ShortcutCategory.General, ShortcutScope.Global, Key.F1, ModifierKeys.None);
        Add("view.fullscreen", "Toggle Fullscreen", "Toggle borderless fullscreen desktop display", ShortcutCategory.General, ShortcutScope.Global, Key.F11, ModifierKeys.None);
        Add("general.escape", "Cancel / Deselect", "Close active popup or clear current selection", ShortcutCategory.General, ShortcutScope.Global, Key.Escape, ModifierKeys.None);

        // 4. 3D Viewport & Interactive Tools (Context-aware: ViewportOnly scope)
        Add("viewport.tool.select", "Select Tool", "Activate node selection mode in 3D viewport", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.Q, ModifierKeys.None);
        Add("viewport.tool.move", "Move Tool", "Activate 3D translation gizmo", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.W, ModifierKeys.None);
        Add("viewport.tool.rotate", "Rotate Tool", "Activate 3D rotation gizmo", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.E, ModifierKeys.None);
        Add("viewport.tool.scale", "Scale Tool", "Activate 3D scale gizmo", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.R, ModifierKeys.None);
        Add("viewport.tool.pivot", "Pivot Tool", "Center or manipulate transform pivot point", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.T, ModifierKeys.None);

        Add("viewport.cam.focus_selected", "Focus Selected", "Frame camera onto currently selected object", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.F, ModifierKeys.None);
        Add("viewport.cam.focus_all", "Focus Entire Scene", "Fit entire model bounding box within view", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.Home, ModifierKeys.None);
        Add("viewport.view.toggle_grid", "Toggle Grid Floor", "Show or hide 3D world reference grid", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.G, ModifierKeys.None);
        Add("viewport.view.toggle_axes", "Toggle Coordinate Axes", "Show or hide 3D coordinate tripod indicator", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.X, ModifierKeys.None);
        Add("viewport.view.toggle_shading", "Toggle Shading Mode", "Cycle between Textured, Flat, and Wireframe", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.Z, ModifierKeys.None);

        Add("viewport.cam.front", "Camera Front View", "Align camera along negative Z-axis", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.D1, ModifierKeys.None);
        Add("viewport.cam.back", "Camera Back View", "Align camera along positive Z-axis", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.D2, ModifierKeys.None);
        Add("viewport.cam.left", "Camera Left View", "Align camera along negative X-axis", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.D3, ModifierKeys.None);
        Add("viewport.cam.right", "Camera Right View", "Align camera along positive X-axis", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.D4, ModifierKeys.None);
        Add("viewport.cam.top", "Camera Top View", "Align camera looking downward (Y-down)", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.D5, ModifierKeys.None);
        Add("viewport.cam.bottom", "Camera Bottom View", "Align camera looking upward (Y-up)", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.D6, ModifierKeys.None);
        Add("viewport.cam.perspective", "Perspective View", "Switch camera to Perspective projection", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.D7, ModifierKeys.None);
        Add("viewport.cam.orthographic", "Orthographic View", "Switch camera to Orthographic isometric projection", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.D8, ModifierKeys.None);
        Add("viewport.cam.reset", "Reset Camera", "Reset camera orientation and distance to default", ShortcutCategory.Viewport3D, ShortcutScope.ViewportOnly, Key.D0, ModifierKeys.None);

        // 5. Page Navigation (Global Scope)
        Add("nav.home", "Navigate to Home", "Switch workspace to Home Dashboard", ShortcutCategory.Navigation, ShortcutScope.Global, Key.D1, ModifierKeys.Control);
        Add("nav.import", "Navigate to Import", "Switch workspace to Import dialog", ShortcutCategory.Navigation, ShortcutScope.Global, Key.D2, ModifierKeys.Control);
        Add("nav.convert", "Navigate to Convert", "Switch workspace to 3D Converter", ShortcutCategory.Navigation, ShortcutScope.Global, Key.D3, ModifierKeys.Control);
        Add("nav.editor", "Navigate to 3D Editor", "Switch workspace to 3D Viewport Editor", ShortcutCategory.Navigation, ShortcutScope.Global, Key.D4, ModifierKeys.Control);
        Add("nav.voxelization", "Navigate to Voxelization", "Switch workspace to Voxelization Engine", ShortcutCategory.Navigation, ShortcutScope.Global, Key.D5, ModifierKeys.Control);
        Add("nav.batch", "Navigate to Batch Processing", "Switch workspace to Batch Conversion Studio", ShortcutCategory.Navigation, ShortcutScope.Global, Key.D6, ModifierKeys.Control);
        Add("nav.plugins", "Navigate to Plugin Hub", "Switch workspace to Extension & Plugin Hub", ShortcutCategory.Navigation, ShortcutScope.Global, Key.D7, ModifierKeys.Control);
        Add("nav.settings", "Navigate to Settings", "Switch workspace to Settings & Preferences", ShortcutCategory.Navigation, ShortcutScope.Global, Key.OemComma, ModifierKeys.Control);
    }

    private void Add(string id, string name, string desc, ShortcutCategory cat, ShortcutScope scope, Key defKey, ModifierKeys defMod)
    {
        _shortcuts.Add(new KeyboardShortcutDefinition(id, name, desc, cat, scope, defKey, defMod));
    }

    public bool ProcessKeyDown(Key key, ModifierKeys modifiers, bool isTextInputFocused, ShortcutScope currentScope)
    {
        // Text Input Guard:
        // When typing in a TextBox, only allow standard text editing shortcuts!
        if (isTextInputFocused)
        {
            bool isTextShortcut = modifiers == ModifierKeys.Control &&
                                  key is Key.C or Key.V or Key.X or Key.Z or Key.Y or Key.A;
            if (!isTextShortcut)
            {
                return false;
            }
        }

        // Find matching shortcut
        var match = _shortcuts.FirstOrDefault(s =>
            s.IsEnabled &&
            s.Key == key &&
            s.Modifiers == modifiers &&
            (s.Scope == ShortcutScope.Global || s.Scope == currentScope));

        if (match is not null)
        {
            _logger.LogDebug("Shortcut triggered: '{Id}' ({Formatted})", match.Id, match.FormattedGesture);
            ShortcutTriggered?.Invoke(this, match.Id);
            return true;
        }

        return false;
    }

    public bool TryTrigger(string shortcutId)
    {
        var match = _shortcuts.FirstOrDefault(s => s.Id == shortcutId && s.IsEnabled);
        if (match is not null)
        {
            ShortcutTriggered?.Invoke(this, shortcutId);
            return true;
        }
        return false;
    }

    public ShortcutConflictResult CheckConflict(string shortcutId, Key key, ModifierKeys modifiers, ShortcutScope scope)
    {
        var conflict = _shortcuts.FirstOrDefault(s =>
            s.Id != shortcutId &&
            s.IsEnabled &&
            s.Key == key &&
            s.Modifiers == modifiers &&
            (s.Scope == ShortcutScope.Global || scope == ShortcutScope.Global || s.Scope == scope));

        return new ShortcutConflictResult(conflict is not null, conflict);
    }

    public void UpdateShortcut(string shortcutId, Key newKey, ModifierKeys newModifiers)
    {
        var sc = _shortcuts.FirstOrDefault(s => s.Id == shortcutId);
        if (sc is null) return;

        sc.Key = newKey;
        sc.Modifiers = newModifiers;
    }

    public void ResetShortcut(string shortcutId)
    {
        var sc = _shortcuts.FirstOrDefault(s => s.Id == shortcutId);
        sc?.ResetToDefault();
    }

    public void ResetAllToDefaults()
    {
        foreach (var sc in _shortcuts)
        {
            sc.ResetToDefault();
        }
    }

    public void SetShortcutEnabled(string shortcutId, bool isEnabled)
    {
        var sc = _shortcuts.FirstOrDefault(s => s.Id == shortcutId);
        if (sc is not null)
        {
            sc.IsEnabled = isEnabled;
        }
    }

    public async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath)) return;

        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
            ImportSettingsJson(json);
            _logger.LogInformation("Loaded custom keyboard shortcut configurations from {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load shortcut settings from {Path}", _settingsFilePath);
        }
    }

    public async Task SaveSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = ExportSettingsJson();
            _pathsService.EnsureDirectoriesExist();
            await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken);
            _logger.LogInformation("Saved custom keyboard shortcut configurations to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save shortcut settings to {Path}", _settingsFilePath);
        }
    }

    public string ExportSettingsJson()
    {
        var data = _shortcuts
            .Where(s => s.IsCustomized || !s.IsEnabled)
            .Select(s => new ShortcutOverrideDto
            {
                Id = s.Id,
                Key = (int)s.Key,
                Modifiers = (int)s.Modifiers,
                IsEnabled = s.IsEnabled
            })
            .ToList();

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    public bool ImportSettingsJson(string json)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<ShortcutOverrideDto>>(json);
            if (list is null) return false;

            foreach (var item in list)
            {
                var sc = _shortcuts.FirstOrDefault(s => s.Id == item.Id);
                if (sc is not null)
                {
                    sc.Key = (Key)item.Key;
                    sc.Modifiers = (ModifierKeys)item.Modifiers;
                    sc.IsEnabled = item.IsEnabled;
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class ShortcutOverrideDto
    {
        public string Id { get; set; } = string.Empty;
        public int Key { get; set; }
        public int Modifiers { get; set; }
        public bool IsEnabled { get; set; }
    }
}

