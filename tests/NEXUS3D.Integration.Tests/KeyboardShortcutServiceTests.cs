using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.App.Services.Shortcuts;
using NEXUS3D.App.Themes;
using NEXUS3D.Application.Interfaces;
using Xunit;

namespace NEXUS3D.Integration.Tests;

public sealed class KeyboardShortcutServiceTests
{
    private readonly MockApplicationPathsService _pathsService = new();
    private readonly KeyboardShortcutService _service;

    public KeyboardShortcutServiceTests()
    {
        _service = new KeyboardShortcutService(_pathsService, NullLogger<KeyboardShortcutService>.Instance);
    }

    [Fact]
    public void VerifyDefaultShortcuts_AreLoadedAndValid()
    {
        var shortcuts = _service.Shortcuts;
        Assert.NotEmpty(shortcuts);

        // Core actions must exist
        Assert.Contains(shortcuts, s => s.Id == "file.new" && s.Key == Key.N && s.Modifiers == ModifierKeys.Control);
        Assert.Contains(shortcuts, s => s.Id == "file.open" && s.Key == Key.O && s.Modifiers == ModifierKeys.Control);
        Assert.Contains(shortcuts, s => s.Id == "file.save" && s.Key == Key.S && s.Modifiers == ModifierKeys.Control);
        Assert.Contains(shortcuts, s => s.Id == "edit.undo" && s.Key == Key.Z && s.Modifiers == ModifierKeys.Control);
        Assert.Contains(shortcuts, s => s.Id == "viewport.tool.move" && s.Key == Key.W && s.Scope == ShortcutScope.ViewportOnly);
        Assert.Contains(shortcuts, s => s.Id == "viewport.tool.select" && s.Key == Key.Q && s.Scope == ShortcutScope.ViewportOnly);
        Assert.Contains(shortcuts, s => s.Id == "nav.home" && s.Key == Key.D1 && s.Modifiers == ModifierKeys.Control);
        Assert.Contains(shortcuts, s => s.Id == "help.shortcuts" && s.Key == Key.F1);

        // Check keycap formatting
        var saveAs = shortcuts.First(s => s.Id == "file.save_as");
        Assert.Contains("Ctrl", saveAs.KeycapTokens);
        Assert.Contains("Shift", saveAs.KeycapTokens);
        Assert.Contains("S", saveAs.KeycapTokens);
    }

    [Fact]
    public void ProcessKeyDown_GlobalShortcut_TriggersEvent()
    {
        string? triggeredId = null;
        _service.ShortcutTriggered += (s, id) => triggeredId = id;

        bool handled = _service.ProcessKeyDown(Key.O, ModifierKeys.Control, isTextInputFocused: false, ShortcutScope.Global);

        Assert.True(handled);
        Assert.Equal("file.open", triggeredId);
    }

    [Fact]
    public void ProcessKeyDown_TextInputFocused_GuardsStandardTyping()
    {
        string? triggeredId = null;
        _service.ShortcutTriggered += (s, id) => triggeredId = id;

        // User types 'W' or 'Q' inside a TextBox -> MUST NOT trigger 3D tools!
        bool handledW = _service.ProcessKeyDown(Key.W, ModifierKeys.None, isTextInputFocused: true, ShortcutScope.ViewportOnly);
        Assert.False(handledW);
        Assert.Null(triggeredId);

        // User types '1' inside a TextBox -> MUST NOT trigger Front view!
        bool handled1 = _service.ProcessKeyDown(Key.D1, ModifierKeys.None, isTextInputFocused: true, ShortcutScope.ViewportOnly);
        Assert.False(handled1);

        // User presses Ctrl+1 inside a TextBox -> MUST NOT trigger page navigation!
        bool handledNav = _service.ProcessKeyDown(Key.D1, ModifierKeys.Control, isTextInputFocused: true, ShortcutScope.Global);
        Assert.False(handledNav);

        // User presses Ctrl+C inside a TextBox -> ALLOWED (standard clipboard editing)
        bool handledCopy = _service.ProcessKeyDown(Key.C, ModifierKeys.Control, isTextInputFocused: true, ShortcutScope.Global);
        Assert.True(handledCopy);
        Assert.Equal("edit.copy", triggeredId);
    }

