using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Engine.Conversion;
using NEXUS3D.Voxelization.Engine.Engine;
using NEXUS3D.Voxelization.Engine.Processing;

namespace NEXUS3D.Voxelization.Engine.DependencyInjection;

public static class VoxelizationServiceRegistration
{
    public static IServiceCollection AddNexusVoxelization(this IServiceCollection services)
    {
        services.AddSingleton<MeshPreprocessor>();
        services.AddSingleton<VoxelOptimizationService>();
        services.AddSingleton<VoxelColorSampler>();
        services.AddSingleton<VoxelMaterialMapper>();
        services.AddSingleton<IVoxelizationValidator, VoxelizationValidator>();
        services.AddSingleton<IVoxelToCuboidConverter, VoxelToCuboidConverter>();
        services.AddSingleton<IVoxelizationEngine, VoxelizationEngine>();

        return services;
    }
}
