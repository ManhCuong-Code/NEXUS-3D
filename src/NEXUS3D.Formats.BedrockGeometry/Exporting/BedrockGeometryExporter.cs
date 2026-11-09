using System.Text;
using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.BedrockGeometry.Validation;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Exporting;

/// <summary>
/// Serializes a <see cref="BedrockGeometryDocument"/> to UTF-8 JSON across Legacy (<c>1.8.0</c>),
/// Intermediate (<c>1.12.0</c>), and Modern (<c>1.16.0</c>..<c>1.21.0</c>) Bedrock Geometry formats.
/// </summary>
public sealed class BedrockGeometryExporter
{
    private readonly BedrockGeometryValidator _validator = new();

    public string ExportToString(BedrockGeometryDocument document, BedrockGeometryWriteOptions? options = null)
    {
        byte[] bytes = ExportToUtf8Bytes(document, options);
        return Encoding.UTF8.GetString(bytes);
    }

    public byte[] ExportToUtf8Bytes(BedrockGeometryDocument document, BedrockGeometryWriteOptions? options = null)
    {
        using var ms = new MemoryStream();
        ExportToStream(document, ms, options);
        return ms.ToArray();
    }

    public IReadOnlyList<BedrockGeometryDiagnostic> EvaluateCompatibilityWarnings(
        BedrockGeometryDocument document,
        string targetFormatVersion)
    {
        ArgumentNullException.ThrowIfNull(document);
        var warnings = new List<BedrockGeometryDiagnostic>();
        BedrockGeometrySchemaFamily targetFamily = BedrockGeometryDetector.ClassifyVersionString(targetFormatVersion);

        foreach (Models.BedrockGeometry geo in document.Geometries)
        {
            foreach (BedrockBone bone in geo.Bones)
            {
                if (targetFamily != BedrockGeometrySchemaFamily.Modern1160Plus &&
                    !string.IsNullOrWhiteSpace(bone.Binding))
                {
                    warnings.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.UnsupportedSchemaOrDialect,
                        $"Bone '{bone.Name}' uses Molang 'binding', which requires Bedrock format_version 1.16.0+ (target is '{targetFormatVersion}').",
                        IsError: false));
                }

                if (targetFamily == BedrockGeometrySchemaFamily.Legacy180)
                {
                    foreach (BedrockCube cube in bone.Cubes)
                    {
                        if (cube.Rotation.HasValue || cube.Pivot.HasValue)
                        {
                            warnings.Add(new BedrockGeometryDiagnostic(
                                JsonFormatErrorCodes.UnsupportedSchemaOrDialect,
                                $"Bone '{bone.Name}' has a cube with per-cube pivot/rotation, which is not natively supported in legacy Bedrock {targetFormatVersion}.",
                                IsError: false));
                        }

                        if (cube.Uv?.Mode == BedrockUvMode.PerFaceUv)
                        {
                            warnings.Add(new BedrockGeometryDiagnostic(
                                JsonFormatErrorCodes.UnsupportedSchemaOrDialect,
                                $"Bone '{bone.Name}' has a cube with per-face UV, which is not natively supported in legacy Bedrock {targetFormatVersion}.",
                                IsError: false));
                        }
                    }
                }
            }
        }

        return warnings;
    }

    public void ExportToFile(BedrockGeometryDocument document, string filePath, BedrockGeometryWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        options ??= BedrockGeometryWriteOptions.Default;

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
                var parser = new BedrockGeometryParser();
                BedrockGeometryParseResult verifyResult = parser.ParseFile(tempPath, new BedrockGeometryReadOptions
                {
                    StrictMode = false
                });

                if (verifyResult.HasErrors || verifyResult.Document.Geometries.Count != document.Geometries.Count)
                {
                    BedrockGeometryDiagnostic? firstErr = verifyResult.Diagnostics.FirstOrDefault(d => d.IsError);
                    throw new JsonFormatException(
                        JsonFormatErrorCodes.ExportFailed,
                        $"Post-write verification failed for '{filePath}': {firstErr?.Message ?? "Geometry count mismatch."}",
                        firstErr?.JsonPath);
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

    public void ExportToStream(BedrockGeometryDocument document, Stream destination, BedrockGeometryWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        options ??= BedrockGeometryWriteOptions.Default;

        IReadOnlyList<BedrockGeometryDiagnostic> diagnostics = _validator.Validate(document, new BedrockGeometryReadOptions
        {
            StrictMode = false
        });

        BedrockGeometryDiagnostic? firstError = diagnostics.FirstOrDefault(d => d.IsError);
        if (firstError is not null)
        {
            throw new JsonFormatException(
                firstError.Code,
                $"Cannot export invalid BedrockGeometryDocument: {firstError.Message}",
                firstError.JsonPath);
        }

        string targetVersion = !string.IsNullOrWhiteSpace(options.TargetFormatVersion)
            ? options.TargetFormatVersion!
            : (!string.IsNullOrWhiteSpace(document.FormatVersion) ? document.FormatVersion : "1.12.0");

        BedrockGeometrySchemaFamily targetFamily = BedrockGeometryDetector.ClassifyVersionString(targetVersion);
        if (targetFamily == BedrockGeometrySchemaFamily.Unknown)
        {
            targetFamily = document.SchemaFamily != BedrockGeometrySchemaFamily.Unknown
                ? document.SchemaFamily
                : BedrockGeometrySchemaFamily.Intermediate1120;
        }

        var writerOptions = new JsonWriterOptions
        {
            Indented = options.PrettyPrint
        };

        using var writer = new Utf8JsonWriter(destination, writerOptions);

        if (targetFamily == BedrockGeometrySchemaFamily.Legacy180)
        {
            WriteLegacyDocument(writer, document, targetVersion, options);
        }
        else
        {
            WriteModernOrIntermediateDocument(writer, document, targetVersion, options);
        }

        writer.Flush();
    }

    private static void WriteLegacyDocument(
        Utf8JsonWriter writer,
        BedrockGeometryDocument document,
        string targetVersion,
        BedrockGeometryWriteOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("format_version", targetVersion);

        foreach (Models.BedrockGeometry geo in document.Geometries)
        {
            string key = !string.IsNullOrWhiteSpace(geo.ParentGeometryIdentifier)
                ? $"{geo.Description.Identifier}:{geo.ParentGeometryIdentifier}"
                : geo.Description.Identifier;

            writer.WritePropertyName(key);
            writer.WriteStartObject();

            writer.WriteNumber("texturewidth", geo.Description.TextureWidth);
            writer.WriteNumber("textureheight", geo.Description.TextureHeight);

            WriteVisibleBounds(writer, geo.Description, options);

            if (!string.IsNullOrWhiteSpace(geo.Cape))
            {
                writer.WriteString("cape", geo.Cape);
            }

            IReadOnlyList<BedrockBone> bonesToWrite = (options.SortBonesTopologically || !options.PreserveOrder)
                ? geo.GetTopologicalBones()
                : geo.Bones;

            if (bonesToWrite.Count > 0)
            {
                writer.WritePropertyName("bones");
                writer.WriteStartArray();
                foreach (BedrockBone bone in bonesToWrite)
                {
                    WriteBoneObject(writer, bone, options);
                }

                writer.WriteEndArray();
            }

            if (options.WriteExtensionData && !geo.ExtensionData.IsEmpty)
            {
                geo.ExtensionData.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        if (options.WriteExtensionData && !document.RootExtensionData.IsEmpty)
        {
            document.RootExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteModernOrIntermediateDocument(
        Utf8JsonWriter writer,
        BedrockGeometryDocument document,
        string targetVersion,
        BedrockGeometryWriteOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("format_version", targetVersion);

        writer.WritePropertyName("minecraft:geometry");
        writer.WriteStartArray();

        foreach (Models.BedrockGeometry geo in document.Geometries)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("description");
            writer.WriteStartObject();
            writer.WriteString("identifier", geo.Description.Identifier);
            writer.WriteNumber("texture_width", geo.Description.TextureWidth);
            writer.WriteNumber("texture_height", geo.Description.TextureHeight);
            WriteVisibleBounds(writer, geo.Description, options);

            if (options.WriteExtensionData && !geo.Description.ExtensionData.IsEmpty)
            {
                geo.Description.ExtensionData.WriteTo(writer);
            }

            writer.WriteEndObject();

            if (!string.IsNullOrWhiteSpace(geo.Cape))
            {
                writer.WriteString("cape", geo.Cape);
            }

            IReadOnlyList<BedrockBone> bonesToWrite = (options.SortBonesTopologically || !options.PreserveOrder)
                ? geo.GetTopologicalBones()
                : geo.Bones;

            if (bonesToWrite.Count > 0)
            {
                writer.WritePropertyName("bones");
                writer.WriteStartArray();
                foreach (BedrockBone bone in bonesToWrite)
                {
                    WriteBoneObject(writer, bone, options);
                }

                writer.WriteEndArray();
            }

            if (options.WriteExtensionData && !geo.ExtensionData.IsEmpty)
            {
                geo.ExtensionData.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        if (options.WriteExtensionData && !document.RootExtensionData.IsEmpty)
        {
            document.RootExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteVisibleBounds(
        Utf8JsonWriter writer,
        BedrockGeometryDescription desc,
        BedrockGeometryWriteOptions options)
    {
        if (desc.VisibleBoundsWidth.HasValue)
        {
            writer.WriteNumber("visible_bounds_width", options.RoundCoordinate(desc.VisibleBoundsWidth.Value));
        }

        if (desc.VisibleBoundsHeight.HasValue)
        {
            writer.WriteNumber("visible_bounds_height", options.RoundCoordinate(desc.VisibleBoundsHeight.Value));
        }

        if (desc.VisibleBoundsOffset.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "visible_bounds_offset", desc.VisibleBoundsOffset.Value, options);
        }
    }

    private static void WriteBoneObject(
        Utf8JsonWriter writer,
        BedrockBone bone,
        BedrockGeometryWriteOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", bone.Name);

        if (!string.IsNullOrWhiteSpace(bone.Parent))
        {
            writer.WriteString("parent", bone.Parent);
        }

        JsonParsingUtilities.WriteVector3DArray(writer, "pivot", bone.Pivot, options);

        if (bone.Rotation != Vector3D.Zero)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "rotation", bone.Rotation, options);
        }

        if (bone.BindPoseRotation.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "bind_pose_rotation", bone.BindPoseRotation.Value, options);
        }

        if (bone.Mirror.HasValue)
        {
            writer.WriteBoolean("mirror", bone.Mirror.Value);
        }

        if (bone.Inflate.HasValue)
        {
            writer.WriteNumber("inflate", options.RoundCoordinate(bone.Inflate.Value));
        }

        if (bone.Debug.HasValue)
        {
            writer.WriteBoolean("debug", bone.Debug.Value);
        }

        if (bone.RenderGroupId.HasValue)
        {
            writer.WriteNumber("render_group_id", bone.RenderGroupId.Value);
        }

        if (!string.IsNullOrWhiteSpace(bone.Binding))
        {
            writer.WriteString("binding", bone.Binding);
        }

        if (!string.IsNullOrWhiteSpace(bone.Material))
        {
            writer.WriteString("material", bone.Material);
        }

        if (bone.NeverRender.HasValue)
        {
            writer.WriteBoolean("neverRender", bone.NeverRender.Value);
        }

        if (bone.Reset.HasValue)
        {
            writer.WriteBoolean("reset", bone.Reset.Value);
        }

        if (bone.Cubes.Count > 0)
        {
            writer.WritePropertyName("cubes");
            writer.WriteStartArray();
            foreach (BedrockCube cube in bone.Cubes)
            {
                WriteCubeObject(writer, cube, options);
            }

            writer.WriteEndArray();
        }

        if (bone.Locators.Count > 0)
        {
            writer.WritePropertyName("locators");
            writer.WriteStartObject();
            foreach ((string locName, BedrockLocator locator) in bone.Locators)
            {
                if (!locator.IsObjectForm &&
                    !locator.Rotation.HasValue &&
                    !locator.IgnoreInheritedScale.HasValue &&
                    locator.ExtensionData.IsEmpty)
                {
                    JsonParsingUtilities.WriteVector3DArray(writer, locName, locator.Offset, options);
                }
                else
                {
                    writer.WritePropertyName(locName);
                    writer.WriteStartObject();
                    JsonParsingUtilities.WriteVector3DArray(writer, "offset", locator.Offset, options);
                    if (locator.Rotation.HasValue)
                    {
                        JsonParsingUtilities.WriteVector3DArray(writer, "rotation", locator.Rotation.Value, options);
                    }

                    if (locator.IgnoreInheritedScale.HasValue)
                    {
                        writer.WriteBoolean("ignore_inherited_scale", locator.IgnoreInheritedScale.Value);
                    }

                    if (options.WriteExtensionData && !locator.ExtensionData.IsEmpty)
                    {
                        locator.ExtensionData.WriteTo(writer);
                    }

                    writer.WriteEndObject();
                }
            }

            writer.WriteEndObject();
        }

        if (bone.PolyMesh is { } polyMesh)
        {
            WritePolyMeshObject(writer, polyMesh, options);
        }

        if (options.WriteExtensionData && !bone.ExtensionData.IsEmpty)
        {
            bone.ExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteCubeObject(
        Utf8JsonWriter writer,
        BedrockCube cube,
        BedrockGeometryWriteOptions options)
    {
        writer.WriteStartObject();
        JsonParsingUtilities.WriteVector3DArray(writer, "origin", cube.Origin, options);
        JsonParsingUtilities.WriteVector3DArray(writer, "size", cube.Size, options);

        if (cube.Pivot.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "pivot", cube.Pivot.Value, options);
        }

        if (cube.Rotation.HasValue)
        {
            JsonParsingUtilities.WriteVector3DArray(writer, "rotation", cube.Rotation.Value, options);
        }

        if (cube.Inflate.HasValue)
        {
            writer.WriteNumber("inflate", options.RoundCoordinate(cube.Inflate.Value));
        }

        if (cube.Mirror.HasValue)
        {
            writer.WriteBoolean("mirror", cube.Mirror.Value);
        }

        if (cube.Uv is { } uv)
        {
            WriteCubeUv(writer, uv, options);
        }

        if (options.WriteExtensionData && !cube.ExtensionData.IsEmpty)
        {
            cube.ExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    private static void WriteCubeUv(
        Utf8JsonWriter writer,
        BedrockCubeUv uv,
        BedrockGeometryWriteOptions options)
    {
        if (uv.Mode == BedrockUvMode.BoxUv)
        {
            JsonParsingUtilities.WriteVector2DArray(writer, "uv", uv.BoxUvOffset, options);
            return;
        }

        if (uv.Mode == BedrockUvMode.PerFaceUv)
        {
            writer.WritePropertyName("uv");
            writer.WriteStartObject();
            foreach ((string faceKey, BedrockFaceUv faceUv) in uv.Faces)
            {
                writer.WritePropertyName(faceKey);
                writer.WriteStartObject();
                JsonParsingUtilities.WriteVector2DArray(writer, "uv", faceUv.Uv, options);
                if (faceUv.UvSize.HasValue)
                {
                    JsonParsingUtilities.WriteVector2DArray(writer, "uv_size", faceUv.UvSize.Value, options);
                }

                if (!string.IsNullOrWhiteSpace(faceUv.MaterialInstance))
                {
                    writer.WriteString("material_instance", faceUv.MaterialInstance);
                }

                if (faceUv.UvRotation.HasValue)
                {
                    writer.WriteNumber("uv_rotation", faceUv.UvRotation.Value);
                }

                if (options.WriteExtensionData && !faceUv.ExtensionData.IsEmpty)
                {
                    faceUv.ExtensionData.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            if (options.WriteExtensionData && !uv.ExtensionData.IsEmpty)
            {
                uv.ExtensionData.WriteTo(writer);
            }

            writer.WriteEndObject();
        }
    }

    private static void WritePolyMeshObject(
        Utf8JsonWriter writer,
        BedrockPolyMesh polyMesh,
        BedrockGeometryWriteOptions options)
    {
        writer.WritePropertyName("poly_mesh");
        writer.WriteStartObject();

        if (polyMesh.NormalizedUvs.HasValue)
        {
            writer.WriteBoolean("normalized_uvs", polyMesh.NormalizedUvs.Value);
        }

        if (polyMesh.Positions.Count > 0)
        {
            writer.WritePropertyName("positions");
            writer.WriteStartArray();
            foreach (Vector3D pos in polyMesh.Positions)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(options.RoundCoordinate(pos.X));
                writer.WriteNumberValue(options.RoundCoordinate(pos.Y));
                writer.WriteNumberValue(options.RoundCoordinate(pos.Z));
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        if (polyMesh.Normals.Count > 0)
        {
            writer.WritePropertyName("normals");
            writer.WriteStartArray();
            foreach (Vector3D norm in polyMesh.Normals)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(options.RoundCoordinate(norm.X));
                writer.WriteNumberValue(options.RoundCoordinate(norm.Y));
                writer.WriteNumberValue(options.RoundCoordinate(norm.Z));
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        if (polyMesh.Uvs.Count > 0)
        {
            writer.WritePropertyName("uvs");
            writer.WriteStartArray();
            foreach (Vector2D uv in polyMesh.Uvs)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(options.RoundCoordinate(uv.X));
                writer.WriteNumberValue(options.RoundCoordinate(uv.Y));
                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        if (!string.IsNullOrWhiteSpace(polyMesh.PolyType))
        {
            writer.WriteString("polys", polyMesh.PolyType);
        }
        else if (polyMesh.Polys.Count > 0)
        {
            writer.WritePropertyName("polys");
            writer.WriteStartArray();
            foreach (int[][] face in polyMesh.Polys)
            {
                writer.WriteStartArray();
                foreach (int[] vert in face)
                {
                    writer.WriteStartArray();
                    foreach (int idx in vert)
                    {
                        writer.WriteNumberValue(idx);
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        if (options.WriteExtensionData && !polyMesh.ExtensionData.IsEmpty)
        {
            polyMesh.ExtensionData.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
