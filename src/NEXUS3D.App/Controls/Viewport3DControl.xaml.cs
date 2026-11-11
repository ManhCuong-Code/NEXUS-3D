using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using NEXUS3D.App.Rendering;
using NEXUS3D.App.Rendering.Gizmos;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Commands;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.Controls;

public partial class Viewport3DControl : UserControl
{
    private readonly HelixSceneRenderer _sceneRenderer = new();
    private readonly TransformGizmoVisual3D _gizmoVisual = new();
    private readonly BoundingBoxWireFrameVisual3D _blockHighlightVisual = new() { Color = Colors.LimeGreen, Thickness = 2.0 };
    private ModelVisual3D? _currentSceneVisual;
    private LinesVisual3D? _currentLinesVisual;
    private BoundingBoxWireFrameVisual3D? _currentBoundsVisual;

    private Point _mouseDownPos;
    private bool _isDraggingGizmo;
    private GizmoHandle _activeHandle = GizmoHandle.None;
    private SceneNode? _draggedNode;
    private NEXUS3D.Domain.Mathematics.Transform3D? _initialNodeTransform;

    public static readonly DependencyProperty SceneProperty =
        DependencyProperty.Register(
            nameof(Scene),
            typeof(RenderScene),
            typeof(Viewport3DControl),
            new PropertyMetadata(null, OnSceneChanged));

    public static readonly DependencyProperty RawUsmSceneProperty =
        DependencyProperty.Register(
            nameof(RawUsmScene),
            typeof(Scene3D),
            typeof(Viewport3DControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SettingsProperty =
        DependencyProperty.Register(
            nameof(Settings),
            typeof(RenderSettings),
            typeof(Viewport3DControl),
            new PropertyMetadata(RenderSettings.Default, OnSettingsChanged));

    public static readonly DependencyProperty CameraStateProperty =
        DependencyProperty.Register(
            nameof(CameraState),
            typeof(CameraState),
            typeof(Viewport3DControl),
            new PropertyMetadata(CameraState.Default, OnCameraStateChanged));

    public static readonly DependencyProperty ActiveToolProperty =
        DependencyProperty.Register(
            nameof(ActiveTool),
            typeof(EditorToolType),
            typeof(Viewport3DControl),
            new PropertyMetadata(EditorToolType.Select, OnEditorToolChanged));

    public static readonly DependencyProperty TransformSpaceProperty =
        DependencyProperty.Register(
            nameof(TransformSpace),
            typeof(TransformSpace),
            typeof(Viewport3DControl),
            new PropertyMetadata(TransformSpace.World, OnTransformSpaceChanged));

    public static readonly DependencyProperty SelectedNodeIdProperty =
        DependencyProperty.Register(
            nameof(SelectedNodeId),
            typeof(string),
            typeof(Viewport3DControl),
            new PropertyMetadata(null, OnSelectedNodeIdChanged));

    public static readonly DependencyProperty HighlightedBlockIdProperty =
        DependencyProperty.Register(
            nameof(HighlightedBlockId),
            typeof(string),
            typeof(Viewport3DControl),
            new PropertyMetadata(null, (d, e) => ((Viewport3DControl)d).UpdateBlockHighlight()));

    public static readonly DependencyProperty TransformServiceProperty =
        DependencyProperty.Register(
            nameof(TransformService),
            typeof(ITransformService),
            typeof(Viewport3DControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty UndoRedoServiceProperty =
        DependencyProperty.Register(
            nameof(UndoRedoService),
            typeof(IUndoRedoService),
            typeof(Viewport3DControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty PivotServiceProperty =
        DependencyProperty.Register(
            nameof(PivotService),
            typeof(IPivotService),
            typeof(Viewport3DControl),
            new PropertyMetadata(null));

    public event EventHandler<string?>? NodeSelected;

    public RenderScene? Scene
    {
        get => (RenderScene?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public Scene3D? RawUsmScene
    {
        get => (Scene3D?)GetValue(RawUsmSceneProperty);
        set => SetValue(RawUsmSceneProperty, value);
    }

    public RenderSettings Settings
    {
        get => (RenderSettings)GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    public CameraState CameraState
    {
        get => (CameraState)GetValue(CameraStateProperty);
        set => SetValue(CameraStateProperty, value);
    }

    public EditorToolType ActiveTool
    {
        get => (EditorToolType)GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    public TransformSpace TransformSpace
    {
        get => (TransformSpace)GetValue(TransformSpaceProperty);
        set => SetValue(TransformSpaceProperty, value);
    }

    public string? SelectedNodeId
    {
        get => (string?)GetValue(SelectedNodeIdProperty);
        set => SetValue(SelectedNodeIdProperty, value);
    }

    public string? HighlightedBlockId
    {
        get => (string?)GetValue(HighlightedBlockIdProperty);
        set => SetValue(HighlightedBlockIdProperty, value);
    }

    public ITransformService? TransformService
    {
        get => (ITransformService?)GetValue(TransformServiceProperty);
        set => SetValue(TransformServiceProperty, value);
    }

    public IUndoRedoService? UndoRedoService
    {
        get => (IUndoRedoService?)GetValue(UndoRedoServiceProperty);
        set => SetValue(UndoRedoServiceProperty, value);
    }

    public IPivotService? PivotService
    {
        get => (IPivotService?)GetValue(PivotServiceProperty);
        set => SetValue(PivotServiceProperty, value);
    }

    public Viewport3DControl()
    {
        InitializeComponent();
        EmptyStateOverlay.Visibility = Visibility.Visible;

        HelixViewport.Children.Add(_gizmoVisual);

        HelixViewport.MouseDown += OnViewportMouseDown;
        HelixViewport.MouseMove += OnViewportMouseMove;
        HelixViewport.MouseUp += OnViewportMouseUp;

        Unloaded += (s, e) =>
        {
            _sceneRenderer.Dispose();
        };
    }

    private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Viewport3DControl control)
        {
            control.RebuildSceneVisuals();
            control.UpdateGizmoDisplay();
        }
    }

    private static void OnSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Viewport3DControl control)
        {
            control.ApplySettings();
        }
    }

    private static void OnCameraStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Viewport3DControl control && e.NewValue is CameraState cam)
        {
            control.ApplyCamera(cam);
        }
    }

    private static void OnEditorToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Viewport3DControl control)
        {
            control.UpdateGizmoDisplay();
        }
    }

    private static void OnTransformSpaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Viewport3DControl control)
        {
            control.UpdateGizmoDisplay();
        }
    }

