using System.Text.Json;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Exporting;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;

namespace NEXUS3D.Formats.VoxelJson.Conversion;

/// <summary>
/// Preserves dialect-specific Voxel JSON attributes (sub-grid coordinates, per-voxel cuboid rotations,
/// per-face UV maps, and unknown JSON extension properties) inside USM <see cref="ExtensionData"/> and
/// <see cref="BlockEntityData"/> without data loss.
/// </summary>
public static class VoxelDataPreserver
{
    public const string ExtensionNamespace = "nexus:voxel_json";
    public const string BlockEntityTypeId = "nexus:voxel_record_meta";

    public static void AttachDocumentMetadata(
        Scene3D scene,
        SceneObject sceneObject,
        VoxelDocument document,
        bool preserveRawData)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(sceneObject);
        ArgumentNullException.ThrowIfNull(document);

        var ext = new ExtensionData(ExtensionNamespace);
        ext.Values["dialect"] = JsonSerializer.SerializeToElement(document.Dialect.ToString());
        ext.Values["schemaVersion"] = JsonSerializer.SerializeToElement(document.Metadata.SchemaVersion);

        if (!string.IsNullOrWhiteSpace(document.Metadata.ParentModel))
        {
            ext.Values["parentModel"] = JsonSerializer.SerializeToElement(document.Metadata.ParentModel);
        }

        if (document.Metadata.AmbientOcclusion.HasValue)
        {
            ext.Values["ambientOcclusion"] = JsonSerializer.SerializeToElement(document.Metadata.AmbientOcclusion.Value);
        }

        if (document.Grid.Dimensions.HasValue)
        {
            var dim = document.Grid.Dimensions.Value;
            ext.Values["gridDimensions"] = JsonSerializer.SerializeToElement(new[] { dim.X, dim.Y, dim.Z });
        }

        var origin = document.Grid.Origin;
        ext.Values["gridOrigin"] = JsonSerializer.SerializeToElement(new[] { origin.X, origin.Y, origin.Z });

        var texSize = document.Grid.TextureSize;
        ext.Values["textureSize"] = JsonSerializer.SerializeToElement(new[] { texSize.X, texSize.Y });

        if (document.Metadata.CustomMetadata.Count > 0)
        {
            ext.Values["customMetadata"] = JsonSerializer.SerializeToElement(document.Metadata.CustomMetadata);
        }

        if (preserveRawData)
        {
            if (!document.RootExtensionData.IsEmpty)
            {
                ext.Values["rootExtensionData"] = SerializeExtensionData(document.RootExtensionData);
            }

            if (!document.Metadata.ExtensionData.IsEmpty)
            {
                ext.Values["metadataExtensionData"] = SerializeExtensionData(document.Metadata.ExtensionData);
            }

            if (!document.Grid.ExtensionData.IsEmpty)
            {
                ext.Values["gridExtensionData"] = SerializeExtensionData(document.Grid.ExtensionData);
            }

            if (document.Layers.Count > 0)
            {
                var exporter = new VoxelJsonExporter();
                string snapshotJson = exporter.ExportToString(document, new VoxelJsonWriteOptions
                {
                    PrettyPrint = false,
                    TargetDialect = VoxelJsonDialect.NexusVoxelGridV1,
                    VerifyAfterWrite = false
                });
                using JsonDocument snapDoc = JsonDocument.Parse(snapshotJson);
                if (snapDoc.RootElement.TryGetProperty("layers", out JsonElement layersElem))
                {
                    ext.Values["layersSnapshot"] = layersElem.Clone();
                }
            }
        }

