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
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Commands;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;
using NEXUS3D.Rendering.Services;

namespace NEXUS3D.App.Controls;

public sealed record BlockPickEventArgs(int X, int Y, int Z, string? BlockId, string? BlockState);

public partial class Viewport3DControl : UserControl
{

    private readonly HelixSceneRenderer _sceneRenderer = new();
    private readonly TransformGizmoVisual3D _gizmoVisual = new();
    private readonly BoundingBoxWireFrameVisual3D _selectedCubeHighlightVisual = new() { Color = Colors.Gold, Thickness = 2.0 };
    private readonly BoundingBoxWireFrameVisual3D _selectedBlockHighlightVisual = new() { Color = Colors.LimeGreen, Thickness = 2.5 };
    private readonly BoundingBoxWireFrameVisual3D _paletteHighlightVisual = new() { Color = Colors.Cyan, Thickness = 1.5 };
    private ModelVisual3D? _currentSceneVisual;
    private LinesVisual3D? _currentLinesVisual;
    private BoundingBoxWireFrameVisual3D? _currentBoundsVisual;

    private Point _mouseDownPos;
    private Point _lastMousePos;
    private bool _isDraggingGizmo;
    private GizmoHandle _activeHandle = GizmoHandle.None;
    private SceneNode? _draggedNode;
    private NEXUS3D.Domain.Mathematics.Transform3D? _initialNodeTransform;
    private bool _isPotentialEmptySpaceOrbit;
    private bool _isOrbitingEmptySpace;
    private (int X, int Y, int Z)? _selectedBlockCoord;

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

    public static readonly DependencyProperty HighlightedBlockCoordProperty =
        DependencyProperty.Register(
            nameof(HighlightedBlockCoord),
            typeof(VoxelPosition?),
            typeof(Viewport3DControl),
            new PropertyMetadata(null, (d, e) => ((Viewport3DControl)d).OnHighlightedBlockCoordChanged((VoxelPosition?)e.NewValue)));

