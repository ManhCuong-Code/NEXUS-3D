using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Application.Scene;
using NEXUS3D.Domain.Animations;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Factories;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Application.DependencyInjection;

/// <summary>
/// Registers Universal Scene Model (USM) domain factories, validators, coordinate converters, and application services.
/// </summary>
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddUsmApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<SceneFactory>();
        services.AddSingleton<SceneNodeFactory>();
        services.AddSingleton<MeshFactory>();
        services.AddSingleton<VoxelGridFactory>();

        services.AddSingleton<ICoordinateConverter, CoordinateConverter>();

        services.AddSingleton<MeshValidator>();
        services.AddSingleton<IValidator<MeshData>>(sp => sp.GetRequiredService<MeshValidator>());

        services.AddSingleton<VoxelGridValidator>();
        services.AddSingleton<IValidator<VoxelGrid>>(sp => sp.GetRequiredService<VoxelGridValidator>());

        services.AddSingleton<MaterialValidator>();
        services.AddSingleton<IValidator<MaterialData>>(sp => sp.GetRequiredService<MaterialValidator>());

        services.AddSingleton<SkeletonValidator>();
        services.AddSingleton<IValidator<SkeletonData>>(sp => sp.GetRequiredService<SkeletonValidator>());

        services.AddSingleton<AnimationValidator>();
        services.AddSingleton<IValidator<AnimationData>>(sp => sp.GetRequiredService<AnimationValidator>());

        services.AddSingleton<SceneValidator>();
        services.AddSingleton<IValidator<Scene3D>>(sp => sp.GetRequiredService<SceneValidator>());

        services.AddSingleton<ISceneService, SceneService>();
        return services;
    }
}
