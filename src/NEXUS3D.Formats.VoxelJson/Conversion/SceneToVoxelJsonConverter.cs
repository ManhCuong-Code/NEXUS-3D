using System.Globalization;
using System.Text.Json;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;

namespace NEXUS3D.Formats.VoxelJson.Conversion;

public interface ISceneToVoxelJsonConverter
{
    (VoxelDocument Document, VoxelConversionReport Report) Convert(
        Scene3D scene,
        VoxelMappingOptions? options = null);
}

/// <summary>
/// Converts a Universal Scene Model (<see cref="Scene3D"/>) containing <see cref="VoxelGrid"/> data
/// back into a <see cref="VoxelDocument"/> for export to any supported Voxel JSON dialect.
/// </summary>
public sealed class SceneToVoxelJsonConverter : ISceneToVoxelJsonConverter
{
    public (VoxelDocument Document, VoxelConversionReport Report) Convert(
        Scene3D scene,
        VoxelMappingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        options ??= VoxelMappingOptions.Default;

        var document = new VoxelDocument
        {
            Dialect = options.TargetDialect ?? VoxelJsonDialect.NexusVoxelGridV1
        };

        document.Metadata.Name = !string.IsNullOrWhiteSpace(scene.Metadata.Title)
            ? scene.Metadata.Title
            : scene.Name;
        document.Metadata.Author = scene.Metadata.Author;
        document.Metadata.Description = scene.Metadata.Description;
        document.Metadata.Tags = [.. scene.Metadata.Tags];

        int preservedExtensions = 0;

        // Restore document-level extension data if present
        ExtensionData? sceneExt = null;
        if (scene.Metadata.Extensions.TryGetValue(VoxelDataPreserver.ExtensionNamespace, out ExtensionData? rootExt))
        {
            sceneExt = rootExt;
        }
        else
        {
            foreach (SceneNode node in scene.Graph.Traverse())
            {
                if (node.Object?.Extensions.TryGetValue(VoxelDataPreserver.ExtensionNamespace, out ExtensionData? objExt) == true)
                {
                    sceneExt = objExt;
                    break;
                }
            }
        }

        if (sceneExt is not null)
        {
            if (options.TargetDialect is null &&
                sceneExt.Values.TryGetValue("dialect", out JsonElement dialectElem) &&
                dialectElem.ValueKind == JsonValueKind.String &&
                Enum.TryParse(dialectElem.GetString(), ignoreCase: true, out VoxelJsonDialect parsedDialect) &&
                parsedDialect != VoxelJsonDialect.Unknown)
            {
                document.Dialect = parsedDialect;
            }

            if (sceneExt.Values.TryGetValue("schemaVersion", out JsonElement verElem) &&
                verElem.ValueKind == JsonValueKind.String)
            {
                document.Metadata.SchemaVersion = verElem.GetString() ?? "1.0";
            }

            if (sceneExt.Values.TryGetValue("parentModel", out JsonElement parentElem) &&
                parentElem.ValueKind == JsonValueKind.String)
            {
                document.Metadata.ParentModel = parentElem.GetString();
            }

            if (sceneExt.Values.TryGetValue("ambientOcclusion", out JsonElement aoElem) &&
                (aoElem.ValueKind == JsonValueKind.True || aoElem.ValueKind == JsonValueKind.False))
            {
                document.Metadata.AmbientOcclusion = aoElem.GetBoolean();
            }

            if (sceneExt.Values.TryGetValue("gridDimensions", out JsonElement dimElem) &&
                VoxelJsonParser.TryParseVector3D(dimElem, out Vector3D dimVec))
            {
                document.Grid.Dimensions = dimVec;
            }

            if (sceneExt.Values.TryGetValue("gridOrigin", out JsonElement originElem) &&
                VoxelJsonParser.TryParseVector3D(originElem, out Vector3D originVec))
            {
                document.Grid.Origin = originVec;
            }

            if (sceneExt.Values.TryGetValue("textureSize", out JsonElement texSizeElem) &&
                VoxelJsonParser.TryParseVector2D(texSizeElem, out Vector2D texSizeVec))
            {
                document.Grid.TextureSize = texSizeVec;
            }

            if (sceneExt.Values.TryGetValue("customMetadata", out JsonElement customMetaElem) &&
                customMetaElem.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in customMetaElem.EnumerateObject())
                {
                    document.Metadata.CustomMetadata[p.Name] = p.Value.ValueKind == JsonValueKind.String
                        ? p.Value.GetString() ?? string.Empty
                        : p.Value.GetRawText();
                }
            }

            if (options.PreserveRawData)
            {
                if (sceneExt.Values.TryGetValue("rootExtensionData", out JsonElement rootExtElem))
                {
                    VoxelDataPreserver.RestoreExtensionData(rootExtElem, document.RootExtensionData);
                    preservedExtensions += document.RootExtensionData.Count;
                }

                if (sceneExt.Values.TryGetValue("metadataExtensionData", out JsonElement metaExtElem))
                {
                    VoxelDataPreserver.RestoreExtensionData(metaExtElem, document.Metadata.ExtensionData);
                    preservedExtensions += document.Metadata.ExtensionData.Count;
                }

                if (sceneExt.Values.TryGetValue("gridExtensionData", out JsonElement gridExtElem))
                {
                    VoxelDataPreserver.RestoreExtensionData(gridExtElem, document.Grid.ExtensionData);
                    preservedExtensions += document.Grid.ExtensionData.Count;
                }
            }
        }

