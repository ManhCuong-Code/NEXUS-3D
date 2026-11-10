using System.Text.Json;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;

namespace NEXUS3D.Formats.VoxelJson.Conversion;

public interface IVoxelJsonToSceneConverter
{
    (Scene3D Scene, VoxelConversionReport Report) Convert(
        VoxelDocument document,
        VoxelMappingOptions? options = null,
        IReadOnlyList<JsonFormatDiagnostic>? parseDiagnostics = null);
}

/// <summary>
/// Converts a parsed <see cref="VoxelDocument"/> into a Universal Scene Model (<see cref="Scene3D"/>)
/// using native USM <see cref="VoxelGrid"/>, <see cref="Voxel"/>, <see cref="VoxelPalette"/>, <see cref="VoxelLayer"/>,
/// and <see cref="MaterialData"/> without converting voxels into polygon meshes.
/// </summary>
public sealed class VoxelJsonToSceneConverter : IVoxelJsonToSceneConverter
{
    public (Scene3D Scene, VoxelConversionReport Report) Convert(
        VoxelDocument document,
        VoxelMappingOptions? options = null,
        IReadOnlyList<JsonFormatDiagnostic>? parseDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= VoxelMappingOptions.Default;

        var lossReport = new LossReport();
        var warnings = new List<WarningRecord>();
        var issues = new List<ValidationIssue>();

        if (parseDiagnostics is not null)
        {
            foreach (JsonFormatDiagnostic diag in parseDiagnostics)
            {
                var severity = diag.IsError ? ValidationSeverity.Error : ValidationSeverity.Warning;
                issues.Add(new ValidationIssue(diag.Code, diag.Message, severity, diag.JsonPath));
                if (!diag.IsError)
                {
                    warnings.Add(new WarningRecord
                    {
                        Code = diag.Code,
                        Message = diag.Message,
                        SourceComponent = nameof(VoxelJsonToSceneConverter)
                    });
                }
            }
        }

        string sceneName = string.IsNullOrWhiteSpace(document.Metadata.Name)
            ? "Voxel Scene"
            : document.Metadata.Name;

        var scene = new Scene3D
        {
            Name = sceneName
        };

        scene.Metadata.Title = sceneName;
        scene.Metadata.Author = document.Metadata.Author;
        scene.Metadata.Description = document.Metadata.Description;
        scene.Metadata.Tags = [.. document.Metadata.Tags];
        scene.Metadata.Source = new SourceMetadata
        {
            OriginalFileName = options.SourceFileName,
            OriginalExtension = ".json",
            OriginalFormatId = "voxel:json",
            OriginalFormatVersion = document.Metadata.SchemaVersion,
            FileSizeBytes = options.SourceFileSizeBytes,
            SourceApplication = "NEXUS3D.Formats.VoxelJson"
        };

        scene.Metadata.Formats.Add(new FormatMetadata("voxel:json", document.Metadata.SchemaVersion));
        scene.Metadata.ProvenanceHistory.Add(new DataProvenance(
            DataOriginKind.Imported,
            "NEXUS3D.Formats.VoxelJson",
            "VoxelJsonToSceneConverter.Convert",
            $"Imported {document.Voxels.Count} voxels from dialect '{document.Dialect}'."));

        var materialGuidByStringId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        int preservedExtensions = document.RootExtensionData.Count +
                                  document.Metadata.ExtensionData.Count +
                                  document.Grid.ExtensionData.Count;

        for (int i = 0; i < document.Materials.Count; i++)
        {
            VoxelMaterialReference srcMat = document.Materials[i];
            string matName = !string.IsNullOrWhiteSpace(srcMat.Name)
                ? srcMat.Name
                : (!string.IsNullOrWhiteSpace(srcMat.Id) ? srcMat.Id : $"VoxelMaterial_{i}");

            var usmMat = new MaterialData(matName)
            {
                BaseColor = srcMat.Color is { } c
                    ? new MaterialColor(c.R, c.G, c.B, c.A)
                    : MaterialColor.White,
                Metallic = Math.Clamp(srcMat.Metallic, 0.0, 1.0),
                Roughness = Math.Clamp(srcMat.Roughness, 0.0, 1.0),
                Opacity = Math.Clamp(srcMat.Opacity, 0.0, 1.0),
                IsPbrMetallicRoughnessSpecified = srcMat.Metallic > 0.0 || Math.Abs(srcMat.Roughness - 0.5) > 1e-6,
                TransparencyMode = srcMat.Opacity < 1.0 || (srcMat.Color?.A ?? 1.0) < 1.0
                    ? TransparencyMode.AlphaBlend
                    : TransparencyMode.Opaque
            };

            string matIdKey = !string.IsNullOrWhiteSpace(srcMat.Id) ? srcMat.Id : matName;
            usmMat.CustomProperties["voxel.materialId"] = JsonSerializer.SerializeToElement(matIdKey);
            usmMat.CustomProperties["voxel.orderIndex"] = JsonSerializer.SerializeToElement(i);
            if (srcMat.Color.HasValue)
            {
                usmMat.CustomProperties["voxel.hasExplicitColor"] = JsonSerializer.SerializeToElement(true);
            }

            if (srcMat.Emissive != 0.0)
            {
                usmMat.CustomProperties["voxel.emissive"] = JsonSerializer.SerializeToElement(srcMat.Emissive);
            }

            if (!string.IsNullOrWhiteSpace(srcMat.TextureReference))
            {
                usmMat.CustomProperties["voxel.textureReference"] = JsonSerializer.SerializeToElement(srcMat.TextureReference);
            }

            if (options.PreserveRawData && !srcMat.ExtensionData.IsEmpty)
            {
                preservedExtensions += srcMat.ExtensionData.Count;
                usmMat.CustomProperties["voxel.extensionData"] = VoxelDataPreserver.SerializeExtensionData(srcMat.ExtensionData);
            }

            scene.Materials.Add(usmMat);
            materialGuidByStringId[matIdKey] = usmMat.Id;
        }

        VoxelSize gridVoxelSize = document.Grid.HasValidVoxelSize
            ? new VoxelSize(document.Grid.VoxelSize.X, document.Grid.VoxelSize.Y, document.Grid.VoxelSize.Z)
            : VoxelSize.UnitCube;

        var gridOrigin = new VoxelPosition(
            (int)Math.Round(document.Grid.Origin.X),
            (int)Math.Round(document.Grid.Origin.Y),
            (int)Math.Round(document.Grid.Origin.Z));

        var primaryGrid = new VoxelGrid(sceneName)
        {
            Origin = gridOrigin,
            VoxelSize = gridVoxelSize
        };

        var layerById = new Dictionary<string, VoxelLayer>(StringComparer.OrdinalIgnoreCase);
        foreach (VoxelLayerDefinition srcLayer in document.Layers)
        {
            preservedExtensions += srcLayer.ExtensionData.Count;
            string layerName = !string.IsNullOrWhiteSpace(srcLayer.Name) ? srcLayer.Name : srcLayer.Id;
            var usmLayer = new VoxelLayer(string.IsNullOrWhiteSpace(layerName) ? "Layer" : layerName)
            {
                IsVisible = srcLayer.IsVisible,
                IsLocked = srcLayer.IsLocked,
                Opacity = Math.Clamp(srcLayer.Opacity, 0.0, 1.0)
            };
            primaryGrid.Layers.Add(usmLayer);
            if (!string.IsNullOrWhiteSpace(srcLayer.Id))
            {
                layerById[srcLayer.Id] = usmLayer;
            }
        }

        var grids = new List<VoxelGrid> { primaryGrid };

        for (int i = 0; i < document.Voxels.Count; i++)
        {
            VoxelRecord record = document.Voxels[i];
            preservedExtensions += record.ExtensionData.Count;
            foreach (var face in record.Faces.Values)
            {
                preservedExtensions += face.ExtensionData.Count;
            }

            var discretePos = new VoxelPosition(
                (int)Math.Round(record.Position.X),
                (int)Math.Round(record.Position.Y),
                (int)Math.Round(record.Position.Z));

            BlockState state = BuildBlockState(record);
            Guid? matGuid = null;
            if (!string.IsNullOrWhiteSpace(record.MaterialId) &&
                materialGuidByStringId.TryGetValue(record.MaterialId, out Guid resolvedGuid))
            {
                matGuid = resolvedGuid;
            }

            BlockEntityData blockEntity = VoxelDataPreserver.CreateVoxelRecordBlockEntity(
                record,
                i,
                options.PreserveRawData);

            // Find a grid where discretePos is not yet occupied (supports sub-grid/cuboid elements sharing rounded integer cells)
            VoxelGrid? targetGrid = null;
            for (int g = 0; g < grids.Count; g++)
            {
                if (!grids[g].TryGetVoxel(discretePos, out _))
                {
                    targetGrid = grids[g];
                    break;
                }
            }

            if (targetGrid is null)
            {
                targetGrid = new VoxelGrid($"{sceneName}_SubGrid_{grids.Count}")
                {
                    Origin = gridOrigin,
                    VoxelSize = gridVoxelSize
                };
                grids.Add(targetGrid);
            }

            int paletteIdx = targetGrid.Palette.GetOrAdd(state, matGuid);
            var usmVoxel = new Voxel(discretePos, paletteIdx, state, matGuid, blockEntity);
            targetGrid.SetVoxel(usmVoxel);

            if (!string.IsNullOrWhiteSpace(record.LayerId) &&
                layerById.TryGetValue(record.LayerId, out VoxelLayer? targetLayer))
            {
                targetLayer.Positions.Add(discretePos);
            }
        }

        var sceneObj = new SceneObject(sceneName);
        foreach (VoxelGrid grid in grids)
        {
            grid.RecalculateBounds();
            sceneObj.VoxelGrids.Add(grid);
        }

        VoxelDataPreserver.AttachDocumentMetadata(scene, sceneObj, document, options.PreserveRawData);

        var voxelNode = new SceneNode(sceneName, sceneObj);
        scene.Graph.AddNode(scene.Graph.Root, voxelNode);

        lossReport.RecordSupported("VoxelGrid", $"Converted {document.Voxels.Count} voxels into USM VoxelGrid.");
        if (document.Materials.Count > 0)
        {
            lossReport.RecordSupported("Materials", $"Converted {document.Materials.Count} voxel materials.");
        }

        if (document.Layers.Count > 0)
        {
            lossReport.RecordSupported("Layers", $"Converted {document.Layers.Count} voxel layers.");
        }

        if (preservedExtensions > 0 && options.PreserveRawData)
        {
            lossReport.RecordPreservedRaw(
                "JsonExtensions",
                $"Preserved {preservedExtensions} custom/unknown JSON extension properties in USM.");
        }

        scene.Metadata.LossReport = lossReport;
        scene.Metadata.Warnings.AddRange(warnings);

        var report = new VoxelConversionReport
        {
            Dialect = document.Dialect,
            VoxelCount = document.Voxels.Count,
            MaterialCount = document.Materials.Count,
            LayerCount = document.Layers.Count,
            PreservedExtensionCount = preservedExtensions,
            LossReport = lossReport,
            Warnings = warnings,
            Issues = issues
        };

        return (scene, report);
    }

    private static BlockState BuildBlockState(VoxelRecord record)
    {
        string rawType = !string.IsNullOrWhiteSpace(record.VoxelType)
            ? record.VoxelType!
            : "voxel:solid";

        string ns = "voxel";
        string name = rawType;
        int colonIdx = rawType.IndexOf(':', StringComparison.Ordinal);
        if (colonIdx > 0 && colonIdx < rawType.Length - 1)
        {
            ns = rawType[..colonIdx];
            name = rawType[(colonIdx + 1)..];
        }

        var props = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(record.MaterialId))
        {
            props["material"] = record.MaterialId!;
        }

        if (record.Color.HasValue)
        {
            props["color"] = record.Color.Value.ToHexString(includeAlpha: true);
        }

        if (!string.IsNullOrWhiteSpace(record.LayerId))
        {
            props["layer"] = record.LayerId!;
        }

        return new BlockState(ns, name, props);
    }
}