    public static readonly DependencyProperty TransformServiceProperty =
        DependencyProperty.Register(
            nameof(TransformService),
            typeof(ITransformService),
            typeof(Viewport3DControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectionServiceProperty =
        DependencyProperty.Register(
            nameof(SelectionService),
            typeof(ISelectionService),
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
    public event EventHandler<BlockPickEventArgs?>? BlockPicked;

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

    public VoxelPosition? HighlightedBlockCoord
    {
        get => (VoxelPosition?)GetValue(HighlightedBlockCoordProperty);
        set => SetValue(HighlightedBlockCoordProperty, value);
    }

    public ITransformService? TransformService
    {
        get => (ITransformService?)GetValue(TransformServiceProperty);
        set => SetValue(TransformServiceProperty, value);
    }

    public ISelectionService? SelectionService
    {
        get => (ISelectionService?)GetValue(SelectionServiceProperty);
        set => SetValue(SelectionServiceProperty, value);
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
            control.UpdateCubeHighlightFromSelectedNode();
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

        ClearCubeHighlight();
        ClearBlockHighlight();
        _selectedBlockCoord = null;

        if (Scene is null || (Scene.RootNodes.Count == 0 && Scene.Skeletons.Count == 0))
        {
            _sceneRenderer.ClearCache();
            EmptyStateOverlay.Visibility = Visibility.Visible;
            UpdateTelemetry(null);
            _gizmoVisual.Hide();
            return;
        }

        EmptyStateOverlay.Visibility = Visibility.Collapsed;
        UpdateTelemetry(null);

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
        UpdateCubeHighlightFromSelectedNode();
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
        if (Scene is not null && Scene.BoundsMin != Scene.BoundsMax)
        {
            var min = Scene.BoundsMin;
            var max = Scene.BoundsMax;
            FocusBounds(new Point3D(min.X, min.Y, min.Z), new Point3D(max.X, max.Y, max.Z));
            return;
        }

        if (RawUsmScene?.Graph.Root.GetWorldBounds() is { IsValid: true } b)
        {
            FocusBounds(new Point3D(b.Min.X, b.Min.Y, b.Min.Z), new Point3D(b.Max.X, b.Max.Y, b.Max.Z));
            return;
        }

        GridVisual.Normal = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
        GridVisual.LengthDirection = new System.Windows.Media.Media3D.Vector3D(1, 0, 0);
        GridVisual.Center = new Point3D(0, 0, 0);
    }

    public void FocusSelected()
    {
        // 1. If an individual block is selected
        if (_selectedBlockCoord.HasValue)
        {
            var p = _selectedBlockCoord.Value;
            FocusBounds(new Point3D(p.X, p.Y, p.Z), new Point3D(p.X + 1, p.Y + 1, p.Z + 1));
            return;
        }

        // 2. If a SceneNode (cube or mesh) is selected
        if (!string.IsNullOrEmpty(SelectedNodeId))
        {
            if (Scene is not null)
            {
                var rNode = FindNodeMatchingId(Scene.RootNodes, SelectedNodeId);
                if (rNode is not null && rNode.BoundsMin != rNode.BoundsMax)
                {
                    FocusBounds(new Point3D(rNode.BoundsMin.X, rNode.BoundsMin.Y, rNode.BoundsMin.Z),
                                new Point3D(rNode.BoundsMax.X, rNode.BoundsMax.Y, rNode.BoundsMax.Z));
                    return;
                }
            }

            if (RawUsmScene is not null && Guid.TryParse(SelectedNodeId, out var guid))
            {
                var usmNode = RawUsmScene.Graph.FindNode(guid);
                if (usmNode is not null)
                {
                    if (usmNode.GetWorldBounds() is { IsValid: true } wb)
                    {
                        FocusBounds(new Point3D(wb.Min.X, wb.Min.Y, wb.Min.Z),
                                    new Point3D(wb.Max.X, wb.Max.Y, wb.Max.Z));
                        return;
                    }

                    var world = usmNode.GetWorldMatrix();
                    var center = world.Translation;
                    double r = 1.0;
                    if (usmNode.Object?.Meshes.Count > 0)
                    {
                        var b = usmNode.Object.Meshes[0].Bounds;
                        r = Math.Max(0.5, (b.Max - b.Min).Length() * 0.5);
                    }
                    FocusBounds(new Point3D(center.X - r, center.Y - r, center.Z - r),
                                new Point3D(center.X + r, center.Y + r, center.Z + r));
                    return;
                }
            }
        }

        // 3. Fallback: Focus entire scene
        FocusScene();
    }

    public void FocusBounds(Point3D min, Point3D max)
    {
        var size = max - min;
        double maxDim = Math.Max(size.X, Math.Max(size.Y, size.Z));
        maxDim = Math.Max(maxDim, 0.5);

        var center = new Point3D((min.X + max.X) * 0.5, (min.Y + max.Y) * 0.5, (min.Z + max.Z) * 0.5);
        double distance = Math.Max(maxDim * 2.0, 3.5);

        System.Windows.Media.Media3D.Vector3D dir = new(1.2, 1.0, 1.4);
        if (HelixViewport.Camera is ProjectionCamera currentCam)
        {
            var curOffset = currentCam.Position - (currentCam.Position + currentCam.LookDirection);
            if (curOffset.LengthSquared > 0.001)
            {
                curOffset.Normalize();
                dir = curOffset;
            }
        }
        else
        {
            dir.Normalize();
        }

        var camPos = center + dir * distance;
        var lookDir = center - camPos;

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
            oCam.Width = maxDim * 2.2;
            oCam.NearPlaneDistance = Math.Max(0.01, distance * 0.001);
            oCam.FarPlaneDistance = Math.Max(2000.0, distance * 100.0);
        }

        if (Scene is not null && Scene.BoundsMin != Scene.BoundsMax)
        {
            var sMin = Scene.BoundsMin;
            var sMax = Scene.BoundsMax;
            var sSize = sMax - sMin;
            float sMaxDim = Math.Max(sSize.X, Math.Max(sSize.Y, sSize.Z));
            double gridDim = Math.Max(80.0, Math.Ceiling(sMaxDim * 2.5 / 10.0) * 10.0);
            GridVisual.Width = gridDim;
            GridVisual.Length = gridDim;
            GridVisual.MajorDistance = Math.Max(10.0, Math.Pow(10, Math.Floor(Math.Log10(gridDim))));
            GridVisual.MinorDistance = Math.Max(1.0, GridVisual.MajorDistance / 10.0);
            GridVisual.Normal = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
            GridVisual.LengthDirection = new System.Windows.Media.Media3D.Vector3D(1, 0, 0);
            GridVisual.Center = new Point3D((sMin.X + sMax.X) * 0.5, sMin.Y - 0.005, (sMin.Z + sMax.Z) * 0.5);
        }
    }

    public void ResetCamera()
    {
        var camPos = new Point3D(16, 16, 24);
        var lookDir = new System.Windows.Media.Media3D.Vector3D(-16, -16, -24);
        var upDir = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);

        if (HelixViewport.Camera is PerspectiveCamera pCam)
        {
            pCam.Position = camPos;
            pCam.LookDirection = lookDir;
            pCam.UpDirection = upDir;
            pCam.FieldOfView = 45;
        }
        else if (HelixViewport.Camera is OrthographicCamera oCam)
        {
            oCam.Position = camPos;
            oCam.LookDirection = lookDir;
            oCam.UpDirection = upDir;
            oCam.Width = 30;
        }

        if (Scene is not null)
        {
            FocusScene();
        }
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
                if (node.Object?.GetLocalBounds() is { IsValid: true } localBounds)
                {
                    var worldBounds = localBounds.Transform(node.GetWorldMatrix());
                    gizmoPos = new Point3D(worldBounds.Center.X, worldBounds.Center.Y, worldBounds.Center.Z);
                }
                else
                {
                    var worldPos = node.GetWorldMatrix().Translation;
                    gizmoPos = new Point3D(worldPos.X, worldPos.Y, worldPos.Z);
                }

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
        _lastMousePos = _mouseDownPos;
        _isDraggingGizmo = false;
        _isOrbitingEmptySpace = false;
        _isPotentialEmptySpaceOrbit = false;

        if (e.ChangedButton == MouseButton.Left)
        {
            // 1. Hit test against gizmo handles
            if (_gizmoVisual.CurrentTool != EditorToolType.None)
            {
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

            // 2. Check if clicked on a scene object vs empty space
            var sceneHit = VisualTreeHelper.HitTest(HelixViewport, _mouseDownPos) as RayMeshGeometry3DHitTestResult;
            if (sceneHit?.ModelHit is GeometryModel3D model && _sceneRenderer.ContainsModel(model))
            {
                // Clicked on a scene object
            }
            else
            {
                _isPotentialEmptySpaceOrbit = true;
            }
        }
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        // Gizmo Dragging: Strictly modifies object transform without moving camera
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

            // Real-time visual synchronization
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
            return;
        }

        // Orbit With Left Mouse Drag On Empty Viewport
        if (e.LeftButton == MouseButtonState.Pressed && _isPotentialEmptySpaceOrbit)
        {
            var currentPos = e.GetPosition(HelixViewport);
            if (!_isOrbitingEmptySpace)
            {
                if (Math.Abs(currentPos.X - _mouseDownPos.X) > 3 || Math.Abs(currentPos.Y - _mouseDownPos.Y) > 3)
                {
                    _isOrbitingEmptySpace = true;
                    HelixViewport.CaptureMouse();
                }
            }

            if (_isOrbitingEmptySpace)
            {
                double dx = currentPos.X - _lastMousePos.X;
                double dy = currentPos.Y - _lastMousePos.Y;
                _lastMousePos = currentPos;

                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    HelixViewport.CameraController?.AddPanForce(dx, dy);
                }
                else
                {
                    HelixViewport.CameraController?.AddRotateForce(dx, dy);
                }

                e.Handled = true;
                return;
            }
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

        if (_isOrbitingEmptySpace)
        {
            _isOrbitingEmptySpace = false;
            _isPotentialEmptySpaceOrbit = false;
            HelixViewport.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        _isPotentialEmptySpaceOrbit = false;

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
        bool isCtrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

        var hit = VisualTreeHelper.HitTest(HelixViewport, clickPoint) as RayMeshGeometry3DHitTestResult;
        if (hit?.ModelHit is GeometryModel3D hitModel && Scene is not null)
        {
            var foundNode = _sceneRenderer.GetNodeForModel(hitModel) ?? FindNodeMatchingModel(Scene.RootNodes, hitModel);

            // 1. Schematic / VoxelGrid Hit Test
            bool isVoxelHit = (foundNode != null && foundNode.VoxelChunks.Count > 0) ||
                              (RawUsmScene != null && RawUsmScene.Graph.Nodes.Any(n => n.Object?.VoxelGrids.Count > 0));

            if (isVoxelHit)
            {
                var camPos = HelixViewport.Camera?.Position ?? new Point3D(0, 0, 10);
                var rayDir = hit.PointHit - camPos;
                if (rayDir.LengthSquared > 1e-6) rayDir.Normalize();

                var insideWorld = hit.PointHit + rayDir * 0.005;

                int blockX = (int)Math.Floor(insideWorld.X);
                int blockY = (int)Math.Floor(insideWorld.Y);
                int blockZ = (int)Math.Floor(insideWorld.Z);
                string? blockIdentifier = null;
                string? blockStateStr = null;

                if (RawUsmScene is not null)
                {
                    foreach (var n in RawUsmScene.Graph.Nodes)
                    {
                        if (n.Object is null || n.Object.VoxelGrids.Count == 0) continue;
                        var world = n.GetWorldMatrix();
                        if (!world.TryInvert(out var invWorld)) continue;

                        var localPt = invWorld.TransformPoint(new NEXUS3D.Domain.Mathematics.Vector3D(insideWorld.X, insideWorld.Y, insideWorld.Z));

                        foreach (var grid in n.Object.VoxelGrids)
                        {
                            var gridLocal = localPt - new NEXUS3D.Domain.Mathematics.Vector3D(grid.Origin.X, grid.Origin.Y, grid.Origin.Z);
                            var vx = (int)Math.Floor(gridLocal.X);
                            var vy = (int)Math.Floor(gridLocal.Y);
                            var vz = (int)Math.Floor(gridLocal.Z);

                            var vPos = new VoxelPosition(vx, vy, vz);
                            if (grid.TryGetVoxel(vPos, out var vox) && vox is not null)
                            {
                                blockX = vx + grid.Origin.X;
                                blockY = vy + grid.Origin.Y;
                                blockZ = vz + grid.Origin.Z;
                                blockIdentifier = vox.State?.GetFullIdentifier() ?? "minecraft:stone";
                                blockStateStr = vox.State?.ToString() ?? blockIdentifier;
                                break;
                            }

                            // 3x3x3 search for grazing angle tolerances
                            double bestDistSq = double.MaxValue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    for (int dz = -1; dz <= 1; dz++)
                                    {
                                        var candPos = new VoxelPosition(vx + dx, vy + dy, vz + dz);
                                        if (grid.TryGetVoxel(candPos, out var candVox) && candVox is not null)
                                        {
                                            var candCenter = new NEXUS3D.Domain.Mathematics.Vector3D(candPos.X + 0.5, candPos.Y + 0.5, candPos.Z + 0.5);
                                            double distSq = (gridLocal - candCenter).LengthSquared();
                                            if (distSq < bestDistSq)
                                            {
                                                bestDistSq = distSq;
                                                blockX = candPos.X + grid.Origin.X;
                                                blockY = candPos.Y + grid.Origin.Y;
                                                blockZ = candPos.Z + grid.Origin.Z;
                                                blockIdentifier = candVox.State?.GetFullIdentifier() ?? "minecraft:stone";
                                                blockStateStr = candVox.State?.ToString() ?? blockIdentifier;
                                            }
                                        }
                                    }
                                }
                            }
                            if (blockIdentifier != null) break;
                        }
                        if (blockIdentifier != null) break;
                    }
                }

                _selectedBlockCoord = (blockX, blockY, blockZ);
                HighlightedBlockCoord = new VoxelPosition(blockX, blockY, blockZ);

                // Highlight individual 1x1x1 block with distinct wireframe
                _selectedBlockHighlightVisual.BoundingBox = new Rect3D(blockX, blockY, blockZ, 1.0, 1.0, 1.0);
                if (!HelixViewport.Children.Contains(_selectedBlockHighlightVisual))
                {
                    HelixViewport.Children.Add(_selectedBlockHighlightVisual);
                }

                ClearCubeHighlight();

                var blockArgs = new BlockPickEventArgs(blockX, blockY, blockZ, blockIdentifier, blockStateStr);
                BlockPicked?.Invoke(this, blockArgs);

                UpdateTelemetry($"Block: [{blockX}, {blockY}, {blockZ}] {blockIdentifier ?? "Block"}");

                if (foundNode != null)
                {
                    SelectedNodeId = foundNode.Id;
                    NodeSelected?.Invoke(this, foundNode.Id);
                }

                UpdateGizmoDisplay();
                return;
            }

            // 2. Bedrock Geometry / Mesh Cube Picking
            if (foundNode is not null)
            {
                ClearBlockHighlight();
                _selectedBlockCoord = null;
                BlockPicked?.Invoke(this, null);

                if (Guid.TryParse(foundNode.Id, out var nodeGuid))
                {
                    SelectionService?.Select(nodeGuid, multiSelect: isCtrl);
                }

                SelectedNodeId = foundNode.Id;
                NodeSelected?.Invoke(this, foundNode.Id);

                UpdateCubeHighlight(foundNode);
                UpdateTelemetry($"Selected: {foundNode.Name}");
                UpdateGizmoDisplay();
                return;
            }
        }

        // 3. Empty Viewport Clicked: Clear selection unless multi-selecting
        if (!isCtrl)
        {
            SelectedNodeId = null;
            _selectedBlockCoord = null;
            HighlightedBlockCoord = null;
            SelectionService?.ClearSelection();
            ClearCubeHighlight();
            ClearBlockHighlight();
            NodeSelected?.Invoke(this, null);
            BlockPicked?.Invoke(this, null);
            UpdateTelemetry(null);
            UpdateGizmoDisplay();
        }
    }

