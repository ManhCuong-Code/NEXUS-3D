using System.Windows;
using System.Windows.Controls;
using NEXUS3D.App.ViewModels;
using NEXUS3D.Rendering.Contracts;

namespace NEXUS3D.App.Controls;

public partial class OrientationGizmoControl : UserControl
{
    public static readonly DependencyProperty CameraProperty =
        DependencyProperty.Register(
            nameof(Camera),
            typeof(CameraViewModel),
            typeof(OrientationGizmoControl),
            new PropertyMetadata(null));

    public CameraViewModel? Camera
    {
        get => (CameraViewModel?)GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    public OrientationGizmoControl()
    {
        InitializeComponent();
    }

    private void OnPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tagStr } &&
            Enum.TryParse<CameraPresetView>(tagStr, ignoreCase: true, out var preset))
        {
            Camera?.SetPreset(preset);
        }
    }
}
