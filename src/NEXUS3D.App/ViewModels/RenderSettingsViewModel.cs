using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.App.ViewModels;

/// <summary>
/// ViewModel controlling viewport visual modes, wireframes, normals, lighting presets, and background colors.
/// </summary>
public sealed partial class RenderSettingsViewModel : ObservableObject
{
    private readonly IRenderService _renderService;

    [ObservableProperty]
    private ShadingMode _selectedShading = ShadingMode.Textured;

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _showAxes = true;

    [ObservableProperty]
    private bool _showBoundingBox;

    [ObservableProperty]
    private bool _showNormals;

    [ObservableProperty]
    private bool _showWireframe;

    [ObservableProperty]
    private bool _showBones = true;

    [ObservableProperty]
    private bool _showTextures = true;

    [ObservableProperty]
    private float _normalScale = 0.5f;

    [ObservableProperty]
    private string _backgroundColorHex = "#1E1E24";

    public event EventHandler<RenderSettings>? SettingsChanged;

    public RenderSettingsViewModel(IRenderService renderService)
    {
        _renderService = renderService;
        SyncFromService();
    }

    private void SyncFromService()
    {
        var s = _renderService.Settings;
        SelectedShading = s.Shading;
        ShowGrid = s.ShowGrid;
        ShowAxes = s.ShowAxes;
        ShowBoundingBox = s.ShowBoundingBox;
        ShowNormals = s.ShowNormals;
        ShowWireframe = s.ShowWireframe;
        ShowBones = s.ShowBones;
        ShowTextures = s.ShowTextures;
        NormalScale = s.NormalScale;
        BackgroundColorHex = s.BackgroundColorHex;
    }

    partial void OnSelectedShadingChanged(ShadingMode value) => ApplyChanges();
    partial void OnShowGridChanged(bool value) => ApplyChanges();
    partial void OnShowAxesChanged(bool value) => ApplyChanges();
    partial void OnShowBoundingBoxChanged(bool value) => ApplyChanges();
    partial void OnShowNormalsChanged(bool value) => ApplyChanges();
    partial void OnShowWireframeChanged(bool value) => ApplyChanges();
    partial void OnShowBonesChanged(bool value) => ApplyChanges();
    partial void OnShowTexturesChanged(bool value) => ApplyChanges();
    partial void OnNormalScaleChanged(float value) => ApplyChanges();
    partial void OnBackgroundColorHexChanged(string value) => ApplyChanges();

    [RelayCommand]
    public void ApplyPresetStudio()
    {
        BackgroundColorHex = "#1E1E24";
        SelectedShading = ShadingMode.Textured;
        ShowTextures = true;
        ShowGrid = true;
        ShowAxes = true;
    }

    [RelayCommand]
    public void ApplyPresetNeutral()
    {
        BackgroundColorHex = "#2B2B30";
        SelectedShading = ShadingMode.Solid;
        ShowTextures = false;
        ShowGrid = true;
    }

    [RelayCommand]
    public void ApplyPresetWireframe()
    {
        BackgroundColorHex = "#101014";
        SelectedShading = ShadingMode.Wireframe;
        ShowWireframe = true;
        ShowGrid = true;
    }

    [RelayCommand]
    public void ApplyPresetDark()
    {
        BackgroundColorHex = "#0C0C0E";
        SelectedShading = ShadingMode.Textured;
        ShowGrid = false;
        ShowAxes = false;
    }

    private void ApplyChanges()
    {
        var settings = new RenderSettings(
            Shading: SelectedShading,
            ShowGrid: ShowGrid,
            ShowAxes: ShowAxes,
            ShowBoundingBox: ShowBoundingBox,
            ShowNormals: ShowNormals,
            ShowWireframe: ShowWireframe,
            ShowBones: ShowBones,
            ShowTextures: ShowTextures,
            BackgroundColorHex: BackgroundColorHex,
            NormalScale: NormalScale);

        _renderService.UpdateSettings(settings);
        SettingsChanged?.Invoke(this, settings);
    }
}