        // Convert materials
        var stringIdByMaterialGuid = new Dictionary<Guid, string>();
        var orderedMaterials = scene.Materials.Materials
            .Select((m, idx) =>
            {
                int order = idx;
                if (m.CustomProperties.TryGetValue("voxel.orderIndex", out JsonElement ordElem) &&
                    ordElem.ValueKind == JsonValueKind.Number &&
                    ordElem.TryGetInt32(out int parsedOrd))
                {
                    order = parsedOrd;
                }

                return (Material: m, Order: order);
            })
            .OrderBy(x => x.Order)
            .Select(x => x.Material)
            .ToList();

        foreach (MaterialData usmMat in orderedMaterials)
        {
            string matId = usmMat.Name;
            if (usmMat.CustomProperties.TryGetValue("voxel.materialId", out JsonElement idElem) &&
                idElem.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(idElem.GetString()))
            {
                matId = idElem.GetString()!;
            }

            bool hasExplicitColor = !usmMat.CustomProperties.TryGetValue("voxel.hasExplicitColor", out JsonElement hasColElem)
                ? !usmMat.CustomProperties.ContainsKey("voxel.textureReference")
                : (hasColElem.ValueKind == JsonValueKind.True);

            var voxelMat = new VoxelMaterialReference
            {
                Id = matId,
                Name = usmMat.Name,
                Color = hasExplicitColor
                    ? new VoxelColor(usmMat.BaseColor.R, usmMat.BaseColor.G, usmMat.BaseColor.B, usmMat.BaseColor.A)
                    : null,
                Metallic = usmMat.Metallic,
                Roughness = usmMat.Roughness,
                Opacity = usmMat.Opacity
            };

            if (usmMat.CustomProperties.TryGetValue("voxel.emissive", out JsonElement emElem) &&
                emElem.ValueKind == JsonValueKind.Number &&
                emElem.TryGetDouble(out double emissive))
            {
                voxelMat.Emissive = emissive;
            }

            if (usmMat.CustomProperties.TryGetValue("voxel.textureReference", out JsonElement texRefElem) &&
                texRefElem.ValueKind == JsonValueKind.String)
            {
                voxelMat.TextureReference = texRefElem.GetString();
            }

            if (options.PreserveRawData &&
                usmMat.CustomProperties.TryGetValue("voxel.extensionData", out JsonElement matExtElem))
            {
                VoxelDataPreserver.RestoreExtensionData(matExtElem, voxelMat.ExtensionData);
                preservedExtensions += voxelMat.ExtensionData.Count;
            }

            document.Materials.Add(voxelMat);
            stringIdByMaterialGuid[usmMat.Id] = matId;
        }

        // Gather all VoxelGrids in scene
        var allVoxelGrids = new List<VoxelGrid>();
        foreach (SceneNode node in scene.Graph.Traverse())
        {
            if (node.Object is { VoxelGrids.Count: > 0 } obj)
            {
                allVoxelGrids.AddRange(obj.VoxelGrids);
            }
        }

