using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NEXUS3D.Application.Models;

namespace NEXUS3D.App.ViewModels;

public sealed partial class FileOpenInspectionViewModel : ObservableObject
{
    [ObservableProperty]
    private FileClassificationReport _report;

    [ObservableProperty]
    private FormatCategory _selectedCategory;

    [ObservableProperty]
    private bool _alwaysInspectFiles = true;

    [ObservableProperty]
    private bool _confirmed;

    public IReadOnlyList<FormatCategory> AvailableCategories { get; } = new[]
    {
        FormatCategory.PolygonMesh,
        FormatCategory.MinecraftSchematic,
        FormatCategory.VoxelModel,
        FormatCategory.BedrockGeometry
    };

    public event EventHandler<bool>? RequestClose;

    public FileOpenInspectionViewModel(FileClassificationReport report)
    {
        _report = report;
        _selectedCategory = report.Category;
    }

    [RelayCommand]
    public void Open()
    {
        // Apply category override if user altered it
        if (Report.Category != SelectedCategory)
        {
            Report.Category = SelectedCategory;
        }

        Confirmed = true;
        RequestClose?.Invoke(this, true);
    }

    [RelayCommand]
    public void Cancel()
    {
        Confirmed = false;
        RequestClose?.Invoke(this, false);
    }
}
