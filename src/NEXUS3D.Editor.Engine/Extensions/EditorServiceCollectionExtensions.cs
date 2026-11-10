using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Picking;
using NEXUS3D.Editor.Engine.Services;

namespace NEXUS3D.Editor.Engine.Extensions;

/// <summary>
/// Dependency Injection registration helpers for Interactive 3D Editor services.
/// </summary>
public static class EditorServiceCollectionExtensions
{
    public static IServiceCollection AddNexus3DEditorEngine(this IServiceCollection services)
    {
        services.AddSingleton<IUndoRedoService, UndoRedoService>();
        services.AddSingleton<ISelectionService, SelectionService>();
        services.AddSingleton<IRaycastPicker, RaycastPicker>();
        services.AddSingleton<ITransformService, TransformService>();
        services.AddSingleton<IPivotService, PivotService>();
        services.AddSingleton<IBoneEditorService, BoneEditorService>();
        services.AddSingleton<ITextureEditorService, TextureEditorService>();

        return services;
    }
}

