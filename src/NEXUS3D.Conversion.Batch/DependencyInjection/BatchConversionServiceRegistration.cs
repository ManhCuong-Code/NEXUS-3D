using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Batch.Services;

namespace NEXUS3D.Conversion.Batch.DependencyInjection;

public static class BatchConversionServiceRegistration
{
    public static IServiceCollection AddNexusBatchConversion(this IServiceCollection services)
    {
        services.AddSingleton<IBatchConversionService, BatchConversionService>();
        return services;
    }
}
