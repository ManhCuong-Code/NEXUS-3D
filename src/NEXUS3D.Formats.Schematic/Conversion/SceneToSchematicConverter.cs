using System.Text.Json;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Palette;

namespace NEXUS3D.Formats.Schematic.Conversion;

/// <summary>
/// Contract for converting a Universal Scene Model <see cref="Scene3D"/> into a <see cref="SchematicDocument"/>.
/// </summary>
public interface ISceneToSchematicConverter
{
    (SchematicDocument Document, SchematicConversionReport Report) Convert(
        Scene3D scene,
        SchematicMappingOptions? options = null);
}

/// <summary>
/// Converts a USM <see cref="Scene3D"/> containing one or more <see cref="VoxelGrid"/> instances
/// into a <see cref="SchematicDocument"/> ready for export to Sponge v2, Sponge v3, or Legacy <c>.schematic</c>.
/// </summary>
public sealed class SceneToSchematicConverter : ISceneToSchematicConverter
{
    private readonly BlockEntityMapper _blockEntityMapper = new();

    public (SchematicDocument Document, SchematicConversionReport Report) Convert(
        Scene3D scene,
        SchematicMappingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var effectiveOptions = options ?? SchematicMappingOptions.Default;

        // 1. Collect all VoxelGrids from SceneGraph
        var gridsWithNodes = new List<(SceneNode Node, SceneObject Object, VoxelGrid Grid)>();
        foreach (var node in scene.Graph.Traverse())
        {
            if (node.Object is { VoxelGrids.Count: > 0 } obj)
            {
                foreach (var grid in obj.VoxelGrids)
                {
                    gridsWithNodes.Add((node, obj, grid));
                }
            }
        }

        if (gridsWithNodes.Count == 0)
        {
            throw new SchematicException(
                SchematicErrorCodes.MissingRequiredData,
                "Cannot convert Scene3D to SchematicDocument because the scene contains no VoxelGrid.");
        }

        var primary = gridsWithNodes[0];
        VoxelGrid primaryGrid = primary.Grid;

        // 2. Resolve extension metadata if preserved on SceneMetadata or SceneObject
        ExtensionData? schemExt = null;
        if (!primary.Object.Extensions.TryGetValue(SchematicToSceneConverter.SchematicExtensionNamespace, out schemExt))
        {
            scene.Metadata.Extensions.TryGetValue(SchematicToSceneConverter.SchematicExtensionNamespace, out schemExt);
        }

        // 3. Determine bounding box and coordinate normalization shift
        VoxelPosition minPos = VoxelPosition.Zero;
        VoxelPosition maxPos = VoxelPosition.Zero;

        int extW = 0, extH = 0, extL = 0;
        bool hasExtensionDims = TryGetInt(schemExt, "width", out extW) &&
                                TryGetInt(schemExt, "height", out extH) &&
                                TryGetInt(schemExt, "length", out extL) &&
                                extW > 0 && extH > 0 && extL > 0;

        if (gridsWithNodes.Count == 1 && primaryGrid.Bounds.IsValid)
        {
            minPos = primaryGrid.Bounds.Min;
            maxPos = primaryGrid.Bounds.Max;

            // If original dimensions were preserved and all voxels lie inside [0..extW-1, 0..extH-1, 0..extL-1], keep exact bounds
            if (hasExtensionDims &&
                minPos.X >= 0 && minPos.Y >= 0 && minPos.Z >= 0 &&
                maxPos.X < extW && maxPos.Y < extH && maxPos.Z < extL)
            {
                minPos = VoxelPosition.Zero;
                maxPos = new VoxelPosition(extW - 1, extH - 1, extL - 1);
            }
        }
        else if (hasExtensionDims)
        {
            minPos = VoxelPosition.Zero;
            maxPos = new VoxelPosition(extW - 1, extH - 1, extL - 1);
        }
        else
        {
            var combinedBounds = VoxelGridBounds.Empty;
            foreach (var (_, _, grid) in gridsWithNodes)
            {
                foreach (var v in grid.Voxels)
                {
                    combinedBounds = combinedBounds.Encapsulate(v.Position + grid.Origin);
                }
            }

            if (combinedBounds.IsValid)
            {
                minPos = combinedBounds.Min;
                maxPos = combinedBounds.Max;
            }
        }

        int width = Math.Max(1, maxPos.X - minPos.X + 1);
        int height = Math.Max(1, maxPos.Y - minPos.Y + 1);
        int length = Math.Max(1, maxPos.Z - minPos.Z + 1);

        var dimensions = new SchematicDimensions(width, height, length);
        int totalBlocks = dimensions.GetCheckedVolume();

        // 4. Build SchematicPalette & BlockIndices
        var palette = new SchematicPalette();
        var airState = new BlockState("minecraft", "air");
        int airPaletteIndex = palette.GetOrAdd(airState);

        // Pre-populate palette entries from primary VoxelGrid.Palette to preserve ordering where possible
        foreach (var entry in primaryGrid.Palette.Entries)
        {
            palette.GetOrAdd(entry.State.DeepClone());
        }

        int[] blockIndices = new int[totalBlocks];
        if (airPaletteIndex != 0)
        {
            Array.Fill(blockIndices, airPaletteIndex);
        }

        var blockEntities = new List<SchematicBlockEntity>();
        int targetVersion = effectiveOptions.TargetFormat switch
        {
            SchematicFormatId.SpongeV3 => 3,
            SchematicFormatId.LegacyMcEdit => 1,
            _ => 2
        };

        int populatedVoxels = 0;

        foreach (var (_, _, grid) in gridsWithNodes)
        {
            VoxelPosition gridShift = gridsWithNodes.Count > 1 ? grid.Origin : VoxelPosition.Zero;

            foreach (var voxel in grid.Voxels)
            {
                VoxelPosition localPos = voxel.Position + gridShift - minPos;
                if (!dimensions.Contains(localPos))
                {
                    continue;
                }

                BlockState state = voxel.State
                    ?? (grid.Palette.TryGet(voxel.PaletteIndex, out var pEntry) && pEntry is not null
                        ? pEntry.State
                        : airState);

                int schemPaletteIdx = palette.GetOrAdd(state);
                int linearIdx = SchematicIndexing.ToIndex(localPos.X, localPos.Y, localPos.Z, width, length);
                blockIndices[linearIdx] = schemPaletteIdx;

                if (!state.IsAir)
                {
                    populatedVoxels++;
                }

                if (voxel.BlockEntity is not null)
                {
                    blockEntities.Add(_blockEntityMapper.MapFromUsm(localPos, voxel.BlockEntity, targetVersion));
                }
            }
        }

        // 5. Reconstruct Offset and Metadata
        var offset = new SchematicOffset(
            primaryGrid.Origin.X + minPos.X,
            primaryGrid.Origin.Y + minPos.Y,
            primaryGrid.Origin.Z + minPos.Z);

        if (TryGetInt(schemExt, "offsetX", out int ox) &&
            TryGetInt(schemExt, "offsetY", out int oy) &&
            TryGetInt(schemExt, "offsetZ", out int oz) &&
            minPos == VoxelPosition.Zero)
        {
            offset = new SchematicOffset(ox, oy, oz);
        }

        int dataVersion = TryGetInt(schemExt, "dataVersion", out int dv) && dv > 0
            ? dv
            : effectiveOptions.DefaultDataVersion;

        var metadata = new SchematicMetadata
        {
            Name = !string.IsNullOrWhiteSpace(scene.Metadata.Title) ? scene.Metadata.Title : scene.Name,
            Author = scene.Metadata.Author
        };

        if (schemExt is not null)
        {
            if (schemExt.Values.TryGetValue("dateUnixMs", out var dateElem) && dateElem.TryGetInt64(out long dateMs))
            {
                metadata.DateUnixMilliseconds = dateMs;
            }

            if (schemExt.Values.TryGetValue("requiredMods", out var modsElem) && modsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in modsElem.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } modStr)
                    {
                        metadata.RequiredMods.Add(modStr);
                    }
                }
            }

            if (TryGetIntArray3(schemExt, "weOffset", out var weOff))
            {
                metadata.WorldEditOffset = weOff;
            }

            if (TryGetIntArray3(schemExt, "weOrigin", out var weOrig))
            {
                metadata.WorldEditOrigin = weOrig;
            }

            if (schemExt.Values.TryGetValue("legacyMaterials", out var legMatElem) && legMatElem.ValueKind == JsonValueKind.String)
            {
                metadata.LegacyMaterialsType = legMatElem.GetString();
            }
        }

        var doc = new SchematicDocument
        {
            Format = effectiveOptions.TargetFormat,
            FormatVersion = targetVersion,
            DataVersion = dataVersion,
            Dimensions = dimensions,
            Offset = offset,
            Palette = palette,
            BlockIndices = blockIndices,
            BlockEntities = blockEntities,
            Metadata = metadata,
            Warnings = scene.Metadata.Warnings.Select(w => w.DeepClone()).ToList()
        };

        if (effectiveOptions.PreserveRawNbtData)
        {
            SchematicDataPreserver.RestoreFromScene(scene, doc);
        }

        var report = new SchematicConversionReport
        {
            Format = effectiveOptions.TargetFormat,
            Dimensions = dimensions,
            PopulatedVoxelCount = populatedVoxels,
            PaletteEntryCount = palette.Count,
            BlockEntityCount = blockEntities.Count,
            PreservedRawContainerCount = scene.Metadata.PreservedRawData.Count,
            Warnings = doc.Warnings.ToList(),
            LossReport = scene.Metadata.LossReport.DeepClone()
        };

        return (doc, report);
    }

    private static bool TryGetInt(ExtensionData? ext, string key, out int value)
    {
        if (ext is not null &&
            ext.Values.TryGetValue(key, out var elem) &&
            elem.ValueKind == JsonValueKind.Number &&
            elem.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetIntArray3(ExtensionData? ext, string key, out SchematicOffset offset)
    {
        if (ext is not null &&
            ext.Values.TryGetValue(key, out var elem) &&
            elem.ValueKind == JsonValueKind.Array &&
            elem.GetArrayLength() == 3)
        {
            int[] arr = new int[3];
            int idx = 0;
            foreach (var item in elem.EnumerateArray())
            {
                arr[idx++] = item.GetInt32();
            }

            offset = new SchematicOffset(arr[0], arr[1], arr[2]);
            return true;
        }

        offset = SchematicOffset.Zero;
        return false;
    }
}