    private void UpdateCubeHighlight(RenderNode node)
    {
        if (SelectionService is not null && SelectionService.SelectedNodeIds.Count > 1 && RawUsmScene is not null)
        {
            var bounds = SelectionService.GetSelectionWorldBounds(RawUsmScene);
            if (bounds.HasValue && bounds.Value.IsValid)
            {
                var bMin = bounds.Value.Min;
                var bMax = bounds.Value.Max;
                double sx = Math.Max(0.02, bMax.X - bMin.X);
                double sy = Math.Max(0.02, bMax.Y - bMin.Y);
                double sz = Math.Max(0.02, bMax.Z - bMin.Z);
                _selectedCubeHighlightVisual.BoundingBox = new Rect3D(bMin.X, bMin.Y, bMin.Z, sx, sy, sz);
                if (!HelixViewport.Children.Contains(_selectedCubeHighlightVisual))
                {
                    HelixViewport.Children.Add(_selectedCubeHighlightVisual);
                }
                return;
            }
        }

        var min = node.BoundsMin;
        var max = node.BoundsMax;
        double width = Math.Max(0.02, max.X - min.X);
        double height = Math.Max(0.02, max.Y - min.Y);
        double depth = Math.Max(0.02, max.Z - min.Z);

        _selectedCubeHighlightVisual.BoundingBox = new Rect3D(min.X, min.Y, min.Z, width, height, depth);
        if (!HelixViewport.Children.Contains(_selectedCubeHighlightVisual))
        {
            HelixViewport.Children.Add(_selectedCubeHighlightVisual);
        }
    }

