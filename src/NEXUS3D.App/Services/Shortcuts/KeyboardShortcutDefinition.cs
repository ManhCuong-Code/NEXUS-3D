using System.Text.Json.Serialization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NEXUS3D.App.Services.Shortcuts;

/// <summary>
/// Represents a customizable keyboard shortcut binding with categorization, scopes, and keycap tokens.
/// </summary>
public sealed partial class KeyboardShortcutDefinition : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ShortcutCategory Category { get; init; } = ShortcutCategory.General;
    public ShortcutScope Scope { get; init; } = ShortcutScope.Global;

    public Key DefaultKey { get; init; }
    public ModifierKeys DefaultModifiers { get; init; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedGesture))]
    [NotifyPropertyChangedFor(nameof(KeycapTokens))]
    [NotifyPropertyChangedFor(nameof(IsCustomized))]
    private Key _key;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedGesture))]
    [NotifyPropertyChangedFor(nameof(KeycapTokens))]
    [NotifyPropertyChangedFor(nameof(IsCustomized))]
    private ModifierKeys _modifiers;

    [ObservableProperty]
    private bool _isEnabled = true;

    [JsonIgnore]
    public bool IsCustomized => Key != DefaultKey || Modifiers != DefaultModifiers;

    [JsonIgnore]
    public string FormattedGesture => FormatGesture(Key, Modifiers);

    [JsonIgnore]
    public IReadOnlyList<string> KeycapTokens => GenerateKeycapTokens(Key, Modifiers);

    public KeyboardShortcutDefinition() { }

    public KeyboardShortcutDefinition(
        string id,
        string name,
        string description,
        ShortcutCategory category,
        ShortcutScope scope,
        Key defaultKey,
        ModifierKeys defaultModifiers = ModifierKeys.None)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        Scope = scope;
        DefaultKey = defaultKey;
        DefaultModifiers = defaultModifiers;
        Key = defaultKey;
        Modifiers = defaultModifiers;
    }

    public void ResetToDefault()
    {
        Key = DefaultKey;
        Modifiers = DefaultModifiers;
        IsEnabled = true;
    }

    public static string FormatGesture(Key key, ModifierKeys modifiers)
    {
        if (key == Key.None) return "None";

        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        parts.Add(FormatKey(key));
        return string.Join(" + ", parts);
    }

    public static IReadOnlyList<string> GenerateKeycapTokens(Key key, ModifierKeys modifiers)
    {
        if (key == Key.None) return Array.Empty<string>();

        var list = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) list.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) list.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) list.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) list.Add("Win");

        list.Add(FormatKey(key));
        return list;
    }

    public static string FormatKey(Key key) => key switch
    {
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemMinus => "-",
        Key.OemPlus => "+",
        Key.OemQuestion => "?",
        Key.Escape => "Esc",
        Key.Delete => "Del",
        Key.Back => "Backspace",
        Key.D0 => "0",
        Key.D1 => "1",
        Key.D2 => "2",
        Key.D3 => "3",
        Key.D4 => "4",
        Key.D5 => "5",
        Key.D6 => "6",
        Key.D7 => "7",
        Key.D8 => "8",
        Key.D9 => "9",
        _ => key.ToString()
    };
}

