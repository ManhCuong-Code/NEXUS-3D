using System.Globalization;
using System.Text;
using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;
using NEXUS3D.Formats.VoxelJson.Validation;

namespace NEXUS3D.Formats.VoxelJson.Exporting;

/// <summary>
/// Serializes a <see cref="VoxelDocument"/> to UTF-8 JSON across all supported Voxel JSON dialects,
/// supporting atomic file writes and post-write verification.
/// </summary>
public sealed class VoxelJsonExporter
{
    private readonly VoxelJsonValidator _validator = new();

    public string ExportToString(VoxelDocument document, VoxelJsonWriteOptions? options = null)
    {
        byte[] bytes = ExportToUtf8Bytes(document, options);
        return Encoding.UTF8.GetString(bytes);
    }

    public byte[] ExportToUtf8Bytes(VoxelDocument document, VoxelJsonWriteOptions? options = null)
    {
        using var ms = new MemoryStream();
        ExportToStream(document, ms, options);
        return ms.ToArray();
    }

    public void ExportToFile(VoxelDocument document, string filePath, VoxelJsonWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        options ??= VoxelJsonWriteOptions.Default;

        if (File.Exists(filePath) && !options.AllowOverwrite)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.ExportFailed,
                $"Target file '{filePath}' already exists and AllowOverwrite is false.",
                filePath);
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = $"{filePath}.tmp.{Guid.NewGuid():N}";
        try
        {
            byte[] utf8Bytes = ExportToUtf8Bytes(document, options);
            File.WriteAllBytes(tempPath, utf8Bytes);

            if (options.VerifyAfterWrite)
            {
                var parser = new VoxelJsonParser();
                VoxelJsonParseResult verifyResult = parser.ParseFile(tempPath, new VoxelJsonReadOptions
                {
                    StrictMode = false,
                    DuplicatePolicy = DuplicateVoxelPolicy.KeepAll
                });

                if (verifyResult.HasErrors)
                {
                    JsonFormatDiagnostic firstErr = verifyResult.Diagnostics.First(d => d.IsError);
                    throw new JsonFormatException(
                        JsonFormatErrorCodes.ExportFailed,
                        $"Post-write verification failed for '{filePath}': {firstErr.Message}",
                        firstErr.JsonPath);
                }
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* Best-effort cleanup */ }
            }
        }
    }

    public void ExportToStream(VoxelDocument document, Stream destination, VoxelJsonWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        options ??= VoxelJsonWriteOptions.Default;

        IReadOnlyList<JsonFormatDiagnostic> diagnostics = _validator.Validate(document, new VoxelJsonReadOptions
        {
            StrictMode = false,
            DuplicatePolicy = DuplicateVoxelPolicy.KeepAll
        });

        JsonFormatDiagnostic? firstError = diagnostics.FirstOrDefault(d => d.IsError);
        if (firstError is not null)
        {
            throw new JsonFormatException(
                firstError.Code,
                $"Cannot export invalid VoxelDocument: {firstError.Message}",
                firstError.JsonPath);
        }

        VoxelJsonDialect dialect = options.TargetDialect ?? document.Dialect;
        if (dialect == VoxelJsonDialect.Unknown)
        {
            dialect = VoxelJsonDialect.NexusVoxelGridV1;
        }

        var writerOptions = new JsonWriterOptions
        {
            Indented = options.PrettyPrint
        };

        using var writer = new Utf8JsonWriter(destination, writerOptions);
        IReadOnlyList<VoxelRecord> voxelsToWrite = options.PreserveOrder
            ? document.Voxels
            : document.Voxels
                .OrderBy(v => v.LayerId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(v => v.Position.Y)
                .ThenBy(v => v.Position.Z)
                .ThenBy(v => v.Position.X)
                .ToList();

        switch (dialect)
        {
            case VoxelJsonDialect.NexusVoxelGridV1:
                WriteNexusVoxelGridV1(writer, document, voxelsToWrite, options);
                break;
            case VoxelJsonDialect.SparseVoxelArray:
                WriteSparseVoxelArray(writer, document, voxelsToWrite, options);
                break;
            case VoxelJsonDialect.MinecraftBlockModelCuboids:
                WriteMinecraftBlockModelCuboids(writer, document, voxelsToWrite, options);
                break;
            default:
                WriteNexusVoxelGridV1(writer, document, voxelsToWrite, options);
                break;
        }

        writer.Flush();
    }

    private static void WriteNexusVoxelGridV1(
        Utf8JsonWriter writer,
        VoxelDocument document,
        IReadOnlyList<VoxelRecord> voxels,
        VoxelJsonWriteOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("format", "nexus:voxel_grid_v1");

        if (options.PreserveMetadata)
        {
            writer.WritePropertyName("metadata");
            writer.WriteStartObject();
            writer.WriteString("name", document.Metadata.Name);
            if (!string.IsNullOrWhiteSpace(document.Metadata.Author))
            {
                writer.WriteString("author", document.Metadata.Author);
            }

            if (!string.IsNullOrWhiteSpace(document.Metadata.Description))
            {
                writer.WriteString("description", document.Metadata.Description);
            }

            writer.WriteString("schemaVersion", document.Metadata.SchemaVersion);

            if (!string.IsNullOrWhiteSpace(document.Metadata.ParentModel))
            {
                writer.WriteString("parent", document.Metadata.ParentModel);
            }

            if (document.Metadata.AmbientOcclusion.HasValue)
            {
                writer.WriteBoolean("ambientOcclusion", document.Metadata.AmbientOcclusion.Value);
            }

            if (document.Metadata.Tags.Count > 0)
            {
                writer.WritePropertyName("tags");
                writer.WriteStartArray();
                foreach (string tag in document.Metadata.Tags)
                {
                    writer.WriteStringValue(tag);
                }

                writer.WriteEndArray();
            }

            if (document.Metadata.CustomMetadata.Count > 0)
            {
                writer.WritePropertyName("customMetadata");
                writer.WriteStartObject();
                foreach ((string k, string v) in document.Metadata.CustomMetadata)
                {
                    writer.WriteString(k, v);
                }

                writer.WriteEndObject();
            }

            if (options.WriteExtensionData && !document.Metadata.ExtensionData.IsEmpty)
            {
                document.Metadata.ExtensionData.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        writer.WritePropertyName("grid");
        writer.WriteStartObject();
        if (document.Grid.Dimensions.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "dimensions", document.Grid.Dimensions.Value, options);
        }

        JsonParsingUtilities.WriteVector3DArray(writer, "origin", document.Grid.Origin, options);
        JsonParsingUtilities.WriteVector3DArray(writer, "voxelSize", document.Grid.VoxelSize, options);
        JsonParsingUtilities.WriteVector2DArray(writer, "textureSize", document.Grid.TextureSize, options);

        if (options.WriteExtensionData && !document.Grid.ExtensionData.IsEmpty)
        {
            document.Grid.ExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();

        if (document.Materials.Count > 0)
        {
            writer.WritePropertyName("materials");
            writer.WriteStartArray();
            foreach (VoxelMaterialReference mat in document.Materials)
            {
                WriteMaterialObject(writer, mat, options);
            }

            writer.WriteEndArray();
        }

        if (document.Layers.Count > 0)
        {
            writer.WritePropertyName("layers");
            writer.WriteStartArray();
            foreach (VoxelLayerDefinition layer in document.Layers)
            {
                WriteLayerObject(writer, layer, options);
            }

            writer.WriteEndArray();
        }

        writer.WritePropertyName("voxels");
        writer.WriteStartArray();
        foreach (VoxelRecord voxel in voxels)
        {
            WriteVoxelRecordObject(writer, voxel, options, usePositionArray: true);
        }

        writer.WriteEndArray();

        if (options.WriteExtensionData && !document.RootExtensionData.IsEmpty)
        {
            document.RootExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteSparseVoxelArray(
        Utf8JsonWriter writer,
        VoxelDocument document,
        IReadOnlyList<VoxelRecord> voxels,
        VoxelJsonWriteOptions options)
    {
        writer.WriteStartObject();

        if (options.PreserveMetadata)
        {
            writer.WriteString("name", document.Metadata.Name);
            if (!string.IsNullOrWhiteSpace(document.Metadata.Author))
            {
                writer.WriteString("author", document.Metadata.Author);
            }

            if (!string.IsNullOrWhiteSpace(document.Metadata.Description))
            {
                writer.WriteString("description", document.Metadata.Description);
            }

            writer.WriteString("version", document.Metadata.SchemaVersion);
        }

        if (document.Grid.Dimensions.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "dimensions", document.Grid.Dimensions.Value, options);
        }

        if (document.Grid.Origin != Vector3D.Zero)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "origin", document.Grid.Origin, options);
        }

        if (document.Grid.VoxelSize != Vector3D.One)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "voxelSize", document.Grid.VoxelSize, options);
        }

        if (document.Materials.Count > 0)
        {
            writer.WritePropertyName("materials");
            writer.WriteStartArray();
            foreach (VoxelMaterialReference mat in document.Materials)
            {
                WriteMaterialObject(writer, mat, options);
            }

            writer.WriteEndArray();
        }

        if (document.Layers.Count > 0)
        {
            writer.WritePropertyName("layers");
            writer.WriteStartArray();
            foreach (VoxelLayerDefinition layer in document.Layers)
            {
                WriteLayerObject(writer, layer, options);
            }

            writer.WriteEndArray();
        }

        writer.WritePropertyName("voxels");
        writer.WriteStartArray();
        foreach (VoxelRecord voxel in voxels)
        {
            bool canWriteCompact = options.WriteCompactVoxelTuples &&
                                   !voxel.Size.HasValue &&
                                   !voxel.Rotation.HasValue &&
                                   string.IsNullOrWhiteSpace(voxel.VoxelType) &&
                                   string.IsNullOrWhiteSpace(voxel.LayerId) &&
                                   voxel.Faces.Count == 0 &&
                                   voxel.CustomProperties.Count == 0 &&
                                   voxel.ExtensionData.IsEmpty;

            if (canWriteCompact)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(options.RoundCoordinate(voxel.Position.X));
                writer.WriteNumberValue(options.RoundCoordinate(voxel.Position.Y));
                writer.WriteNumberValue(options.RoundCoordinate(voxel.Position.Z));
                if (voxel.Color.HasValue)
                {
                    writer.WriteStringValue(voxel.Color.Value.ToHexString());
                }
                else if (!string.IsNullOrWhiteSpace(voxel.MaterialId))
                {
                    writer.WriteStringValue(voxel.MaterialId);
                }

                writer.WriteEndArray();
            }
            else
            {
                WriteVoxelRecordObject(writer, voxel, options, usePositionArray: false);
            }
        }

        writer.WriteEndArray();

        if (options.WriteExtensionData && !document.RootExtensionData.IsEmpty)
        {
            document.RootExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteMinecraftBlockModelCuboids(
        Utf8JsonWriter writer,
        VoxelDocument document,
        IReadOnlyList<VoxelRecord> voxels,
        VoxelJsonWriteOptions options)
    {
        writer.WriteStartObject();

        if (options.PreserveMetadata)
        {
            if (!string.IsNullOrWhiteSpace(document.Metadata.Author))
            {
                writer.WriteString("credit", document.Metadata.Author);
            }

            if (!string.IsNullOrWhiteSpace(document.Metadata.ParentModel))
            {
                writer.WriteString("parent", document.Metadata.ParentModel);
            }

            if (document.Metadata.AmbientOcclusion.HasValue)
            {
                writer.WriteBoolean("ambientocclusion", document.Metadata.AmbientOcclusion.Value);
            }
        }

        JsonParsingUtilities.WriteVector2DArray(writer, "texture_size", document.Grid.TextureSize, options);

        if (document.Materials.Count > 0)
        {
            writer.WritePropertyName("textures");
            writer.WriteStartObject();
            foreach (VoxelMaterialReference mat in document.Materials)
            {
                string value = !string.IsNullOrWhiteSpace(mat.TextureReference)
                    ? mat.TextureReference!
                    : (mat.Color?.ToHexString() ?? mat.Name);
                writer.WriteString(mat.Id, value);
            }

            writer.WriteEndObject();
        }

        writer.WritePropertyName("elements");
        writer.WriteStartArray();
        foreach (VoxelRecord voxel in voxels)
        {
            writer.WriteStartObject();
            if (!string.IsNullOrWhiteSpace(voxel.Name))
            {
                writer.WriteString("name", voxel.Name);
            }

            Vector3D from = voxel.Position;
            Vector3D size = voxel.GetEffectiveSize(document.Grid.VoxelSize);
            Vector3D to = from + size;

            if (voxel.CustomProperties.TryGetValue("cuboid.rawTo", out string? rawToStr))
            {
                string[] parts = rawToStr.Split(',');
                if (parts.Length == 3 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double tx) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double ty) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double tz))
                {
                    to = new Vector3D(tx, ty, tz);
                }
            }

            JsonParsingUtilities.WriteVector3DArray(writer, "from", from, options);
            JsonParsingUtilities.WriteVector3DArray(writer, "to", to, options);

            if (voxel.Rotation.HasValue || voxel.RotationOrigin.HasValue || !string.IsNullOrWhiteSpace(voxel.RotationAxis))
            {
                writer.WritePropertyName("rotation");
                writer.WriteStartObject();
                Vector3D rotOrigin = voxel.RotationOrigin ?? from;
                string axis = !string.IsNullOrWhiteSpace(voxel.RotationAxis) ? voxel.RotationAxis! : "y";
                Vector3D rotVec = voxel.Rotation ?? Vector3D.Zero;
                double angle = axis switch
                {
                    "x" => rotVec.X,
                    "z" => rotVec.Z,
                    _ => rotVec.Y
                };

                writer.WriteNumber("angle", options.RoundCoordinate(angle));
                writer.WriteString("axis", axis);
                JsonParsingUtilities.WriteVector3DArray(writer, "origin", rotOrigin, options);
                if (voxel.Rescale.HasValue)
                {
                    writer.WriteBoolean("rescale", voxel.Rescale.Value);
                }

                writer.WriteEndObject();
            }

            if (voxel.Shade.HasValue)
            {
                writer.WriteBoolean("shade", voxel.Shade.Value);
            }

            if (voxel.CustomProperties.TryGetValue("cuboid.markerColor", out string? markerColorStr) &&
                int.TryParse(markerColorStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int markerColor))
            {
                writer.WriteNumber("color", markerColor);
            }
            else if (voxel.Color.HasValue)
            {
                writer.WriteString("color", voxel.Color.Value.ToHexString());
            }

            if (voxel.Faces.Count > 0)
            {
                writer.WritePropertyName("faces");
                writer.WriteStartObject();
                foreach ((string faceKey, VoxelCuboidFaceUv faceUv) in voxel.Faces)
                {
                    writer.WritePropertyName(faceKey);
                    writer.WriteStartObject();
                    if (faceUv.Uv is { Length: 4 })
                    {
                        writer.WritePropertyName("uv");
                        writer.WriteStartArray();
                        foreach (double u in faceUv.Uv)
                        {
                            writer.WriteNumberValue(options.RoundCoordinate(u));
                        }

                        writer.WriteEndArray();
                    }

                    writer.WriteString("texture", faceUv.Texture);
                    if (!string.IsNullOrWhiteSpace(faceUv.CullFace))
                    {
                        writer.WriteString("cullface", faceUv.CullFace);
                    }

                    if (faceUv.Rotation.HasValue)
                    {
                        writer.WriteNumber("rotation", faceUv.Rotation.Value);
                    }

                    if (faceUv.TintIndex.HasValue)
                    {
                        writer.WriteNumber("tintindex", faceUv.TintIndex.Value);
                    }

                    if (options.WriteExtensionData && !faceUv.ExtensionData.IsEmpty)
                    {
                        faceUv.ExtensionData.WriteTo(writer);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            if (options.WriteExtensionData && !voxel.ExtensionData.IsEmpty)
            {
                voxel.ExtensionData.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        if (document.Layers.Count > 0)
        {
            writer.WritePropertyName("groups");
            writer.WriteStartArray();
            foreach (VoxelLayerDefinition layer in document.Layers.Where(l => string.IsNullOrEmpty(l.ParentLayerId)))
            {
                WriteCuboidGroupObject(writer, layer, document.Layers, options);
            }

            writer.WriteEndArray();
        }

        if (options.WriteExtensionData && !document.RootExtensionData.IsEmpty)
        {
            document.RootExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteCuboidGroupObject(
        Utf8JsonWriter writer,
        VoxelLayerDefinition layer,
        List<VoxelLayerDefinition> allLayers,
        VoxelJsonWriteOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", layer.Name);
        if (layer.Pivot.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "origin", layer.Pivot.Value, options);
        }

        if (layer.Rotation.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "rotation", layer.Rotation.Value, options);
        }

        writer.WriteBoolean("visibility", layer.IsVisible);

        writer.WritePropertyName("children");
        writer.WriteStartArray();
        foreach (int elemIdx in layer.ElementIndices)
        {
            writer.WriteNumberValue(elemIdx);
        }

        foreach (VoxelLayerDefinition childLayer in allLayers.Where(l => l.ParentLayerId == layer.Id))
        {
            WriteCuboidGroupObject(writer, childLayer, allLayers, options);
        }

        writer.WriteEndArray();

        if (options.WriteExtensionData && !layer.ExtensionData.IsEmpty)
        {
            layer.ExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteMaterialObject(Utf8JsonWriter writer, VoxelMaterialReference mat, VoxelJsonWriteOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", mat.Id);
        writer.WriteString("name", mat.Name);
        if (mat.Color.HasValue)
        {
            writer.WriteString("color", mat.Color.Value.ToHexString());
        }

        if (mat.Metallic != 0.0)
        {
            writer.WriteNumber("metallic", options.RoundCoordinate(mat.Metallic));
        }

        if (Math.Abs(mat.Roughness - 0.5) > 1e-9)
        {
            writer.WriteNumber("roughness", options.RoundCoordinate(mat.Roughness));
        }

        if (Math.Abs(mat.Opacity - 1.0) > 1e-9)
        {
            writer.WriteNumber("opacity", options.RoundCoordinate(mat.Opacity));
        }

        if (mat.Emissive != 0.0)
        {
            writer.WriteNumber("emissive", options.RoundCoordinate(mat.Emissive));
        }

        if (!string.IsNullOrWhiteSpace(mat.TextureReference))
        {
            writer.WriteString("texture", mat.TextureReference);
        }

        if (options.WriteExtensionData && !mat.ExtensionData.IsEmpty)
        {
            mat.ExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteLayerObject(Utf8JsonWriter writer, VoxelLayerDefinition layer, VoxelJsonWriteOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", layer.Id);
        writer.WriteString("name", layer.Name);
        if (!string.IsNullOrWhiteSpace(layer.ParentLayerId))
        {
            writer.WriteString("parentLayerId", layer.ParentLayerId);
        }

        writer.WriteBoolean("visible", layer.IsVisible);
        writer.WriteBoolean("locked", layer.IsLocked);
        if (Math.Abs(layer.Opacity - 1.0) > 1e-9)
        {
            writer.WriteNumber("opacity", options.RoundCoordinate(layer.Opacity));
        }

        if (layer.Pivot.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "pivot", layer.Pivot.Value, options);
        }

        if (layer.Rotation.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "rotation", layer.Rotation.Value, options);
        }

        if (options.WriteExtensionData && !layer.ExtensionData.IsEmpty)
        {
            layer.ExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteVoxelRecordObject(
        Utf8JsonWriter writer,
        VoxelRecord voxel,
        VoxelJsonWriteOptions options,
        bool usePositionArray)
    {
        writer.WriteStartObject();

        if (!string.IsNullOrWhiteSpace(voxel.Name))
        {
            writer.WriteString("name", voxel.Name);
        }

        if (usePositionArray)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "position", voxel.Position, options);
        }
        else
        {
            writer.WriteNumber("x", options.RoundCoordinate(voxel.Position.X));
            writer.WriteNumber("y", options.RoundCoordinate(voxel.Position.Y));
            writer.WriteNumber("z", options.RoundCoordinate(voxel.Position.Z));
        }

        if (voxel.Size.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "size", voxel.Size.Value, options);
        }

        if (voxel.Rotation.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "rotation", voxel.Rotation.Value, options);
        }

        if (voxel.RotationOrigin.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "rotationOrigin", voxel.RotationOrigin.Value, options);
        }

        if (!string.IsNullOrWhiteSpace(voxel.RotationAxis))
        {
            writer.WriteString("rotationAxis", voxel.RotationAxis);
        }

        if (voxel.Rescale.HasValue)
        {
            writer.WriteBoolean("rescale", voxel.Rescale.Value);
        }

        if (voxel.Shade.HasValue)
        {
            writer.WriteBoolean("shade", voxel.Shade.Value);
        }

        if (voxel.Color.HasValue)
        {
            writer.WriteString("color", voxel.Color.Value.ToHexString());
        }

        if (!string.IsNullOrWhiteSpace(voxel.MaterialId))
        {
            writer.WriteString("materialId", voxel.MaterialId);
        }

        if (!string.IsNullOrWhiteSpace(voxel.VoxelType))
        {
            writer.WriteString("voxelType", voxel.VoxelType);
        }

        if (!string.IsNullOrWhiteSpace(voxel.LayerId))
        {
            writer.WriteString("layerId", voxel.LayerId);
        }

        if (voxel.Faces.Count > 0)
        {
            writer.WritePropertyName("faces");
            writer.WriteStartObject();
            foreach ((string faceKey, VoxelCuboidFaceUv faceUv) in voxel.Faces)
            {
                writer.WritePropertyName(faceKey);
                writer.WriteStartObject();
                if (faceUv.Uv is { Length: 4 })
                {
                    writer.WritePropertyName("uv");
                    writer.WriteStartArray();
                    foreach (double u in faceUv.Uv)
                    {
                        writer.WriteNumberValue(options.RoundCoordinate(u));
                    }

                    writer.WriteEndArray();
                }

                writer.WriteString("texture", faceUv.Texture);
                if (!string.IsNullOrWhiteSpace(faceUv.CullFace))
                {
                    writer.WriteString("cullface", faceUv.CullFace);
                }

                if (faceUv.Rotation.HasValue)
                {
                    writer.WriteNumber("rotation", faceUv.Rotation.Value);
                }

                if (faceUv.TintIndex.HasValue)
                {
                    writer.WriteNumber("tintindex", faceUv.TintIndex.Value);
                }

                if (options.WriteExtensionData && !faceUv.ExtensionData.IsEmpty)
                {
                    faceUv.ExtensionData.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        if (voxel.CustomProperties.Count > 0)
        {
            writer.WritePropertyName("customProperties");
            writer.WriteStartObject();
            foreach ((string k, string v) in voxel.CustomProperties)
            {
                writer.WriteString(k, v);
            }

            writer.WriteEndObject();
        }

        if (options.WriteExtensionData && !voxel.ExtensionData.IsEmpty)
        {
            voxel.ExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
