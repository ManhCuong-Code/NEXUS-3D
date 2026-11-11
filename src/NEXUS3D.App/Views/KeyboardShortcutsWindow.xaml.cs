using System.Windows;
using NEXUS3D.App.ViewModels;

namespace NEXUS3D.App.Views;

public partial class KeyboardShortcutsWindow : Window
{
    public KeyboardShortcutsWindow(KeyboardShortcutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

