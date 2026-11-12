using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NEXUS3D.App.Controls;
using NEXUS3D.App.ViewModels;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;

namespace NEXUS3D.App.Views;

public partial class ViewportWorkspaceView : UserControl
{
    public ViewportWorkspaceView()
    {
        InitializeComponent();
        Viewport3D.NodeSelected += OnViewportNodeSelected;
        Viewport3D.BlockPicked += OnViewportBlockPicked;
        KeyDown += OnWorkspaceKeyDown;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ViewportViewModel oldVm)
        {
            oldVm.BedrockWorkspace.CubeMeshUpdated -= OnBedrockCubeMeshUpdated;
            oldVm.BedrockWorkspace.ViewportRefresher = null;
        }

        if (e.NewValue is ViewportViewModel newVm)
        {
            newVm.BedrockWorkspace.CubeMeshUpdated += OnBedrockCubeMeshUpdated;
            newVm.BedrockWorkspace.ViewportRefresher = OnBedrockViewportRefresh;
        }
    }

    private void OnBedrockCubeMeshUpdated(object? sender, BedrockCubeItemViewModel? cube)
    {
        if (DataContext is not ViewportViewModel vm) return;

        // If there is an active RenderScene and the cube has a NodeId, sync the RenderNode's mesh
        if (cube?.NodeId.HasValue == true && vm.Scene is not null && vm.RawUsmScene is not null)
        {
            string nodeIdStr = cube.NodeId.Value.ToString();
            var rNode = FindRenderNode(vm.Scene.RootNodes, nodeIdStr);
            var usmNode = vm.RawUsmScene.Graph.FindNode(cube.NodeId.Value);

            if (rNode is not null && usmNode?.Object?.Meshes.Count > 0)
            {
                rNode.Meshes.Clear();
                var builder = new MeshRenderBuilder();
                foreach (var mesh in usmNode.Object.Meshes)
                {
                    var rMeshes = builder.BuildMulti(mesh, "default");
                    rNode.Meshes.AddRange(rMeshes);
                }
            }
        }

        Viewport3D.RebuildSceneVisuals();
    }

    private void OnBedrockViewportRefresh()
    {
        Viewport3D.RebuildSceneVisuals();
    }

    private static RenderNode? FindRenderNode(IEnumerable<RenderNode> nodes, string id)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Id, id, StringComparison.OrdinalIgnoreCase)) return node;
            var found = FindRenderNode(node.Children, id);
            if (found is not null) return found;
        }
        return null;
    }

    private void OnViewportBlockPicked(object? sender, BlockPickEventArgs? e)
    {
        if (DataContext is ViewportViewModel vm && e is not null)
        {
            vm.BlockWorkspace.SelectVoxel(new NEXUS3D.Domain.Voxels.VoxelPosition(e.X, e.Y, e.Z), e.BlockId, e.BlockState);
        }
    }

    private void OnViewportNodeSelected(object? sender, string? nodeId)
    {
        if (DataContext is ViewportViewModel vm)
        {
            vm.SelectNode(nodeId);
        }
    }

    private void OnBedrockOutlinerSelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not ViewportViewModel vm) return;

        if (e.NewValue is BedrockCubeItemViewModel cube)
        {
            vm.BedrockWorkspace.SelectCube(cube);
            string? targetId = cube.NodeId?.ToString() ?? cube.CubeId;
            if (!string.IsNullOrEmpty(targetId))
            {
                vm.SelectNode(targetId);
            }
        }
        else if (e.NewValue is BedrockBoneNodeViewModel bone)
        {
            vm.BedrockWorkspace.SelectBone(bone);
            string? targetId = bone.NodeId?.ToString() ?? bone.Name;
            if (!string.IsNullOrEmpty(targetId))
            {
                vm.SelectNode(targetId);
            }
        }
    }

    private void OnWorkspaceKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ViewportViewModel vm) return;

        // If focus is currently inside a TextBox (e.g. numeric input), update binding and apply
        if (Keyboard.FocusedElement is TextBox tb)
        {
            if (e.Key == Key.Enter)
            {
                var binding = tb.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();
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
            if (e.Key == Key.O)
            {
                _ = vm.OpenNewTabAsync();
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
                Viewport3D.FocusSelected();
                vm.FocusSelected();
                e.Handled = true;
                break;
            case Key.Home:
                Viewport3D.FocusScene();
                vm.FocusModel();
                e.Handled = true;
                break;
            case Key.D0:
            case Key.NumPad0:
                Viewport3D.ResetCamera();
                vm.Camera.ResetCameraCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D1:
            case Key.NumPad1:
                vm.Camera.SetPreset(CameraPresetView.Front);
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                vm.Camera.SetPreset(CameraPresetView.Right);
                e.Handled = true;
                break;
            case Key.D7:
            case Key.NumPad7:
                vm.Camera.SetPreset(CameraPresetView.Top);
                e.Handled = true;
                break;
            case Key.D5:
            case Key.NumPad5:
                vm.Camera.ToggleProjectionCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.SelectNode(null);
                e.Handled = true;
                break;
        }
    }
}
