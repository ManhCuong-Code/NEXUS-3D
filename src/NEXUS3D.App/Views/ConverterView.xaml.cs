using System.Windows.Controls;
using NEXUS3D.App.ViewModels;

namespace NEXUS3D.App.Views;

public partial class ConverterView : UserControl
{
    public ConverterView()
    {
        InitializeComponent();
    }

    private void OnBatchFilesDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files && DataContext is ConverterViewModel vm)
            {
                vm.AddBatchFilesFromPaths(files);
            }
        }
    }

    private void OnBatchFilesDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }
}

