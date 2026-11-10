using System.Windows.Controls;
using NEXUS3D.App.ViewModels;

namespace NEXUS3D.App.Views;

public partial class ViewportWorkspaceView : UserControl
{
    public ViewportWorkspaceView()
    {
        InitializeComponent();
        Viewport3D.NodeSelected += OnViewportNodeSelected;
    }

    private void OnViewportNodeSelected(object? sender, string? nodeId)
    {
        if (DataContext is ViewportViewModel vm)
        {
            vm.SelectNode(nodeId);
        }
    }
}