    private static void OnSelectedNodeIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Viewport3DControl control)
        {
            control.UpdateGizmoDisplay();
        }
    }

    public void RebuildSceneVisuals()
    {
        // Remove previous visuals
        if (_currentSceneVisual is not null)
        {
            HelixViewport.Children.Remove(_currentSceneVisual);
            _currentSceneVisual = null;
        }

        if (_currentLinesVisual is not null)
        {
            HelixViewport.Children.Remove(_currentLinesVisual);
            _currentLinesVisual = null;
        }

        if (_currentBoundsVisual is not null)
        {
            HelixViewport.Children.Remove(_currentBoundsVisual);
            _currentBoundsVisual = null;
        }

        if (Scene is null || (Scene.RootNodes.Count == 0 && Scene.Skeletons.Count == 0))
        {
            _sceneRenderer.ClearCache();
            EmptyStateOverlay.Visibility = Visibility.Visible;
            TxtStats.Text = "Objects: 0 | Triangles: 0 | Vertices: 0 | Voxels: 0";
            _gizmoVisual.Hide();
            return;
        }

        EmptyStateOverlay.Visibility = Visibility.Collapsed;
        TxtStats.Text = $"Nodes: {Scene.RootNodes.Count} | Triangles: {Scene.TotalTriangles:N0} | Vertices: {Scene.TotalVertices:N0} | Voxels: {Scene.TotalVoxels:N0}";

        _currentSceneVisual = _sceneRenderer.BuildSceneVisual(Scene, Settings, out _currentLinesVisual, out _currentBoundsVisual);
        HelixViewport.Children.Add(_currentSceneVisual);

        if (_currentLinesVisual.Points?.Count > 0)
        {
            HelixViewport.Children.Add(_currentLinesVisual);
        }

        if (Settings.ShowBoundingBox)
        {
            HelixViewport.Children.Add(_currentBoundsVisual);
        }

        ApplySettings();
        FocusScene();
        UpdateGizmoDisplay();
    }

    public void ApplySettings()
    {
        var settings = Settings ?? RenderSettings.Default;

        GridVisual.Visible = settings.ShowGrid;
        HelixViewport.ShowCoordinateSystem = settings.ShowAxes;

        if (ColorConverter.ConvertFromString(settings.BackgroundColorHex) is Color bgCol)
        {
            HelixViewport.Background = new SolidColorBrush(bgCol);
        }

        if (Scene is not null)
        {
            if (_currentLinesVisual is not null)
            {
                bool shouldShowLines = settings.Shading is ShadingMode.Wireframe or ShadingMode.SolidWireframe || settings.ShowWireframe || settings.ShowNormals || settings.ShowBones;
                if (shouldShowLines && !HelixViewport.Children.Contains(_currentLinesVisual) && _currentLinesVisual.Points?.Count > 0)
                {
                    HelixViewport.Children.Add(_currentLinesVisual);
                }
                else if (!shouldShowLines && HelixViewport.Children.Contains(_currentLinesVisual))
                {
                    HelixViewport.Children.Remove(_currentLinesVisual);
                }
            }

            if (_currentBoundsVisual is not null)
            {
                if (settings.ShowBoundingBox && !HelixViewport.Children.Contains(_currentBoundsVisual))
                {
                    HelixViewport.Children.Add(_currentBoundsVisual);
                }
                else if (!settings.ShowBoundingBox && HelixViewport.Children.Contains(_currentBoundsVisual))
                {
                    HelixViewport.Children.Remove(_currentBoundsVisual);
                }
            }
        }
    }

    public void FocusScene()
    {
        if (Scene is null)
        {
            GridVisual.Normal = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
            GridVisual.LengthDirection = new System.Windows.Media.Media3D.Vector3D(1, 0, 0);
            GridVisual.Center = new Point3D(0, 0, 0);
            return;
        }

        var min = Scene.BoundsMin;
        var max = Scene.BoundsMax;
        var size = max - min;
        float maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));

        if (maxDim > 0.001f && !float.IsInfinity(maxDim) && !float.IsNaN(maxDim))
        {
            var center = (min + max) * 0.5f;
            double distance = Math.Max(maxDim * 1.8, 4.0);

            var camPos = new Point3D(center.X + distance * 0.8, center.Y + distance * 0.7, center.Z + distance * 1.1);
            var lookDir = new System.Windows.Media.Media3D.Vector3D(center.X - camPos.X, center.Y - camPos.Y, center.Z - camPos.Z);

            if (HelixViewport.Camera is PerspectiveCamera pCam)
            {
                pCam.Position = camPos;
                pCam.LookDirection = lookDir;
                pCam.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
                pCam.NearPlaneDistance = Math.Max(0.01, distance * 0.001);
                pCam.FarPlaneDistance = Math.Max(2000.0, distance * 100.0);
            }
            else if (HelixViewport.Camera is OrthographicCamera oCam)
            {
                oCam.Position = camPos;
                oCam.LookDirection = lookDir;
                oCam.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
                oCam.Width = maxDim * 2.0;
                oCam.NearPlaneDistance = Math.Max(0.01, distance * 0.001);
                oCam.FarPlaneDistance = Math.Max(2000.0, distance * 100.0);
            }

            double gridDim = Math.Max(80.0, Math.Ceiling(maxDim * 2.5 / 10.0) * 10.0);
            GridVisual.Width = gridDim;
            GridVisual.Length = gridDim;
            GridVisual.MajorDistance = Math.Max(10.0, Math.Pow(10, Math.Floor(Math.Log10(gridDim))));
            GridVisual.MinorDistance = Math.Max(1.0, GridVisual.MajorDistance / 10.0);

            // Align floor grid strictly to horizontal XZ plane right under model base (min.Y)
            GridVisual.Normal = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
            GridVisual.LengthDirection = new System.Windows.Media.Media3D.Vector3D(1, 0, 0);
            GridVisual.Center = new Point3D(center.X, min.Y - 0.005, center.Z);
        }
        else
        {
            GridVisual.Normal = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
            GridVisual.LengthDirection = new System.Windows.Media.Media3D.Vector3D(1, 0, 0);
            GridVisual.Center = new Point3D(0, 0, 0);
        }

        try
        {
            HelixViewport.ZoomExtents();
        }
        catch { }
    }

    public void ApplyCamera(CameraState cam)
    {
        if (cam.ProjectionType == CameraProjectionType.Orthographic)
        {
            var ortho = new OrthographicCamera
            {
                Position = new Point3D(cam.Position.X, cam.Position.Y, cam.Position.Z),
                LookDirection = new System.Windows.Media.Media3D.Vector3D(cam.LookDirection.X, cam.LookDirection.Y, cam.LookDirection.Z),
                UpDirection = new System.Windows.Media.Media3D.Vector3D(cam.Up.X, cam.Up.Y, cam.Up.Z),
                Width = cam.OrthographicWidth,
                NearPlaneDistance = cam.NearPlane,
                FarPlaneDistance = cam.FarPlane
            };
            HelixViewport.Camera = ortho;
        }
        else
        {
            var persp = new PerspectiveCamera
            {
                Position = new Point3D(cam.Position.X, cam.Position.Y, cam.Position.Z),
                LookDirection = new System.Windows.Media.Media3D.Vector3D(cam.LookDirection.X, cam.LookDirection.Y, cam.LookDirection.Z),
                UpDirection = new System.Windows.Media.Media3D.Vector3D(cam.Up.X, cam.Up.Y, cam.Up.Z),
                FieldOfView = cam.FieldOfView,
                NearPlaneDistance = cam.NearPlane,
                FarPlaneDistance = cam.FarPlane
            };
            HelixViewport.Camera = persp;
        }
    }

    private void UpdateGizmoDisplay()
    {
        var targetNodeId = SelectedNodeId;
        if (string.IsNullOrEmpty(targetNodeId))
        {
            if (RawUsmScene?.Graph.Root.Children.FirstOrDefault() is { } firstUsmNode)
            {
                targetNodeId = firstUsmNode.Id.ToString();
            }
            else if (Scene?.RootNodes.Count > 0)
            {
                targetNodeId = Scene.RootNodes[0].Id;
            }
        }

        if (string.IsNullOrEmpty(targetNodeId) || ActiveTool == EditorToolType.None)
        {
            _gizmoVisual.Hide();
            return;
        }

        var effectiveTool = ActiveTool == EditorToolType.Select ? EditorToolType.Translate : ActiveTool;

        Point3D gizmoPos = new(0, 0, 0);
        System.Windows.Media.Media3D.Quaternion gizmoRot = System.Windows.Media.Media3D.Quaternion.Identity;

        if (RawUsmScene is not null && Guid.TryParse(targetNodeId, out var guid))
        {
            var node = RawUsmScene.Graph.FindNode(guid);
            if (node is not null)
            {
                var worldPos = node.GetWorldMatrix().Translation;
                gizmoPos = new Point3D(worldPos.X, worldPos.Y, worldPos.Z);

                if (TransformSpace == TransformSpace.Local)
                {
                    gizmoRot = new System.Windows.Media.Media3D.Quaternion(
                        node.LocalTransform.Rotation.X,
                        node.LocalTransform.Rotation.Y,
                        node.LocalTransform.Rotation.Z,
                        node.LocalTransform.Rotation.W);
                }
            }
        }
        else if (Scene is not null)
        {
            var rNode = FindNodeMatchingId(Scene.RootNodes, targetNodeId);
            if (rNode is not null)
            {
                var center = (rNode.BoundsMin + rNode.BoundsMax) * 0.5f;
                gizmoPos = new Point3D(center.X, center.Y, center.Z);
            }
        }

        double camDist = 10.0;
        if (HelixViewport.Camera is ProjectionCamera pCam)
        {
            camDist = (pCam.Position - gizmoPos).Length;
        }

        _gizmoVisual.UpdateGizmo(effectiveTool, gizmoPos, gizmoRot, camDist);
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPos = e.GetPosition(HelixViewport);

        if (e.ChangedButton == MouseButton.Left && _gizmoVisual.CurrentTool != EditorToolType.None)
        {
            // Hit test against gizmo
            var hit = VisualTreeHelper.HitTest(HelixViewport, _mouseDownPos) as RayMeshGeometry3DHitTestResult;
            if (hit?.ModelHit is GeometryModel3D hitModel)
            {
                var handle = _gizmoVisual.HitTestHandle(hitModel);
                if (handle != GizmoHandle.None)
                {
                    _isDraggingGizmo = true;
                    _activeHandle = handle;

                    HelixViewport.CaptureMouse();
                    HelixViewport.IsRotationEnabled = false;
                    HelixViewport.IsPanEnabled = false;

                    var targetId = SelectedNodeId;
                    if (string.IsNullOrEmpty(targetId) && RawUsmScene?.Graph.Root.Children.FirstOrDefault() is { } firstChild)
                    {
                        targetId = firstChild.Id.ToString();
                    }

                    if (RawUsmScene is not null && !string.IsNullOrEmpty(targetId) && Guid.TryParse(targetId, out var guid))
                    {
                        _draggedNode = RawUsmScene.Graph.FindNode(guid);
                        _initialNodeTransform = _draggedNode?.LocalTransform.DeepClone();
                    }

                    e.Handled = true;
                    return;
                }
            }
        }
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingGizmo && _draggedNode is not null && _initialNodeTransform is not null)
        {
            var currentPos = e.GetPosition(HelixViewport);
            double dx = currentPos.X - _mouseDownPos.X;
            double dy = currentPos.Y - _mouseDownPos.Y;

            double camDist = 10.0;
            if (HelixViewport.Camera is ProjectionCamera pCam)
            {
                var nodeWorld = _draggedNode.GetWorldMatrix().Translation;
                camDist = (pCam.Position - new Point3D(nodeWorld.X, nodeWorld.Y, nodeWorld.Z)).Length;
            }

            double speed = Math.Max(0.005, camDist * 0.002);

            var tool = ActiveTool == EditorToolType.Select ? EditorToolType.Translate : ActiveTool;
            switch (tool)
            {
                case EditorToolType.Translate:
                    var delta = _activeHandle switch
                    {
                        GizmoHandle.AxisX => new NEXUS3D.Domain.Mathematics.Vector3D(dx * speed, 0, 0),
                        GizmoHandle.AxisY => new NEXUS3D.Domain.Mathematics.Vector3D(0, -dy * speed, 0),
                        GizmoHandle.AxisZ => new NEXUS3D.Domain.Mathematics.Vector3D(0, 0, dy * speed),
                        GizmoHandle.PlaneXY => new NEXUS3D.Domain.Mathematics.Vector3D(dx * speed, -dy * speed, 0),
                        GizmoHandle.PlaneXZ => new NEXUS3D.Domain.Mathematics.Vector3D(dx * speed, 0, dy * speed),
                        GizmoHandle.PlaneYZ => new NEXUS3D.Domain.Mathematics.Vector3D(0, -dy * speed, dx * speed),
                        _ => new NEXUS3D.Domain.Mathematics.Vector3D(dx * speed, -dy * speed, 0)
                    };
                    _draggedNode.LocalTransform = _initialNodeTransform.DeepClone();
                    TransformService?.Translate([_draggedNode], delta, TransformSpace);
                    break;

                case EditorToolType.Rotate:
                    double angle = dx * 0.5;
                    var rotDelta = _activeHandle switch
                    {
                        GizmoHandle.AxisX => QuaternionD.FromAxisAngle(NEXUS3D.Domain.Mathematics.Vector3D.UnitX, MathUtilities.DegreesToRadians(angle)),
                        GizmoHandle.AxisY => QuaternionD.FromAxisAngle(NEXUS3D.Domain.Mathematics.Vector3D.UnitY, MathUtilities.DegreesToRadians(angle)),
                        GizmoHandle.AxisZ => QuaternionD.FromAxisAngle(NEXUS3D.Domain.Mathematics.Vector3D.UnitZ, MathUtilities.DegreesToRadians(angle)),
                        _ => QuaternionD.FromAxisAngle(NEXUS3D.Domain.Mathematics.Vector3D.UnitY, MathUtilities.DegreesToRadians(angle))
                    };
                    _draggedNode.LocalTransform = _initialNodeTransform.DeepClone();
                    var pivot = _draggedNode.GetWorldMatrix().Translation;
                    TransformService?.Rotate([_draggedNode], rotDelta, pivot, TransformSpace);
                    break;

                case EditorToolType.Scale:
                    double sf = 1.0 - dy * 0.01;
                    var scaleMultiplier = _activeHandle switch
                    {
                        GizmoHandle.AxisX => new NEXUS3D.Domain.Mathematics.Vector3D(sf, 1, 1),
                        GizmoHandle.AxisY => new NEXUS3D.Domain.Mathematics.Vector3D(1, sf, 1),
                        GizmoHandle.AxisZ => new NEXUS3D.Domain.Mathematics.Vector3D(1, 1, sf),
                        _ => new NEXUS3D.Domain.Mathematics.Vector3D(sf, sf, sf)
                    };
                    _draggedNode.LocalTransform = _initialNodeTransform.DeepClone();
                    var scalePivot = _draggedNode.GetWorldMatrix().Translation;
                    TransformService?.Scale([_draggedNode], scaleMultiplier, scalePivot);
                    break;

                case EditorToolType.Pivot:
                    var pivotDelta = new NEXUS3D.Domain.Mathematics.Vector3D(dx * speed, -dy * speed, 0);
                    PivotService?.SetPivot(_draggedNode, pivotDelta, preserveVisualAppearance: true);
                    break;
            }

            // Real-time synchronization: Update visual transform of dragged node
            if (_draggedNode is not null)
            {
                var world = _draggedNode.GetWorldMatrix();
                var m4x4 = new System.Numerics.Matrix4x4(
                    (float)world.M11, (float)world.M12, (float)world.M13, (float)world.M14,
                    (float)world.M21, (float)world.M22, (float)world.M23, (float)world.M24,
                    (float)world.M31, (float)world.M32, (float)world.M33, (float)world.M34,
                    (float)world.M41, (float)world.M42, (float)world.M43, (float)world.M44);

                if (_sceneRenderer.NodeVisualGroups.TryGetValue(_draggedNode.Name, out var group) ||
                    _sceneRenderer.NodeVisualGroups.TryGetValue(_draggedNode.Id.ToString(), out group))
                {
                    group.Transform = HelixSceneRenderer.ToMatrixTransform3D(m4x4);
                }
            }

            UpdateGizmoDisplay();
            e.Handled = true;
        }
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingGizmo)
        {
            _isDraggingGizmo = false;
            HelixViewport.ReleaseMouseCapture();
            HelixViewport.IsRotationEnabled = true;
            HelixViewport.IsPanEnabled = true;

            if (_draggedNode is not null && _initialNodeTransform is not null)
            {
                if (_draggedNode.LocalTransform.Position != _initialNodeTransform.Position ||
                    _draggedNode.LocalTransform.Rotation != _initialNodeTransform.Rotation ||
                    _draggedNode.LocalTransform.Scale != _initialNodeTransform.Scale)
                {
                    var cmd = new TransformCommand(
                        $"{ActiveTool} '{_draggedNode.Name}'",
                        _draggedNode,
                        _initialNodeTransform,
                        _draggedNode.LocalTransform);
                    UndoRedoService?.Execute(cmd);
                }
            }

            _draggedNode = null;
            _initialNodeTransform = null;
            _activeHandle = GizmoHandle.None;
            RebuildSceneVisuals();
            e.Handled = true;
            return;
        }

        var upPos = e.GetPosition(HelixViewport);
        if (Math.Abs(upPos.X - _mouseDownPos.X) < 4 && Math.Abs(upPos.Y - _mouseDownPos.Y) < 4)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                PerformHitTest(upPos);
            }
        }
    }

    private void PerformHitTest(Point clickPoint)
    {
        var hits = VisualTreeHelper.HitTest(HelixViewport, clickPoint) as RayMeshGeometry3DHitTestResult;
        if (hits?.ModelHit is GeometryModel3D hitModel && Scene is not null)
        {
            foreach (var node in Scene.RootNodes)
            {
                var found = FindNodeMatchingModel(node, hitModel);
                if (found is not null)
                {
                    SelectedNodeId = found.Id;
                    NodeSelected?.Invoke(this, found.Id);
                    UpdateGizmoDisplay();
                    return;
                }
            }
        }

        SelectedNodeId = null;
        NodeSelected?.Invoke(this, null);
        UpdateGizmoDisplay();
    }

    private static RenderNode? FindNodeMatchingModel(RenderNode node, GeometryModel3D model)
    {
        if (node.Meshes.Any(m => m.Positions.Length == (model.Geometry as MeshGeometry3D)?.Positions.Count))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var c = FindNodeMatchingModel(child, model);
            if (c is not null) return c;
        }

        return null;
    }

    private static RenderNode? FindNodeMatchingId(IEnumerable<RenderNode> nodes, string id)
    {
        foreach (var n in nodes)
        {
            if (string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase)) return n;
            var c = FindNodeMatchingId(n.Children, id);
            if (c is not null) return c;
        }
        return null;
    }

    private void UpdateBlockHighlight()
    {
        if (string.IsNullOrEmpty(HighlightedBlockId) || RawUsmScene is null)
        {
            if (HelixViewport.Children.Contains(_blockHighlightVisual))
            {
                HelixViewport.Children.Remove(_blockHighlightVisual);
            }
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        bool found = false;

        foreach (var node in RawUsmScene.Graph.Nodes)
        {
            if (node.Object is not { } obj) continue;
            foreach (var grid in obj.VoxelGrids)
            {
                foreach (var voxel in grid.Voxels)
                {
                    string id = voxel.State?.GetFullIdentifier() ?? "minecraft:stone";
                    if (string.Equals(id, HighlightedBlockId, StringComparison.OrdinalIgnoreCase) ||
                        id.Contains(HighlightedBlockId, StringComparison.OrdinalIgnoreCase))
                    {
                        var p = voxel.Position + grid.Origin;
                        minX = Math.Min(minX, p.X);
                        minY = Math.Min(minY, p.Y);
                        minZ = Math.Min(minZ, p.Z);
                        maxX = Math.Max(maxX, p.X + 1);
                        maxY = Math.Max(maxY, p.Y + 1);
                        maxZ = Math.Max(maxZ, p.Z + 1);
                        found = true;
                    }
                }
            }
        }

        if (found)
        {
            _blockHighlightVisual.BoundingBox = new Rect3D(minX, minY, minZ, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY), Math.Max(1, maxZ - minZ));
            if (!HelixViewport.Children.Contains(_blockHighlightVisual))
            {
                HelixViewport.Children.Add(_blockHighlightVisual);
            }
        }
        else if (HelixViewport.Children.Contains(_blockHighlightVisual))
        {
            HelixViewport.Children.Remove(_blockHighlightVisual);
        }
    }
}
