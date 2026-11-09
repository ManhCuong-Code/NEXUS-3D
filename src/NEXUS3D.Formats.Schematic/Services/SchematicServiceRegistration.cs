using Microsoft.Extensions.DependencyInjection;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Schematic.BlockStates;
using NEXUS3D.Formats.Schematic.Conversion;
using NEXUS3D.Formats.Schematic.Legacy;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Palette;
using NEXUS3D.Formats.Schematic.Sponge;
using NEXUS3D.Formats.Schematic.Validation;

namespace NEXUS3D.Formats.Schematic.Services;

/// <summary>
/// Dependency Injection registration extensions for the Stage 03 Schematic Engine.
/// </summary>
public static class SchematicServiceRegistration
{
    public static IServiceCollection AddSchematicFormatServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // NBT Engine
        services.AddSingleton<INbtReader, NbtReader>();
        services.AddSingleton<INbtWriter, NbtWriter>();

        // BlockState & Palette Services
        services.AddSingleton<BlockStateRegistry>();
        services.AddSingleton<PaletteDecoder>();
        services.AddSingleton<PaletteEncoder>();
        services.AddSingleton<PaletteMappingService>();
        services.AddSingleton<SchematicValidator>();

        // Sponge v2 & v3 Engine
        services.AddSingleton<ISpongeV2Parser, SpongeV2Parser>();
        services.AddSingleton<ISpongeV3Parser, SpongeV3Parser>();
        services.AddSingleton<ISpongeV2Exporter, SpongeV2Exporter>();
        services.AddSingleton<ISpongeV3Exporter, SpongeV3Exporter>();
        services.AddSingleton<SpongeSchematicReader>();
        services.AddSingleton<SpongeSchematicWriter>();

        // Legacy .schematic Engine
        services.AddSingleton<ILegacyBlockIdMapping, LegacyBlockIdMapping>();
        services.AddSingleton<ILegacySchematicParser, LegacySchematicParser>();
        services.AddSingleton<ILegacySchematicExporter, LegacySchematicExporter>();

        // USM Conversion
        services.AddSingleton<ISchematicToSceneConverter, SchematicToSceneConverter>();
        services.AddSingleton<ISceneToSchematicConverter, SceneToSchematicConverter>();

        // Unified Format Detector, Parser, Exporter
        services.AddSingleton<ISchematicFormatDetector, SchematicFormatDetector>();
        services.AddSingleton<IFormatDetector>(sp => sp.GetRequiredService<ISchematicFormatDetector>());

        services.AddSingleton<ISchematicReader, SchematicFormatParser>();
        services.AddSingleton<ISchematicParser>(sp => sp.GetRequiredService<ISchematicReader>());
        services.AddSingleton<IFormatParser>(sp => sp.GetRequiredService<ISchematicReader>());

        services.AddSingleton<ISchematicWriter, SchematicFormatExporter>();
        services.AddSingleton<ISchematicExporter>(sp => sp.GetRequiredService<ISchematicWriter>());
        services.AddSingleton<IFormatExporter>(sp => sp.GetRequiredService<ISchematicWriter>());

        return services;
    }
}
