using System.Windows;

namespace NEXUS3D.App.Themes;

/// <summary>
/// Manages application-wide theme switching (Dark Theme vs Light Theme).
/// </summary>
public static class ThemeManager
{
    private static bool _isDarkTheme = true;

    public static bool IsDarkTheme => _isDarkTheme;

    public static event EventHandler<bool>? ThemeChanged;

    public static void SetTheme(bool isDark)
    {
        _isDarkTheme = isDark;
        string themeUri = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";

        var app = System.Windows.Application.Current;
        if (app is null) return;

        var merged = app.Resources.MergedDictionaries;
        ResourceDictionary? existingTheme = null;

        foreach (var dict in merged)
        {
            if (dict.Source is not null && dict.Source.OriginalString.Contains("Theme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                existingTheme = dict;
                break;
            }
        }

        var newTheme = new ResourceDictionary
        {
            Source = new Uri(themeUri, UriKind.RelativeOrAbsolute)
        };

        if (existingTheme is not null)
        {
            int index = merged.IndexOf(existingTheme);
            merged[index] = newTheme;
        }
        else
        {
            merged.Insert(0, newTheme);
        }

        ThemeChanged?.Invoke(null, isDark);
    }

    public static void ToggleTheme()
    {
        SetTheme(!_isDarkTheme);
    }
}