        if (allVoxelGrids.Count > 0)
        {
            VoxelGrid primaryGrid = allVoxelGrids[0];
            document.Grid.VoxelSize = primaryGrid.VoxelSize.ToVector3D();
            if (sceneExt is null || !sceneExt.Values.ContainsKey("gridOrigin"))
            {
                document.Grid.Origin = primaryGrid.Origin.ToVector3D();
            }

            if (!document.Grid.Dimensions.HasValue && primaryGrid.Bounds.IsValid)
            {
                document.Grid.Dimensions = new Vector3D(
                    primaryGrid.Bounds.Width,
                    primaryGrid.Bounds.Height,
                    primaryGrid.Bounds.Depth);
            }

            // Restore layers from snapshot if available, otherwise from primaryGrid.Layers
            if (sceneExt is not null &&
                sceneExt.Values.TryGetValue("layersSnapshot", out JsonElement layersSnap) &&
                layersSnap.ValueKind == JsonValueKind.Array)
            {
                string tempJson = $"{{\"format\":\"nexus:voxel_grid_v1\",\"grid\":{{}},\"layers\":{layersSnap.GetRawText()},\"voxels\":[]}}";
                var parser = new VoxelJsonParser();
                VoxelJsonParseResult parsedLayers = parser.Parse(tempJson);
                document.Layers.AddRange(parsedLayers.Document.Layers);
            }
            else
            {
                foreach (VoxelLayer usmLayer in primaryGrid.Layers)
                {
                    document.Layers.Add(new VoxelLayerDefinition
                    {
                        Id = usmLayer.Name,
                        Name = usmLayer.Name,
                        IsVisible = usmLayer.IsVisible,
                        IsLocked = usmLayer.IsLocked,
                        Opacity = usmLayer.Opacity
                    });
                }
            }
        }

        var extractedVoxels = new List<(int OrderIndex, VoxelRecord Record)>();
        int fallbackOrder = 0;

        foreach (VoxelGrid grid in allVoxelGrids)
        {
            foreach (Voxel usmVoxel in grid.Voxels)
            {
                BlockState? state = usmVoxel.State;
                if (state is null && grid.Palette.TryGet(usmVoxel.PaletteIndex, out VoxelPaletteEntry? entry) && entry is not null)
                {
                    state = entry.State;
                }

                if (state?.IsAir == true && usmVoxel.BlockEntity?.TypeId != VoxelDataPreserver.BlockEntityTypeId)
                {
                    continue;
                }

                var (record, orderIdx, extCount) = ReconstructVoxelRecord(
                    usmVoxel,
                    state,
                    grid,
                    stringIdByMaterialGuid,
                    fallbackOrder++,
                    options.PreserveRawData);

                preservedExtensions += extCount;
                extractedVoxels.Add((orderIdx, record));
            }
        }

        document.Voxels = extractedVoxels
            .OrderBy(x => x.OrderIndex)
            .Select(x => x.Record)
            .ToList();

        var lossReport = new LossReport();
        lossReport.RecordSupported("VoxelGrid", $"Converted {document.Voxels.Count} USM voxels to VoxelDocument.");

        var report = new VoxelConversionReport
        {
            Dialect = document.Dialect,
            VoxelCount = document.Voxels.Count,
            MaterialCount = document.Materials.Count,
            LayerCount = document.Layers.Count,
            PreservedExtensionCount = preservedExtensions,
            LossReport = lossReport
        };

