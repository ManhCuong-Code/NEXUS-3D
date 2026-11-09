using System.Text.Json;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Conversion;

/// <summary>
/// Contract for converting a parsed <see cref="SchematicDocument"/> into a Universal Scene Model <see cref="Scene3D"/>.
/// </summary>
public interface ISchematicToSceneConverter
{
    (Scene3D Scene, SchematicConversionReport Report) Convert(
        SchematicDocument document,
        SchematicMappingOptions? options = null);
}

/// <summary>
/// Converts a <see cref="SchematicDocument"/> (Sponge v2, Sponge v3, or Legacy <c>.schematic</c>)
/// into a validated USM <see cref="Scene3D"/> preserving palette states, block entities, offsets,
/// metadata, and raw NBT tags.
/// </summary>
public sealed class SchematicToSceneConverter : ISchematicToSceneConverter
{
    public const string SchematicExtensionNamespace = "minecraft:schematic";

    private readonly BlockEntityMapper _blockEntityMapper = new();

    public (Scene3D Scene, SchematicConversionReport Report) Convert(
        SchematicDocument document,
        SchematicMappingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var effectiveOptions = options ?? SchematicMappingOptions.Default;

        var dims = document.Dimensions;
        int totalBlocks = dims.GetCheckedVolume();

        string sceneName = !string.IsNullOrWhiteSpace(document.Metadata.Name)
            ? document.Metadata.Name
            : (!string.IsNullOrWhiteSpace(effectiveOptions.SourceFileName)
                ? Path.GetFileNameWithoutExtension(effectiveOptions.SourceFileName)
                : "Minecraft Schematic");

        var voxelGrid = new VoxelGrid(sceneName)
        {
            Origin = new VoxelPosition(document.Offset.X, document.Offset.Y, document.Offset.Z),
            VoxelSize = VoxelSize.UnitCube
        };

        // 1. Ensure minecraft:air is registered in VoxelPalette first (index 0)
        var airState = new BlockState("minecraft", "air");
        voxelGrid.Palette.GetOrAdd(airState);

        // Map SchematicPalette index -> USM VoxelPalette index
        var schemPaletteToUsmPalette = new Dictionary<int, (int UsmIndex, BlockState State)>();
        foreach (var entry in document.Palette.EntriesByIndex.Values.OrderBy(e => e.Index))
        {
            int usmIdx = voxelGrid.Palette.GetOrAdd(entry.BlockState.DeepClone());
            schemPaletteToUsmPalette[entry.Index] = (usmIdx, entry.BlockState);
        }

        // 2. Index BlockEntities by VoxelPosition
        var blockEntitiesByPos = new Dictionary<VoxelPosition, BlockEntityData>();
        foreach (var entity in document.BlockEntities)
        {
            blockEntitiesByPos[entity.Position] = _blockEntityMapper.MapToUsm(entity);
        }

        // 3. Populate VoxelGrid from linear YZX array: index = x + z * width + y * width * length
        int populatedCount = 0;
        for (int y = 0; y < dims.Height; y++)
        {
            for (int z = 0; z < dims.Length; z++)
            {
                for (int x = 0; x < dims.Width; x++)
                {
                    int linearIdx = SchematicIndexing.ToIndex(x, y, z, dims.Width, dims.Length);
                    int schemPaletteIdx = document.BlockIndices[linearIdx];

                    if (!schemPaletteToUsmPalette.TryGetValue(schemPaletteIdx, out var mapped))
                    {
                        throw new SchematicException(
                            SchematicErrorCodes.InvalidPalette,
                            $"Block at ({x},{y},{z}) references undefined palette index {schemPaletteIdx}.");
                    }

                    var pos = new VoxelPosition(x, y, z);
                    blockEntitiesByPos.TryGetValue(pos, out var blockEntityData);

                    if (!mapped.State.IsAir || effectiveOptions.IncludeAirVoxels || blockEntityData is not null)
                    {
                        var voxel = new Voxel(
                            pos,
                            mapped.UsmIndex,
                            mapped.State.DeepClone(),
                            materialId: null,
                            blockEntity: blockEntityData);

                        voxelGrid.SetVoxel(voxel);
                        populatedCount++;
                    }
                }
            }
        }

        // Explicitly set Bounds to exact schematic dimensions [0..Width-1, 0..Height-1, 0..Length-1]
        // so that even all-air schematics or schematics with air borders retain exact dimensions.
        voxelGrid.Bounds = new VoxelGridBounds(
            VoxelPosition.Zero,
            new VoxelPosition(dims.Width - 1, dims.Height - 1, dims.Length - 1));

        // 4. Create SceneObject and attach "minecraft:schematic" extension metadata
        var sceneObject = new SceneObject(sceneName);
        sceneObject.VoxelGrids.Add(voxelGrid);

        var schemExt = CreateSchematicExtension(document);
        sceneObject.Extensions[SchematicExtensionNamespace] = schemExt;

        // 5. Create SceneNode and Scene3D
        var rootChildNode = new SceneNode(sceneName, sceneObject)
        {
            LocalTransform = new Transform3D(
                new Vector3D(document.Offset.X, document.Offset.Y, document.Offset.Z),
                QuaternionD.Identity,
                Vector3D.One)
        };

        var scene = new Scene3D
        {
            Name = sceneName
        };

        scene.Graph.AddNode(scene.Graph.Root, rootChildNode);

        // 6. Populate CoordinateMetadata
        scene.Coordinates = new CoordinateMetadata
        {
            SourceSystem = CoordinateSystem.DefaultUsm,
            CurrentSystem = CoordinateSystem.DefaultUsm,
            OriginalUnit = MeasurementUnit.Block,
            CurrentUnit = MeasurementUnit.Block,
            ScaleFactor = 1.0,
            Offset = new Vector3D(document.Offset.X, document.Offset.Y, document.Offset.Z),
            SourceOriginDescription = $"Minecraft Schematic ({document.Format}, v{document.FormatVersion})"
        };

        // 7. Populate SceneMetadata, Provenance, Extension, and PreservedRawData
        scene.Metadata.Title = sceneName;
        scene.Metadata.Author = document.Metadata.Author;
        scene.Metadata.AddExtension(schemExt.DeepClone());

        scene.Metadata.Source = new SourceMetadata
        {
            OriginalFileName = !string.IsNullOrWhiteSpace(effectiveOptions.SourceFileName)
                ? Path.GetFileName(effectiveOptions.SourceFileName)
                : null,
            OriginalExtension = document.Format == SchematicFormatId.LegacyMcEdit ? ".schematic" : ".schem",
            OriginalFormatId = document.Format.ToString(),
            OriginalFormatVersion = document.FormatVersion.ToString(),
            FileSizeBytes = effectiveOptions.SourceFileSizeBytes,
            SourceApplication = "Minecraft / WorldEdit / Sponge"
        };

        scene.Metadata.ProvenanceHistory.Add(new DataProvenance(
            DataOriginKind.Imported,
            "NEXUS3D.Formats.Schematic",
            $"Import {document.Format}",
            $"Dimensions: {dims.Width}x{dims.Height}x{dims.Length}, Voxels: {populatedCount}, Palette: {voxelGrid.Palette.Count}, BlockEntities: {document.BlockEntities.Count}"));

        scene.Metadata.LossReport.RecordSupported(
            "Voxels & Palette",
            $"Imported {populatedCount} voxels across {voxelGrid.Palette.Count} BlockState palette entries.");

        if (document.BlockEntities.Count > 0)
        {
            scene.Metadata.LossReport.RecordSupported(
                "BlockEntities",
                $"Imported {document.BlockEntities.Count} block entities with full binary NBT preservation.");
        }

        if (effectiveOptions.PreserveRawNbtData)
        {
            SchematicDataPreserver.PreserveIntoScene(document, scene);
        }

        foreach (var warning in document.Warnings)
        {
            scene.Metadata.Warnings.Add(warning.DeepClone());
        }

        var report = new SchematicConversionReport
        {
            Format = document.Format,
            Dimensions = dims,
            PopulatedVoxelCount = populatedCount,
            PaletteEntryCount = voxelGrid.Palette.Count,
            BlockEntityCount = document.BlockEntities.Count,
            PreservedRawContainerCount = scene.Metadata.PreservedRawData.Count,
            Warnings = scene.Metadata.Warnings.ToList(),
            LossReport = scene.Metadata.LossReport.DeepClone()
        };

        return (scene, report);
    }

