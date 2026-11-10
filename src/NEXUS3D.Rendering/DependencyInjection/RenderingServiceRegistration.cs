using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Services;

namespace NEXUS3D.Rendering.DependencyInjection;

public static class RenderingServiceRegistration
{
    public static IServiceCollection AddNexusRendering(this IServiceCollection services)
    {
        services.AddSingleton<ITextureLoader, TextureLoader>();
        services.AddSingleton<IBoundingBoxCalculator, BoundingBoxCalculator>();
        services.AddSingleton<IMaterialRenderAdapter, MaterialRenderAdapter>();
        services.AddSingleton<MeshRenderBuilder>();
        services.AddSingleton<VoxelRenderBuilder>();
        services.AddSingleton<NormalVisualizer>();
        services.AddSingleton<IRenderResourceManager, RenderResourceManager>();
        services.AddSingleton<ICameraService, CameraService>();
        services.AddSingleton<IRenderSceneAdapter, SceneRenderAdapter>();
        services.AddSingleton<IRenderService, RenderService>();

        // Legacy / foundation viewport controller compatibility
        services.AddSingleton<IViewportController, DefaultViewportController>();

        return services;
    }
}
