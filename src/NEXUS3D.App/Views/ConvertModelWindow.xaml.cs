using System.Windows;
using NEXUS3D.App.ViewModels;

namespace NEXUS3D.App.Views;

public partial class ConvertModelWindow : Window
{
    public ConvertModelWindow(ConvertModelDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (s, e) =>
        {
            Close();
        };
    }
}