    private static ExtensionData CreateSchematicExtension(SchematicDocument doc)
    {
        var ext = new ExtensionData(SchematicExtensionNamespace);
        ext.Values["format"] = JsonSerializer.SerializeToElement(doc.Format.ToString());
        ext.Values["formatVersion"] = JsonSerializer.SerializeToElement(doc.FormatVersion);
        ext.Values["dataVersion"] = JsonSerializer.SerializeToElement(doc.DataVersion);
        ext.Values["width"] = JsonSerializer.SerializeToElement(doc.Dimensions.Width);
        ext.Values["height"] = JsonSerializer.SerializeToElement(doc.Dimensions.Height);
        ext.Values["length"] = JsonSerializer.SerializeToElement(doc.Dimensions.Length);
        ext.Values["offsetX"] = JsonSerializer.SerializeToElement(doc.Offset.X);
        ext.Values["offsetY"] = JsonSerializer.SerializeToElement(doc.Offset.Y);
        ext.Values["offsetZ"] = JsonSerializer.SerializeToElement(doc.Offset.Z);

        if (doc.Metadata.DateUnixMilliseconds.HasValue)
        {
            ext.Values["dateUnixMs"] = JsonSerializer.SerializeToElement(doc.Metadata.DateUnixMilliseconds.Value);
        }

        if (doc.Metadata.RequiredMods.Count > 0)
        {
            ext.Values["requiredMods"] = JsonSerializer.SerializeToElement(doc.Metadata.RequiredMods);
        }

        if (doc.Metadata.WorldEditOffset.HasValue)
        {
            var we = doc.Metadata.WorldEditOffset.Value;
            ext.Values["weOffset"] = JsonSerializer.SerializeToElement(new[] { we.X, we.Y, we.Z });
        }

        if (doc.Metadata.WorldEditOrigin.HasValue)
        {
            var orig = doc.Metadata.WorldEditOrigin.Value;
            ext.Values["weOrigin"] = JsonSerializer.SerializeToElement(new[] { orig.X, orig.Y, orig.Z });
        }

        if (!string.IsNullOrWhiteSpace(doc.Metadata.LegacyMaterialsType))
        {
            ext.Values["legacyMaterials"] = JsonSerializer.SerializeToElement(doc.Metadata.LegacyMaterialsType);
        }

        return ext;
    }
}