    [Fact]
    public void ProcessKeyDown_ContextScope_IsEnforced()
    {
        string? triggeredId = null;
        _service.ShortcutTriggered += (s, id) => triggeredId = id;

        // Viewport tool 'W' when workspace is Dashboard (Global scope) -> MUST NOT trigger!
        bool handledGlobal = _service.ProcessKeyDown(Key.W, ModifierKeys.None, isTextInputFocused: false, ShortcutScope.Global);
        Assert.False(handledGlobal);
        Assert.Null(triggeredId);

        // Viewport tool 'W' when workspace is 3D Viewport -> MUST trigger!
        bool handledViewport = _service.ProcessKeyDown(Key.W, ModifierKeys.None, isTextInputFocused: false, ShortcutScope.ViewportOnly);
        Assert.True(handledViewport);
        Assert.Equal("viewport.tool.move", triggeredId);
    }

    [Fact]
    public void CheckConflict_DetectsDuplicateKeysInSameScope()
    {
        // 'Ctrl + O' is bound to 'file.open'
        var conflict = _service.CheckConflict("file.new", Key.O, ModifierKeys.Control, ShortcutScope.Global);
        Assert.True(conflict.HasConflict);
        Assert.NotNull(conflict.ConflictingShortcut);
        Assert.Equal("file.open", conflict.ConflictingShortcut.Id);

        // 'Ctrl + K' is not bound to any shortcut
        var noConflict = _service.CheckConflict("file.new", Key.K, ModifierKeys.Control, ShortcutScope.Global);
        Assert.False(noConflict.HasConflict);
        Assert.Null(noConflict.ConflictingShortcut);
    }

    [Fact]
    public void Customization_Update_ResetToDefault()
    {
        var sc = _service.Shortcuts.First(s => s.Id == "file.open");
        Assert.False(sc.IsCustomized);

        // Remap to Ctrl + K
        _service.UpdateShortcut("file.open", Key.K, ModifierKeys.Control);
        Assert.True(sc.IsCustomized);
        Assert.Equal("Ctrl + K", sc.FormattedGesture);

        // Reset
        _service.ResetShortcut("file.open");
        Assert.False(sc.IsCustomized);
        Assert.Equal("Ctrl + O", sc.FormattedGesture);
    }

    [Fact]
    public void Serialization_ExportAndImportSettings()
    {
        _service.UpdateShortcut("file.open", Key.K, ModifierKeys.Control);
        string json = _service.ExportSettingsJson();
        Assert.Contains("file.open", json);

        // Reset all
        _service.ResetAllToDefaults();
        var sc = _service.Shortcuts.First(s => s.Id == "file.open");
        Assert.Equal(Key.O, sc.Key);

        // Re-import
        bool success = _service.ImportSettingsJson(json);
        Assert.True(success);
        Assert.Equal(Key.K, sc.Key);
    }

    [Fact]
    public void ThemeManager_TogglesState()
    {
        ThemeManager.SetTheme(true);
        Assert.True(ThemeManager.IsDarkTheme);

        ThemeManager.SetTheme(false);
        Assert.False(ThemeManager.IsDarkTheme);

        ThemeManager.ToggleTheme();
        Assert.True(ThemeManager.IsDarkTheme);
    }

    private sealed class MockApplicationPathsService : IApplicationPathsService
    {
        private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "nexus3d_test_" + Guid.NewGuid());

        public string AppDataRootDirectory => _tempRoot;
        public string DatabaseFilePath => Path.Combine(_tempRoot, "test.sqlite");
        public string ConfigFilePath => Path.Combine(_tempRoot, "test.json");
        public string LogsDirectory => Path.Combine(_tempRoot, "logs");
        public string CacheDirectory => Path.Combine(_tempRoot, "cache");
        public string PluginsDirectory => Path.Combine(_tempRoot, "plugins");
        public string ProfilesDirectory => Path.Combine(_tempRoot, "profiles");

        public void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(_tempRoot);
        }
    }
}