        scene.Metadata.AddExtension(ext);
        sceneObject.Extensions[ExtensionNamespace] = ext.DeepClone();
    }

    public static BlockEntityData CreateVoxelRecordBlockEntity(
        VoxelRecord record,
        int originalOrderIndex,
        bool preserveRawData)
    {
        var entity = new BlockEntityData(BlockEntityTypeId);
        entity.Properties["orderIndex"] = JsonSerializer.SerializeToElement(originalOrderIndex);
        entity.Properties["exactPosition"] = JsonSerializer.SerializeToElement(new[]
        {
            record.Position.X,
            record.Position.Y,
            record.Position.Z
        });

        if (record.Size.HasValue)
        {
            var s = record.Size.Value;
            entity.Properties["size"] = JsonSerializer.SerializeToElement(new[] { s.X, s.Y, s.Z });
        }

        if (record.Rotation.HasValue)
        {
            var r = record.Rotation.Value;
            entity.Properties["rotation"] = JsonSerializer.SerializeToElement(new[] { r.X, r.Y, r.Z });
        }

        if (record.RotationOrigin.HasValue)
        {
            var ro = record.RotationOrigin.Value;
            entity.Properties["rotationOrigin"] = JsonSerializer.SerializeToElement(new[] { ro.X, ro.Y, ro.Z });
        }

        if (!string.IsNullOrWhiteSpace(record.RotationAxis))
        {
            entity.Properties["rotationAxis"] = JsonSerializer.SerializeToElement(record.RotationAxis);
        }

        if (record.Rescale.HasValue)
        {
            entity.Properties["rescale"] = JsonSerializer.SerializeToElement(record.Rescale.Value);
        }

        if (record.Shade.HasValue)
        {
            entity.Properties["shade"] = JsonSerializer.SerializeToElement(record.Shade.Value);
        }

        if (record.Color.HasValue)
        {
            entity.Properties["color"] = JsonSerializer.SerializeToElement(record.Color.Value.ToHexString(includeAlpha: true));
        }

        if (!string.IsNullOrWhiteSpace(record.MaterialId))
        {
            entity.Properties["materialId"] = JsonSerializer.SerializeToElement(record.MaterialId);
        }

        if (!string.IsNullOrWhiteSpace(record.VoxelType))
        {
            entity.Properties["voxelType"] = JsonSerializer.SerializeToElement(record.VoxelType);
        }

        if (!string.IsNullOrWhiteSpace(record.LayerId))
        {
            entity.Properties["layerId"] = JsonSerializer.SerializeToElement(record.LayerId);
        }

        if (!string.IsNullOrWhiteSpace(record.Name))
        {
            entity.Properties["name"] = JsonSerializer.SerializeToElement(record.Name);
        }

        if (record.CustomProperties.Count > 0)
        {
            entity.Properties["customProperties"] = JsonSerializer.SerializeToElement(record.CustomProperties);
        }

        if (record.Faces.Count > 0)
        {
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach ((string faceKey, VoxelCuboidFaceUv faceUv) in record.Faces)
                {
                    writer.WritePropertyName(faceKey);
                    writer.WriteStartObject();
                    if (faceUv.Uv is { Length: 4 })
                    {
                        writer.WritePropertyName("uv");
                        writer.WriteStartArray();
                        foreach (double u in faceUv.Uv) writer.WriteNumberValue(u);
                        writer.WriteEndArray();
                    }

                    writer.WriteString("texture", faceUv.Texture);
                    if (!string.IsNullOrWhiteSpace(faceUv.CullFace)) writer.WriteString("cullface", faceUv.CullFace);
                    if (faceUv.Rotation.HasValue) writer.WriteNumber("rotation", faceUv.Rotation.Value);
                    if (faceUv.TintIndex.HasValue) writer.WriteNumber("tintindex", faceUv.TintIndex.Value);
                    if (preserveRawData && !faceUv.ExtensionData.IsEmpty)
                    {
                        faceUv.ExtensionData.WriteTo(writer);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            using JsonDocument facesDoc = JsonDocument.Parse(ms.ToArray());
            entity.Properties["faces"] = facesDoc.RootElement.Clone();
        }

        if (preserveRawData && !record.ExtensionData.IsEmpty)
        {
            entity.Properties["extensionData"] = SerializeExtensionData(record.ExtensionData);
        }

        return entity;
    }

    public static JsonElement SerializeExtensionData(JsonExtensionData ext)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            ext.WriteTo(writer);
            writer.WriteEndObject();
        }

        using JsonDocument doc = JsonDocument.Parse(ms.ToArray());
        return doc.RootElement.Clone();
    }

    public static void RestoreExtensionData(JsonElement element, JsonExtensionData target)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty prop in element.EnumerateObject())
        {
            target.Set(prop.Name, prop.Value);
        }
    }
}
