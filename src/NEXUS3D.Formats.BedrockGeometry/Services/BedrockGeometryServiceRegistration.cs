using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Exporting;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.BedrockGeometry.Validation;

namespace NEXUS3D.Formats.BedrockGeometry.Services;

/// <summary>
/// Dependency Injection registration extensions for the Stage 05 Minecraft Bedrock Geometry JSON Engine.
/// </summary>
public static class BedrockGeometryServiceRegistration
{
    public static IServiceCollection AddBedrockGeometryFormatServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<BedrockGeometryParser>();
        services.AddSingleton<BedrockGeometryExporter>();
        services.AddSingleton<BedrockGeometryValidator>();
        services.AddSingleton<BedrockBoneValidator>();
        services.AddSingleton<BedrockCubeValidator>();

        services.AddSingleton<IBedrockGeometryToSceneConverter, BedrockGeometryToSceneConverter>();
        services.AddSingleton<BedrockGeometryToSceneConverter>();
        services.AddSingleton<ISceneToBedrockGeometryConverter, SceneToBedrockGeometryConverter>();
        services.AddSingleton<SceneToBedrockGeometryConverter>();

        services.AddSingleton<IBedrockGeometryFormatDetector, BedrockGeometryFormatDetector>();
        services.AddSingleton<IFormatDetector>(sp => sp.GetRequiredService<IBedrockGeometryFormatDetector>());

        services.AddSingleton<IBedrockGeometryReader, BedrockGeometryFormatParser>();
        services.AddSingleton<IBedrockGeometryParser>(sp => sp.GetRequiredService<IBedrockGeometryReader>());
        services.AddSingleton<IModelFormatParser>(sp => sp.GetRequiredService<IBedrockGeometryReader>());
        services.AddSingleton<IFormatParser>(sp => sp.GetRequiredService<IBedrockGeometryReader>());

        services.AddSingleton<IBedrockGeometryWriter, BedrockGeometryFormatExporter>();
        services.AddSingleton<IBedrockGeometryExporter>(sp => sp.GetRequiredService<IBedrockGeometryWriter>());
        services.AddSingleton<IModelFormatExporter>(sp => sp.GetRequiredService<IBedrockGeometryWriter>());
        services.AddSingleton<IFormatExporter>(sp => sp.GetRequiredService<IBedrockGeometryWriter>());

        return services;
    }
}
