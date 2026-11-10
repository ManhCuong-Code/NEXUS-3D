using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.VoxelJson.Conversion;
using NEXUS3D.Formats.VoxelJson.Exporting;
using NEXUS3D.Formats.VoxelJson.Parsing;
using NEXUS3D.Formats.VoxelJson.Validation;

namespace NEXUS3D.Formats.VoxelJson.Services;

/// <summary>
/// Dependency Injection registration extensions for the Stage 05 Voxel JSON Engine.
/// </summary>
public static class VoxelJsonServiceRegistration
{
    public static IServiceCollection AddVoxelJsonFormatServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<VoxelJsonParser>();
        services.AddSingleton<VoxelJsonExporter>();
        services.AddSingleton<VoxelJsonValidator>();

        services.AddSingleton<IVoxelJsonToSceneConverter, VoxelJsonToSceneConverter>();
        services.AddSingleton<VoxelJsonToSceneConverter>();
        services.AddSingleton<ISceneToVoxelJsonConverter, SceneToVoxelJsonConverter>();
        services.AddSingleton<SceneToVoxelJsonConverter>();

        services.AddSingleton<IVoxelJsonFormatDetector, VoxelJsonFormatDetector>();
        services.AddSingleton<IFormatDetector>(sp => sp.GetRequiredService<IVoxelJsonFormatDetector>());

        services.AddSingleton<IVoxelJsonReader, VoxelJsonFormatParser>();
        services.AddSingleton<IVoxelJsonParser>(sp => sp.GetRequiredService<IVoxelJsonReader>());
        services.AddSingleton<IModelFormatParser>(sp => sp.GetRequiredService<IVoxelJsonReader>());
        services.AddSingleton<IFormatParser>(sp => sp.GetRequiredService<IVoxelJsonReader>());

        services.AddSingleton<IVoxelJsonWriter, VoxelJsonFormatExporter>();
        services.AddSingleton<IVoxelJsonExporter>(sp => sp.GetRequiredService<IVoxelJsonWriter>());
        services.AddSingleton<IModelFormatExporter>(sp => sp.GetRequiredService<IVoxelJsonWriter>());
        services.AddSingleton<IFormatExporter>(sp => sp.GetRequiredService<IVoxelJsonWriter>());

        return services;
    }
}
