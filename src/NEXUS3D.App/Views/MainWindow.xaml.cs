using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NEXUS3D.App.Services.Shortcuts;
using NEXUS3D.App.ViewModels;

namespace NEXUS3D.App.Views;

/// <summary>
/// Main shell window for NEXUS-3D.
/// Listens to global PreviewKeyDown to route global and context-aware shortcuts
/// through <see cref="IKeyboardShortcutService"/> while guarding text input fields.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IKeyboardShortcutService _shortcutService;

    public MainWindow()
        : this(new KeyboardShortcutService(new NEXUS3D.Infrastructure.FileSystem.ApplicationPathsService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<KeyboardShortcutService>.Instance))
    {
    }

    public MainWindow(IKeyboardShortcutService shortcutService)
    {
        InitializeComponent();
        _shortcutService = shortcutService;

        PreviewKeyDown += OnMainWindowPreviewKeyDown;
    }

    private void OnMainWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Detect if a text input control has keyboard focus
        var focused = Keyboard.FocusedElement;
        bool isTextInputFocused = focused is TextBoxBase or PasswordBox;

        // Context scope: ViewportOnly if Viewport is active
        var vm = DataContext as MainWindowViewModel;
        var currentScope = (vm?.IsViewportActive == true)
            ? ShortcutScope.ViewportOnly
            : ShortcutScope.Global;

        // Resolve key (handling SystemKey for Alt key combinations)
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        if (_shortcutService.ProcessKeyDown(key, modifiers, isTextInputFocused, currentScope))
        {
            e.Handled = true;
        }
    }
}