        return (document, report);
    }

    private static (VoxelRecord Record, int OrderIndex, int ExtensionCount) ReconstructVoxelRecord(
        Voxel usmVoxel,
        BlockState? state,
        VoxelGrid grid,
        Dictionary<Guid, string> stringIdByMaterialGuid,
        int defaultOrder,
        bool preserveRawData)
    {
        var record = new VoxelRecord
        {
            Position = usmVoxel.Position.ToVector3D()
        };

        int orderIndex = defaultOrder;
        int extCount = 0;

        if (usmVoxel.MaterialId.HasValue &&
            stringIdByMaterialGuid.TryGetValue(usmVoxel.MaterialId.Value, out string? mappedMatId))
        {
            record.MaterialId = mappedMatId;
        }

        if (state is not null)
        {
            string fullId = state.GetFullIdentifier();
            if (!string.Equals(fullId, "voxel:solid", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(fullId, "default:air", StringComparison.OrdinalIgnoreCase))
            {
                record.VoxelType = fullId;
            }

            if (string.IsNullOrWhiteSpace(record.MaterialId) &&
                state.TryGetProperty("material", out string? stateMat))
            {
                record.MaterialId = stateMat;
            }

            if (state.TryGetProperty("color", out string? stateCol) &&
                VoxelColor.TryParseHex(stateCol, out VoxelColor parsedStateColor))
            {
                record.Color = parsedStateColor;
            }

            if (state.TryGetProperty("layer", out string? stateLayer))
            {
                record.LayerId = stateLayer;
            }
        }

        if (string.IsNullOrWhiteSpace(record.LayerId))
        {
            foreach (VoxelLayer layer in grid.Layers)
            {
                if (layer.Positions.Contains(usmVoxel.Position))
                {
                    record.LayerId = layer.Name;
                    break;
                }
            }
        }

        if (usmVoxel.BlockEntity is { TypeId: VoxelDataPreserver.BlockEntityTypeId } meta)
        {
            if (meta.Properties.TryGetValue("orderIndex", out JsonElement ordElem) &&
                ordElem.ValueKind == JsonValueKind.Number &&
                ordElem.TryGetInt32(out int parsedOrder))
            {
                orderIndex = parsedOrder;
            }

            if (meta.Properties.TryGetValue("exactPosition", out JsonElement posElem) &&
                VoxelJsonParser.TryParseVector3D(posElem, out Vector3D exactPos))
            {
                record.Position = exactPos;
            }

            if (meta.Properties.TryGetValue("size", out JsonElement sizeElem) &&
                VoxelJsonParser.TryParseVector3D(sizeElem, out Vector3D sizeVec))
            {
                record.Size = sizeVec;
            }

            if (meta.Properties.TryGetValue("rotation", out JsonElement rotElem) &&
                VoxelJsonParser.TryParseVector3D(rotElem, out Vector3D rotVec))
            {
                record.Rotation = rotVec;
            }

            if (meta.Properties.TryGetValue("rotationOrigin", out JsonElement rotOrigElem) &&
                VoxelJsonParser.TryParseVector3D(rotOrigElem, out Vector3D rotOrigVec))
            {
                record.RotationOrigin = rotOrigVec;
            }

            if (meta.Properties.TryGetValue("rotationAxis", out JsonElement rotAxisElem) &&
                rotAxisElem.ValueKind == JsonValueKind.String)
            {
                record.RotationAxis = rotAxisElem.GetString();
            }

            if (meta.Properties.TryGetValue("rescale", out JsonElement rescaleElem) &&
                (rescaleElem.ValueKind == JsonValueKind.True || rescaleElem.ValueKind == JsonValueKind.False))
            {
                record.Rescale = rescaleElem.GetBoolean();
            }

            if (meta.Properties.TryGetValue("shade", out JsonElement shadeElem) &&
                (shadeElem.ValueKind == JsonValueKind.True || shadeElem.ValueKind == JsonValueKind.False))
            {
                record.Shade = shadeElem.GetBoolean();
            }

            if (meta.Properties.TryGetValue("color", out JsonElement colElem) &&
                colElem.ValueKind == JsonValueKind.String &&
                VoxelColor.TryParseHex(colElem.GetString(), out VoxelColor metaColor))
            {
                record.Color = metaColor;
            }

            if (meta.Properties.TryGetValue("materialId", out JsonElement matIdElem) &&
                matIdElem.ValueKind == JsonValueKind.String)
            {
                record.MaterialId = matIdElem.GetString();
            }

            if (meta.Properties.TryGetValue("voxelType", out JsonElement vTypeElem) &&
                vTypeElem.ValueKind == JsonValueKind.String)
            {
                record.VoxelType = vTypeElem.GetString();
            }

            if (meta.Properties.TryGetValue("layerId", out JsonElement lIdElem) &&
                lIdElem.ValueKind == JsonValueKind.String)
            {
                record.LayerId = lIdElem.GetString();
            }

            if (meta.Properties.TryGetValue("name", out JsonElement nameElem) &&
                nameElem.ValueKind == JsonValueKind.String)
            {
                record.Name = nameElem.GetString();
            }

            if (meta.Properties.TryGetValue("customProperties", out JsonElement customElem) &&
                customElem.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in customElem.EnumerateObject())
                {
                    record.CustomProperties[p.Name] = p.Value.ValueKind == JsonValueKind.String
                        ? p.Value.GetString() ?? string.Empty
                        : p.Value.GetRawText();
                }
            }

            if (meta.Properties.TryGetValue("faces", out JsonElement facesElem) &&
                facesElem.ValueKind == JsonValueKind.Object)
            {
                string tempFacesJson = $"{{\"format\":\"nexus:voxel_grid_v1\",\"grid\":{{}},\"voxels\":[{{\"position\":[0,0,0],\"faces\":{facesElem.GetRawText()}}}]}}";
                var parser = new VoxelJsonParser();
                VoxelJsonParseResult parsedFaces = parser.Parse(tempFacesJson);
                if (parsedFaces.Document.Voxels.Count == 1)
                {
                    foreach ((string k, VoxelCuboidFaceUv v) in parsedFaces.Document.Voxels[0].Faces)
                    {
                        record.Faces[k] = v;
                        extCount += v.ExtensionData.Count;
                    }
                }
            }

            if (preserveRawData && meta.Properties.TryGetValue("extensionData", out JsonElement extElem))
            {
                VoxelDataPreserver.RestoreExtensionData(extElem, record.ExtensionData);
                extCount += record.ExtensionData.Count;
            }
        }

        return (record, orderIndex, extCount);
    }
}
