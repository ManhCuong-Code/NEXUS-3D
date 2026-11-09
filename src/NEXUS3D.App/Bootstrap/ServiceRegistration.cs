using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.App.Bootstrap;

/// <summary>
/// Central service registration entry point for the NEXUS 3D Dependency Injection container.
/// </summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddNexus3DServices(
        this IServiceCollection services,
        DependencyOptions? options = null)
    {
        Guard.NotNull(services);
        var effectiveOptions = options ?? new DependencyOptions();

        DependencyConfig.ConfigureInfrastructure(services, effectiveOptions);
        DependencyConfig.ConfigureFormatsAndEngine(services);
        DependencyConfig.ConfigureApplicationServices(services);
        DependencyConfig.ConfigurePresentation(services);

        return services;
    }

    public static ServiceProvider BuildNexusServiceProvider(DependencyOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddNexus3DServices(options);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}

