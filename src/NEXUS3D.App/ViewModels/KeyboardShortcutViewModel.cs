using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.App.Services.Shortcuts;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel coordinating keyboard shortcuts search, filtering, keycap display, and remapping.
/// </summary>
public sealed partial class KeyboardShortcutViewModel : BaseViewModel
{
    private readonly IKeyboardShortcutService _shortcutService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedCategoryName = "All Categories";

    [ObservableProperty]
    private KeyboardShortcutDefinition? _selectedShortcut;

    [ObservableProperty]
    private string _statusFeedback = string.Empty;

    public ObservableCollection<KeyboardShortcutDefinition> FilteredShortcuts { get; } = [];
    public ObservableCollection<string> Categories { get; } = [];

    public KeyboardShortcutViewModel(IKeyboardShortcutService shortcutService)
    {
        _shortcutService = Guard.NotNull(shortcutService);

        Title = "Keyboard Shortcuts Cheatsheet & Customizer";
        Subtitle = "View and customize system, navigation, and 3D viewport shortcuts.";

        Categories.Add("All Categories");
        foreach (var cat in Enum.GetNames<ShortcutCategory>())
        {
            Categories.Add(cat);
        }

        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryNameChanged(string value) => ApplyFilter();

    public void ApplyFilter()
    {
        FilteredShortcuts.Clear();

        var query = _shortcutService.Shortcuts.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string term = SearchText.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.Name.ToLowerInvariant().Contains(term) ||
                s.Description.ToLowerInvariant().Contains(term) ||
                s.FormattedGesture.ToLowerInvariant().Contains(term));
        }

        if (SelectedCategoryName != "All Categories" && Enum.TryParse<ShortcutCategory>(SelectedCategoryName, out var cat))
        {
            query = query.Where(s => s.Category == cat);
        }

        foreach (var item in query)
        {
            FilteredShortcuts.Add(item);
        }

        StatusFeedback = $"{FilteredShortcuts.Count} shortcut(s) matching criteria.";
    }

    [RelayCommand]
    private void ResetAllDefaults()
    {
        _shortcutService.ResetAllToDefaults();
        ApplyFilter();
        StatusFeedback = "All shortcuts restored to factory defaults.";
    }

    [RelayCommand]
    private void ResetSelected()
    {
        if (SelectedShortcut is null) return;
        _shortcutService.ResetShortcut(SelectedShortcut.Id);
        ApplyFilter();
        StatusFeedback = $"Reset '{SelectedShortcut.Name}' to default.";
    }

    [RelayCommand]
    private void ToggleEnabled()
    {
        if (SelectedShortcut is null) return;
        SelectedShortcut.IsEnabled = !SelectedShortcut.IsEnabled;
        StatusFeedback = $"Shortcut '{SelectedShortcut.Name}' is now {(SelectedShortcut.IsEnabled ? "Enabled" : "Disabled")}.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _shortcutService.SaveSettingsAsync();
        StatusFeedback = "Keyboard shortcut settings saved successfully.";
    }
}