    private void UpdateCubeHighlightFromSelectedNode()
    {
        if (string.IsNullOrEmpty(SelectedNodeId))
        {
            ClearCubeHighlight();
            return;
        }

        if (Scene is not null)
        {
            var node = FindNodeMatchingId(Scene.RootNodes, SelectedNodeId);
            if (node is not null && node.BoundsMin != node.BoundsMax)
            {
                UpdateCubeHighlight(node);
                return;
            }
        }

        if (RawUsmScene is not null && Guid.TryParse(SelectedNodeId, out var guid))
        {
            var node = RawUsmScene.Graph.FindNode(guid);
            if (node is not null)
            {
                BoundingBox? bb = null;
                if (node.Object?.GetLocalBounds() is { IsValid: true } localBounds)
                {
                    bb = localBounds.Transform(node.GetWorldMatrix());
                }
                else if (node.GetWorldBounds() is { IsValid: true } worldBounds)
                {
                    bb = worldBounds;
                }

                if (bb.HasValue)
                {
                    var wb = bb.Value;
                    double sx = Math.Max(0.02, Math.Abs(wb.Size.X));
                    double sy = Math.Max(0.02, Math.Abs(wb.Size.Y));
                    double sz = Math.Max(0.02, Math.Abs(wb.Size.Z));
                    _selectedCubeHighlightVisual.BoundingBox = new Rect3D(wb.Min.X, wb.Min.Y, wb.Min.Z, sx, sy, sz);
                    if (!HelixViewport.Children.Contains(_selectedCubeHighlightVisual))
                    {
                        HelixViewport.Children.Add(_selectedCubeHighlightVisual);
                    }
                    return;
                }
            }
        }

        ClearCubeHighlight();
    }

