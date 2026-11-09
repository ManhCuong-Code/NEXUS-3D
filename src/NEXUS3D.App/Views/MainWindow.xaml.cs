using System.Windows;

namespace NEXUS3D.App.Views;

/// <summary>
/// Main shell window for NEXUS 3D.
/// Contains zero business logic; all interactions are bound to <see cref="ViewModels.MainWindowViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}

