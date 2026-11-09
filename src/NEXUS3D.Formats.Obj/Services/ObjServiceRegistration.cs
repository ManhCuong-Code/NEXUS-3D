using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Obj.Conversion;
using NEXUS3D.Formats.Obj.Exporting;
using NEXUS3D.Formats.Obj.Materials;
using NEXUS3D.Formats.Obj.Parsing;
using NEXUS3D.Formats.Obj.Textures;
using NEXUS3D.Formats.Obj.Validation;

namespace NEXUS3D.Formats.Obj.Services;

/// <summary>
/// Dependency Injection registration extensions for the Stage 04 Wavefront OBJ &amp; MTL Engine.
/// </summary>
public static class ObjServiceRegistration
{
    public static IServiceCollection AddObjFormatServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Low-level OBJ & MTL Parsers and Exporters
        services.AddSingleton<IObjDocumentParser, ObjParser>();
        services.AddSingleton<ObjParser>();
        services.AddSingleton<IMtlParser, MtlParser>();
        services.AddSingleton<MtlParser>();
        services.AddSingleton<IObjDocumentExporter, ObjExporter>();
        services.AddSingleton<ObjExporter>();
        services.AddSingleton<IMtlExporter, MtlExporter>();
        services.AddSingleton<MtlExporter>();

        // Texture Resolution & Validation
        services.AddSingleton<TexturePathResolver>();
        services.AddSingleton<TextureValidationService>();
        services.AddSingleton<ObjGeometryValidator>();
        services.AddSingleton<ObjReferenceValidator>();
        services.AddSingleton<MtlValidator>();
        services.AddSingleton<ObjValidator>();
        services.AddSingleton<IFormatValidator>(sp => sp.GetRequiredService<ObjValidator>());

        // USM Bidirectional Converters
        services.AddSingleton<IObjToSceneConverter, ObjToSceneConverter>();
        services.AddSingleton<ISceneToObjConverter, SceneToObjConverter>();

        // Unified Format Detector, Parser, Exporter
        services.AddSingleton<IObjFormatDetector, ObjFormatDetector>();
        services.AddSingleton<IFormatDetector>(sp => sp.GetRequiredService<IObjFormatDetector>());

        services.AddSingleton<IObjReader, ObjFormatParser>();
        services.AddSingleton<IObjParser>(sp => sp.GetRequiredService<IObjReader>());
        services.AddSingleton<IModelFormatParser>(sp => sp.GetRequiredService<IObjReader>());
        services.AddSingleton<IFormatParser>(sp => sp.GetRequiredService<IObjReader>());

        services.AddSingleton<IObjWriter, ObjFormatExporter>();
        services.AddSingleton<IObjExporter>(sp => sp.GetRequiredService<IObjWriter>());
        services.AddSingleton<IModelFormatExporter>(sp => sp.GetRequiredService<IObjWriter>());
        services.AddSingleton<IFormatExporter>(sp => sp.GetRequiredService<IObjWriter>());

        return services;
    }
}