    private void ClearCubeHighlight()
    {
        if (HelixViewport.Children.Contains(_selectedCubeHighlightVisual))
        {
            HelixViewport.Children.Remove(_selectedCubeHighlightVisual);
        }
    }

    private void OnHighlightedBlockCoordChanged(VoxelPosition? coord)
    {
        _selectedBlockCoord = coord.HasValue ? (coord.Value.X, coord.Value.Y, coord.Value.Z) : null;
        if (coord.HasValue)
        {
            _selectedBlockHighlightVisual.BoundingBox = new Rect3D(coord.Value.X, coord.Value.Y, coord.Value.Z, 1.0, 1.0, 1.0);
            if (!HelixViewport.Children.Contains(_selectedBlockHighlightVisual))
            {
                HelixViewport.Children.Add(_selectedBlockHighlightVisual);
            }
            ClearCubeHighlight();
        }
        else
        {
            ClearBlockHighlight();
        }
    }

    private void ClearBlockHighlight()
    {
        if (HelixViewport.Children.Contains(_selectedBlockHighlightVisual))
        {
            HelixViewport.Children.Remove(_selectedBlockHighlightVisual);
        }
    }

    private void UpdateTelemetry(string? prefix)
    {
        string baseStats = Scene is null
            ? "Objects: 0 | Triangles: 0 | Vertices: 0 | Voxels: 0"
            : $"Nodes: {Scene.RootNodes.Count} | Triangles: {Scene.TotalTriangles:N0} | Vertices: {Scene.TotalVertices:N0} | Voxels: {Scene.TotalVoxels:N0}";

        TxtStats.Text = string.IsNullOrEmpty(prefix)
            ? baseStats
            : $"{prefix} | {baseStats}";
    }

    private static RenderNode? FindNodeMatchingModel(IEnumerable<RenderNode> nodes, GeometryModel3D model)
    {
        foreach (var node in nodes)
        {
            var found = FindNodeMatchingModel(node, model);
            if (found is not null) return found;
        }
        return null;
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
            if (HelixViewport.Children.Contains(_paletteHighlightVisual))
            {
                HelixViewport.Children.Remove(_paletteHighlightVisual);
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
            _paletteHighlightVisual.BoundingBox = new Rect3D(minX, minY, minZ, Math.Max(1, maxX - minX), Math.Max(1, maxY - minY), Math.Max(1, maxZ - minZ));
            if (!HelixViewport.Children.Contains(_paletteHighlightVisual))
            {
                HelixViewport.Children.Add(_paletteHighlightVisual);
            }
        }
        else if (HelixViewport.Children.Contains(_paletteHighlightVisual))
        {
            HelixViewport.Children.Remove(_paletteHighlightVisual);
        }
    }
}
