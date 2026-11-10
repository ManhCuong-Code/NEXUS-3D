using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Batch.Services;

namespace NEXUS3D.Conversion.Batch.DependencyInjection;

public static class BatchConversionServiceRegistration
{
    public static IServiceCollection AddNexusBatchConversion(this IServiceCollection services)
    {
        services.AddSingleton<IBatchConversionService, BatchConversionService>();
        services.AddSingleton<IJobPriorityQueue, JobPriorityQueue>();
        services.AddSingleton<IBatchProgressReporter, BatchProgressReporter>();
        services.AddSingleton<IBatchWorker, BatchWorker>();
        services.AddSingleton<IBatchProcessingService, BatchProcessingService>();
        return services;
    }
}
