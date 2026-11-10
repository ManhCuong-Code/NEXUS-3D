using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using NEXUS3D.App.Rendering;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.Controls;

public partial class Viewport3DControl : UserControl
{
    private readonly HelixSceneRenderer _sceneRenderer = new();
    private ModelVisual3D? _currentSceneVisual;
    private LinesVisual3D? _currentLinesVisual;
    private BoundingBoxWireFrameVisual3D? _currentBoundsVisual;
    private Point _mouseDownPos;

    public static readonly DependencyProperty SceneProperty =
        DependencyProperty.Register(
            nameof(Scene),
            typeof(RenderScene),
            typeof(Viewport3DControl),
            new PropertyMetadata(null, OnSceneChanged));

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

    public event EventHandler<string?>? NodeSelected;

    public RenderScene? Scene
    {
        get => (RenderScene?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
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

    public Viewport3DControl()
    {
        InitializeComponent();
        EmptyStateOverlay.Visibility = Visibility.Visible;

        HelixViewport.MouseDown += OnViewportMouseDown;
        HelixViewport.MouseUp += OnViewportMouseUp;
    }

    private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Viewport3DControl control)
        {
            control.RebuildSceneVisuals();
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
            EmptyStateOverlay.Visibility = Visibility.Visible;
            TxtStats.Text = "Objects: 0 | Triangles: 0 | Vertices: 0 | Voxels: 0";
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

        // Auto-focus on scene load
        FocusScene();
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
            // Toggle lines visual in viewport
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

            // Toggle bounding box visual in viewport
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
        if (Scene is null) return;
        HelixViewport.ZoomExtents();
    }

    public void ApplyCamera(CameraState cam)
    {
        if (cam.ProjectionType == CameraProjectionType.Orthographic)
        {
            var ortho = new OrthographicCamera
            {
                Position = new Point3D(cam.Position.X, cam.Position.Y, cam.Position.Z),
                LookDirection = new Vector3D(cam.LookDirection.X, cam.LookDirection.Y, cam.LookDirection.Z),
                UpDirection = new Vector3D(cam.Up.X, cam.Up.Y, cam.Up.Z),
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
                LookDirection = new Vector3D(cam.LookDirection.X, cam.LookDirection.Y, cam.LookDirection.Z),
                UpDirection = new Vector3D(cam.Up.X, cam.Up.Y, cam.Up.Z),
                FieldOfView = cam.FieldOfView,
                NearPlaneDistance = cam.NearPlane,
                FarPlaneDistance = cam.FarPlane
            };
            HelixViewport.Camera = persp;
        }
    }

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPos = e.GetPosition(HelixViewport);
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        var upPos = e.GetPosition(HelixViewport);
        // Only trigger selection if mouse didn't drag significantly (drag threshold: 4 pixels)
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
            // Find which node owns this mesh
            foreach (var node in Scene.RootNodes)
            {
                var found = FindNodeMatchingModel(node, hitModel);
                if (found is not null)
                {
                    NodeSelected?.Invoke(this, found.Id);
                    return;
                }
            }
        }

        // Clicked background: clear selection
        NodeSelected?.Invoke(this, null);
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
}
