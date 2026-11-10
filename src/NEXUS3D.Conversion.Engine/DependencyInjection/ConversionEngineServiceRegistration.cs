using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Engine.Services;

namespace NEXUS3D.Conversion.Engine.DependencyInjection;

public static class ConversionEngineServiceRegistration
{
    public static IServiceCollection AddNexusConversionEngine(this IServiceCollection services)
    {
        services.AddSingleton<IFormatCapabilityRegistry, FormatCapabilityRegistry>();
        services.AddSingleton<IConversionPlanner, ConversionPlanner>();
        services.AddSingleton<ICoordinateConverter, CoordinateConversionService>();
        services.AddSingleton<IVoxelToMeshConverter, VoxelToMeshConverter>();
        services.AddSingleton<IMeshToVoxelConverter, MeshToVoxelConverter>();
        services.AddSingleton<IBedrockCubeToMeshConverter, BedrockCubeToMeshConverter>();
        services.AddSingleton<IPolygonTriangulator, PolygonTriangulator>();
        services.AddSingleton<IMaterialMappingService, MaterialMappingService>();
        services.AddSingleton<IDataLossAnalyzer, DataLossAnalyzer>();

        services.AddSingleton<UniversalConversionPipeline>();
        services.AddSingleton<IUniversalConversionPipeline>(sp => sp.GetRequiredService<UniversalConversionPipeline>());
        services.AddSingleton<IConversionExecutor>(sp => sp.GetRequiredService<UniversalConversionPipeline>());

        return services;
    }
}
