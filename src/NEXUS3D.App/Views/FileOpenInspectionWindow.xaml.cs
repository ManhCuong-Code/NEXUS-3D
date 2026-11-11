using System.Windows;
using NEXUS3D.App.ViewModels;

namespace NEXUS3D.App.Views;

public partial class FileOpenInspectionWindow : Window
{
    public FileOpenInspectionWindow(FileOpenInspectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += (s, confirmed) =>
        {
            DialogResult = confirmed;
            Close();
        };
    }
}
