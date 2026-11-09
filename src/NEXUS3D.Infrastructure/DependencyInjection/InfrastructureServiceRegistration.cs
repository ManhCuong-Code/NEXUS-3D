using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Application.Scene;
using NEXUS3D.Application.Serialization;
using NEXUS3D.Infrastructure.Serialization.Json;

namespace NEXUS3D.Infrastructure.DependencyInjection;

/// <summary>
/// Registers USM serialization, schema migration, and scene repository services in the DI container.
/// </summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddUsmInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<ISceneSchemaMigrator, DefaultSceneSchemaMigrator>();
        services.AddSingleton<ISceneSerializer, SceneJsonSerializer>();
        services.AddSingleton<ISceneRepository, InMemorySceneRepository>();
        return services;
    }
}

