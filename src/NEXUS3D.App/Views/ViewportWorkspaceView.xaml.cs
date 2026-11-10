using System.Windows.Controls;
using System.Windows.Input;
using NEXUS3D.App.ViewModels;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.App.Views;

public partial class ViewportWorkspaceView : UserControl
{
    public ViewportWorkspaceView()
    {
        InitializeComponent();
        Viewport3D.NodeSelected += OnViewportNodeSelected;
        KeyDown += OnWorkspaceKeyDown;
    }

    private void OnViewportNodeSelected(object? sender, string? nodeId)
    {
        if (DataContext is ViewportViewModel vm)
        {
            vm.SelectNode(nodeId);
        }
    }

    private void OnWorkspaceKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ViewportViewModel vm) return;

        // If focus is currently inside a TextBox (e.g. numeric input), ignore single-key shortcuts
        if (Keyboard.FocusedElement is TextBox)
        {
            if (e.Key == Key.Enter)
            {
                vm.Inspector.ApplyNumericTransformCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        // Ctrl combinations
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (e.Key == Key.Z)
            {
                vm.Undo();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Y)
            {
                vm.Redo();
                e.Handled = true;
                return;
            }
        }

        switch (e.Key)
        {
            case Key.Q:
                vm.SetTool(EditorToolType.Select);
                e.Handled = true;
                break;
            case Key.W:
                vm.SetTool(EditorToolType.Translate);
                e.Handled = true;
                break;
            case Key.E:
                vm.SetTool(EditorToolType.Rotate);
                e.Handled = true;
                break;
            case Key.R:
                vm.SetTool(EditorToolType.Scale);
                e.Handled = true;
                break;
            case Key.T:
                vm.SetTool(EditorToolType.Pivot);
                e.Handled = true;
                break;
            case Key.F:
                Viewport3D.FocusScene();
                e.Handled = true;
                break;
            case Key.Escape:
                vm.SelectNode(null);
                e.Handled = true;
                break;
        }
    }
}
