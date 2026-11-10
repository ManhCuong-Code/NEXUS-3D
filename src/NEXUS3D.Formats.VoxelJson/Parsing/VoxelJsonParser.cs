using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Validation;

namespace NEXUS3D.Formats.VoxelJson.Parsing;

/// <summary>
/// Parses Voxel JSON documents across all supported dialects (<see cref="VoxelJsonDialect.NexusVoxelGridV1"/>,
/// <see cref="VoxelJsonDialect.SparseVoxelArray"/>, and <see cref="VoxelJsonDialect.MinecraftBlockModelCuboids"/>).
/// </summary>
public sealed class VoxelJsonParser
{
    private static readonly HashSet<string> NexusRootKnownProperties = new(StringComparer.Ordinal)
    {
        "format", "schema", "version", "schemaVersion", "metadata", "grid", "materials", "layers", "voxels"
    };

    private static readonly HashSet<string> SparseRootKnownProperties = new(StringComparer.Ordinal)
    {
        "format", "name", "author", "description", "version", "dimension", "dimensions", "size",
        "origin", "voxelSize", "palette", "materials", "layers", "voxels"
    };

    private static readonly HashSet<string> CuboidRootKnownProperties = new(StringComparer.Ordinal)
    {
        "format", "credit", "parent", "ambientocclusion", "texture_size", "textures", "elements", "groups"
    };

    private static readonly HashSet<string> MetadataKnownProperties = new(StringComparer.Ordinal)
    {
        "name", "author", "description", "schemaVersion", "version", "parent", "ambientOcclusion", "tags", "customMetadata"
    };

    private static readonly HashSet<string> GridKnownProperties = new(StringComparer.Ordinal)
    {
        "dimensions", "dimension", "size", "origin", "voxelSize", "textureSize", "texture_size"
    };

    private static readonly HashSet<string> MaterialKnownProperties = new(StringComparer.Ordinal)
    {
        "id", "name", "color", "metallic", "roughness", "opacity", "emissive", "texture"
    };

    private static readonly HashSet<string> LayerKnownProperties = new(StringComparer.Ordinal)
    {
        "id", "name", "parentLayerId", "parent", "visible", "isVisible", "locked", "isLocked",
        "opacity", "pivot", "origin", "rotation", "elements", "children"
    };

    private static readonly HashSet<string> VoxelKnownProperties = new(StringComparer.Ordinal)
    {
        "position", "pos", "x", "y", "z", "size", "rotation", "rotationOrigin", "rotationAxis",
        "rescale", "shade", "color", "materialId", "material", "voxelType", "type", "layerId", "layer",
        "name", "faces", "customProperties"
    };

    private static readonly HashSet<string> CuboidElementKnownProperties = new(StringComparer.Ordinal)
    {
        "name", "from", "to", "rotation", "shade", "color", "faces"
    };

    private static readonly HashSet<string> CuboidFaceKnownProperties = new(StringComparer.Ordinal)
    {
        "uv", "texture", "cullface", "rotation", "tintindex"
    };

    private readonly VoxelJsonValidator _validator = new();

    public VoxelJsonParseResult ParseFile(string filePath, VoxelJsonReadOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Parse(stream, options, filePath);
    }

    public VoxelJsonParseResult Parse(string jsonText, VoxelJsonReadOptions? options = null, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(jsonText);
        byte[] bytes = Encoding.UTF8.GetBytes(jsonText);
        return Parse(bytes.AsMemory(), options, filePath);
    }

    public VoxelJsonParseResult Parse(Stream stream, VoxelJsonReadOptions? options = null, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= VoxelJsonReadOptions.Default;

        var sw = Stopwatch.StartNew();
        using JsonDocument jsonDoc = VoxelJsonExtensionHelpers.ParseDocument(stream, options, filePath);
        return ParseDocumentCore(jsonDoc.RootElement, options, filePath, sw);
    }

    public VoxelJsonParseResult Parse(ReadOnlyMemory<byte> utf8Bytes, VoxelJsonReadOptions? options = null, string? filePath = null)
    {
        options ??= VoxelJsonReadOptions.Default;

        var sw = Stopwatch.StartNew();
        using JsonDocument jsonDoc = VoxelJsonExtensionHelpers.ParseDocument(utf8Bytes, options, filePath);
        return ParseDocumentCore(jsonDoc.RootElement, options, filePath, sw);
    }

    private VoxelJsonParseResult ParseDocumentCore(
        JsonElement root,
        VoxelJsonReadOptions options,
        string? filePath,
        Stopwatch sw)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonFormatException(
                JsonErrorCodes.UnsupportedFormat,
                "Voxel JSON root must be a JSON object.",
                "$");
        }

        if (VoxelJsonDialectDetector.IsBedrockAnimationDocument(root))
        {
            throw new JsonFormatException(
                JsonErrorCodes.AnimationFileNotSupported,
                "The JSON document is a Minecraft Bedrock Animation file ('animations'), not a Voxel JSON model.",
                "$.animations");
        }

        if (VoxelJsonDialectDetector.IsBedrockGeometryDocument(root))
        {
            throw new JsonFormatException(
                JsonErrorCodes.UnsupportedFormat,
                "The JSON document is a Minecraft Bedrock Geometry file, not a Voxel JSON model. Use BedrockGeometryParser instead.",
                "$");
        }

        if (root.TryGetProperty("voxels", out JsonElement rawVoxelsProp) &&
            rawVoxelsProp.ValueKind != JsonValueKind.Array)
        {
            throw new JsonFormatException(
                JsonErrorCodes.MissingRequiredField,
                "Property 'voxels' must be a JSON array.",
                "$.voxels");
        }

        if (root.TryGetProperty("elements", out JsonElement rawElementsProp) &&
            rawElementsProp.ValueKind != JsonValueKind.Array)
        {
            throw new JsonFormatException(
                JsonErrorCodes.MissingRequiredField,
                "Property 'elements' must be a JSON array.",
                "$.elements");
        }

        VoxelJsonDialect dialect = VoxelJsonDialectDetector.Detect(root);
        if (dialect == VoxelJsonDialect.Unknown)
        {
            throw new JsonFormatException(
                JsonErrorCodes.UnsupportedFormat,
                "Unrecognized Voxel JSON structure. Expected 'grid'+'voxels' (NexusVoxelGridV1), 'voxels' array (SparseVoxelArray), or 'elements' array (MinecraftBlockModelCuboids).",
                "$");
        }

        var diagnostics = new List<JsonFormatDiagnostic>();
        var document = new VoxelDocument { Dialect = dialect };

        switch (dialect)
        {
            case VoxelJsonDialect.NexusVoxelGridV1:
                ParseNexusVoxelGridV1(root, document, options, diagnostics, filePath);
                break;
            case VoxelJsonDialect.SparseVoxelArray:
                ParseSparseVoxelArray(root, document, options, diagnostics, filePath);
                break;
            case VoxelJsonDialect.MinecraftBlockModelCuboids:
                ParseMinecraftBlockModelCuboids(root, document, options, diagnostics, filePath);
                break;
        }

        int rawCount = document.Voxels.Count;
        document.Voxels = VoxelJsonValidator.ApplyDuplicatePolicy(
            document.Voxels,
            options.DuplicatePolicy,
            diagnostics,
            filePath,
            out int duplicateCount);

        IReadOnlyList<JsonFormatDiagnostic> validationDiagnostics = _validator.Validate(document, options, filePath);
        foreach (JsonFormatDiagnostic diag in validationDiagnostics)
        {
            if (!diagnostics.Any(existing => existing.Code == diag.Code && existing.JsonPath == diag.JsonPath && existing.Message == diag.Message))
            {
                diagnostics.Add(diag);
            }
        }

        if (options.StrictMode)
        {
            JsonFormatDiagnostic? firstError = diagnostics.FirstOrDefault(d => d.IsError);
            if (firstError is not null)
            {
                throw new JsonFormatException(
                    firstError.Code,
                    firstError.Message,
                    firstError.JsonPath);
            }
        }

        sw.Stop();
        return new VoxelJsonParseResult
        {
            Document = document,
            DetectedDialect = dialect,
            Diagnostics = diagnostics,
            RawVoxelCount = rawCount,
            DuplicateVoxelCount = duplicateCount,
            Elapsed = sw.Elapsed
        };
    }

    private static void ParseNexusVoxelGridV1(
        JsonElement root,
        VoxelDocument document,
        VoxelJsonReadOptions options,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath)
    {
        if (options.PreserveExtensionData)
        {
            document.RootExtensionData.CaptureUnknownProperties(root, NexusRootKnownProperties);
        }

        if (root.TryGetProperty("version", out JsonElement verProp) && verProp.ValueKind == JsonValueKind.String)
        {
            document.Metadata.SchemaVersion = verProp.GetString() ?? "1.0";
        }
        else if (root.TryGetProperty("schemaVersion", out JsonElement schemaVerProp) && schemaVerProp.ValueKind == JsonValueKind.String)
        {
            document.Metadata.SchemaVersion = schemaVerProp.GetString() ?? "1.0";
        }

        if (root.TryGetProperty("metadata", out JsonElement metaProp) && metaProp.ValueKind == JsonValueKind.Object)
        {
            ParseMetadataObject(metaProp, document.Metadata, options);
        }

        if (root.TryGetProperty("grid", out JsonElement gridProp) && gridProp.ValueKind == JsonValueKind.Object)
        {
            ParseGridObject(gridProp, document.Grid, options, diagnostics, filePath, "$.grid");
        }

        if (root.TryGetProperty("materials", out JsonElement matsProp))
        {
            ParseMaterialsSection(matsProp, document.Materials, options, diagnostics, filePath, "$.materials");
        }

        if (root.TryGetProperty("layers", out JsonElement layersProp) && layersProp.ValueKind == JsonValueKind.Array)
        {
            ParseLayersArray(layersProp, document.Layers, options, diagnostics, filePath, "$.layers");
        }

        if (root.TryGetProperty("voxels", out JsonElement voxelsProp) && voxelsProp.ValueKind == JsonValueKind.Array)
        {
            ParseVoxelsArray(voxelsProp, document.Voxels, document.Materials, options, diagnostics, filePath, "$.voxels");
        }
    }

    private static void ParseSparseVoxelArray(
        JsonElement root,
        VoxelDocument document,
        VoxelJsonReadOptions options,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath)
    {
        if (options.PreserveExtensionData)
        {
            document.RootExtensionData.CaptureUnknownProperties(root, SparseRootKnownProperties);
        }

        if (root.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind == JsonValueKind.String)
        {
            document.Metadata.Name = nameProp.GetString() ?? document.Metadata.Name;
        }

        if (root.TryGetProperty("author", out JsonElement authorProp) && authorProp.ValueKind == JsonValueKind.String)
        {
            document.Metadata.Author = authorProp.GetString();
        }

        if (root.TryGetProperty("description", out JsonElement descProp) && descProp.ValueKind == JsonValueKind.String)
        {
            document.Metadata.Description = descProp.GetString();
        }

        if (root.TryGetProperty("version", out JsonElement verProp) && verProp.ValueKind == JsonValueKind.String)
        {
            document.Metadata.SchemaVersion = verProp.GetString() ?? "1.0";
        }

        if (TryReadVector3DProperty(root, "dimension", out Vector3D dim) ||
            TryReadVector3DProperty(root, "dimensions", out dim) ||
            TryReadVector3DProperty(root, "size", out dim))
        {
            document.Grid.Dimensions = dim;
        }

        if (TryReadVector3DProperty(root, "origin", out Vector3D origin))
        {
            document.Grid.Origin = origin;
        }

        if (TryReadVector3DProperty(root, "voxelSize", out Vector3D voxelSize))
        {
            document.Grid.VoxelSize = voxelSize;
        }

        if (root.TryGetProperty("palette", out JsonElement paletteProp))
        {
            ParseMaterialsSection(paletteProp, document.Materials, options, diagnostics, filePath, "$.palette");
        }
        else if (root.TryGetProperty("materials", out JsonElement matsProp))
        {
            ParseMaterialsSection(matsProp, document.Materials, options, diagnostics, filePath, "$.materials");
        }

        if (root.TryGetProperty("layers", out JsonElement layersProp) && layersProp.ValueKind == JsonValueKind.Array)
        {
            ParseLayersArray(layersProp, document.Layers, options, diagnostics, filePath, "$.layers");
        }

        if (root.TryGetProperty("voxels", out JsonElement voxelsProp) && voxelsProp.ValueKind == JsonValueKind.Array)
        {
            ParseVoxelsArray(voxelsProp, document.Voxels, document.Materials, options, diagnostics, filePath, "$.voxels");
        }
    }

    private static void ParseMinecraftBlockModelCuboids(
        JsonElement root,
        VoxelDocument document,
        VoxelJsonReadOptions options,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath)
    {
        if (options.PreserveExtensionData)
        {
            document.RootExtensionData.CaptureUnknownProperties(root, CuboidRootKnownProperties);
        }

        if (root.TryGetProperty("credit", out JsonElement creditProp) && creditProp.ValueKind == JsonValueKind.String)
        {
            document.Metadata.Author = creditProp.GetString();
        }

        if (root.TryGetProperty("parent", out JsonElement parentProp) && parentProp.ValueKind == JsonValueKind.String)
        {
            document.Metadata.ParentModel = parentProp.GetString();
            if (!string.IsNullOrWhiteSpace(document.Metadata.ParentModel))
            {
                document.Metadata.Name = document.Metadata.ParentModel!;
            }
        }

        if (root.TryGetProperty("ambientocclusion", out JsonElement aoProp) &&
            (aoProp.ValueKind == JsonValueKind.True || aoProp.ValueKind == JsonValueKind.False))
        {
            document.Metadata.AmbientOcclusion = aoProp.GetBoolean();
        }

        if (root.TryGetProperty("texture_size", out JsonElement texSizeProp) &&
            TryParseVector2D(texSizeProp, out Vector2D texSize))
        {
            document.Grid.TextureSize = texSize;
        }

        if (root.TryGetProperty("textures", out JsonElement texturesProp) &&
            texturesProp.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty texEntry in texturesProp.EnumerateObject())
            {
                string texValue = texEntry.Value.ValueKind == JsonValueKind.String
                    ? texEntry.Value.GetString() ?? string.Empty
                    : texEntry.Value.GetRawText();

                document.Materials.Add(new VoxelMaterialReference
                {
                    Id = texEntry.Name,
                    Name = texEntry.Name,
                    TextureReference = texValue
                });
            }
        }

        var elementIndexToLayerId = new Dictionary<int, string>();
        if (root.TryGetProperty("groups", out JsonElement groupsProp) &&
            groupsProp.ValueKind == JsonValueKind.Array)
        {
            ParseCuboidGroups(groupsProp, document.Layers, elementIndexToLayerId, null, options);
        }

        if (root.TryGetProperty("elements", out JsonElement elementsProp) &&
            elementsProp.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement elem in elementsProp.EnumerateArray())
            {
                string elemPath = $"$.elements[{index}]";
                if (elem.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(new JsonFormatDiagnostic(
                        JsonErrorCodes.InvalidVoxelElement,
                        $"Cuboid element at index {index} must be a JSON object.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: elemPath));
                    index++;
                    continue;
                }

                if (!elem.TryGetProperty("from", out JsonElement fromProp) ||
                    !TryParseVector3D(fromProp, out Vector3D fromVec))
                {
                    diagnostics.Add(new JsonFormatDiagnostic(
                        JsonErrorCodes.InvalidCoordinate,
                        $"Cuboid element at index {index} is missing a valid 3D 'from' coordinate.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{elemPath}.from"));
                    index++;
                    continue;
                }

                if (!elem.TryGetProperty("to", out JsonElement toProp) ||
                    !TryParseVector3D(toProp, out Vector3D toVec))
                {
                    diagnostics.Add(new JsonFormatDiagnostic(
                        JsonErrorCodes.InvalidCoordinate,
                        $"Cuboid element at index {index} is missing a valid 3D 'to' coordinate.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{elemPath}.to"));
                    index++;
                    continue;
                }

                Vector3D rawSize = toVec - fromVec;
                if (!rawSize.IsFinite() || rawSize.X < 0.0 || rawSize.Y < 0.0 || rawSize.Z < 0.0)
                {
                    diagnostics.Add(new JsonFormatDiagnostic(
                        JsonErrorCodes.InvalidDimensions,
                        $"Cuboid element at index {index} has negative size (from={fromVec}, to={toVec}).",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: elemPath));
                }

                // Allow zero-thickness billboard planes in Blockbench by clamping 0 to a micro-thickness for positive size validation
                Vector3D effectiveSize = new(
                    rawSize.X == 0.0 ? 0.0001 : rawSize.X,
                    rawSize.Y == 0.0 ? 0.0001 : rawSize.Y,
                    rawSize.Z == 0.0 ? 0.0001 : rawSize.Z);

                var record = new VoxelRecord
                {
                    Position = fromVec,
                    Size = effectiveSize
                };

                if (rawSize.X == 0.0 || rawSize.Y == 0.0 || rawSize.Z == 0.0)
                {
                    record.CustomProperties["cuboid.rawTo"] =
                        string.Create(CultureInfo.InvariantCulture, $"{toVec.X},{toVec.Y},{toVec.Z}");
                }

                if (elem.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind == JsonValueKind.String)
                {
                    record.Name = nameProp.GetString();
                }

                if (elem.TryGetProperty("shade", out JsonElement shadeProp) &&
                    (shadeProp.ValueKind == JsonValueKind.True || shadeProp.ValueKind == JsonValueKind.False))
                {
                    record.Shade = shadeProp.GetBoolean();
                }

                if (elem.TryGetProperty("color", out JsonElement colorProp))
                {
                    if (TryParseVoxelColor(colorProp, out VoxelColor parsedColor))
                    {
                        record.Color = parsedColor;
                    }
                    else if (colorProp.ValueKind == JsonValueKind.Number && colorProp.TryGetInt32(out int markerColorIndex))
                    {
                        record.CustomProperties["cuboid.markerColor"] = markerColorIndex.ToString(CultureInfo.InvariantCulture);
                    }
                }

                if (elem.TryGetProperty("rotation", out JsonElement rotProp) && rotProp.ValueKind == JsonValueKind.Object)
                {
                    if (rotProp.TryGetProperty("origin", out JsonElement rotOriginProp) &&
                        TryParseVector3D(rotOriginProp, out Vector3D rotOrigin))
                    {
                        record.RotationOrigin = rotOrigin;
                    }

                    string axis = rotProp.TryGetProperty("axis", out JsonElement axisProp) && axisProp.ValueKind == JsonValueKind.String
                        ? (axisProp.GetString() ?? "y").ToLowerInvariant()
                        : "y";
                    record.RotationAxis = axis;

                    if (rotProp.TryGetProperty("angle", out JsonElement angleProp) &&
                        angleProp.ValueKind == JsonValueKind.Number &&
                        angleProp.TryGetDouble(out double angle))
                    {
                        record.Rotation = axis switch
                        {
                            "x" => new Vector3D(angle, 0.0, 0.0),
                            "z" => new Vector3D(0.0, 0.0, angle),
                            _ => new Vector3D(0.0, angle, 0.0)
                        };
                    }

                    if (rotProp.TryGetProperty("rescale", out JsonElement rescaleProp) &&
                        (rescaleProp.ValueKind == JsonValueKind.True || rescaleProp.ValueKind == JsonValueKind.False))
                    {
                        record.Rescale = rescaleProp.GetBoolean();
                    }
                }

                if (elem.TryGetProperty("faces", out JsonElement facesProp) && facesProp.ValueKind == JsonValueKind.Object)
                {
                    ParseCuboidFaces(facesProp, record, options, diagnostics, filePath, $"{elemPath}.faces");
                }

                if (elementIndexToLayerId.TryGetValue(index, out string? assignedLayerId))
                {
                    record.LayerId = assignedLayerId;
                }

                if (options.PreserveExtensionData)
                {
                    record.ExtensionData.CaptureUnknownProperties(elem, CuboidElementKnownProperties);
                }

                document.Voxels.Add(record);
                index++;
            }
        }
    }

    private static void ParseCuboidGroups(
        JsonElement groupsArray,
        List<VoxelLayerDefinition> layers,
        Dictionary<int, string> elementIndexToLayerId,
        string? parentLayerId,
        VoxelJsonReadOptions options)
    {
        int i = 0;
        foreach (JsonElement item in groupsArray.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out int childElemIdx) && parentLayerId is not null)
            {
                elementIndexToLayerId[childElemIdx] = parentLayerId;
                VoxelLayerDefinition? parentLayer = layers.FirstOrDefault(l => l.Id == parentLayerId);
                parentLayer?.ElementIndices.Add(childElemIdx);
                i++;
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                i++;
                continue;
            }

            string name = item.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? nameProp.GetString() ?? $"group_{i}"
                : $"group_{i}";

            string id = parentLayerId is null ? name : $"{parentLayerId}/{name}";
            var layer = new VoxelLayerDefinition
            {
                Id = id,
                Name = name,
                ParentLayerId = parentLayerId
            };

            if ((item.TryGetProperty("origin", out JsonElement originProp) || item.TryGetProperty("pivot", out originProp)) &&
                TryParseVector3D(originProp, out Vector3D pivot))
            {
                layer.Pivot = pivot;
            }

            if (item.TryGetProperty("rotation", out JsonElement rotProp) &&
                TryParseVector3D(rotProp, out Vector3D rot))
            {
                layer.Rotation = rot;
            }

            if (item.TryGetProperty("visibility", out JsonElement visProp) &&
                (visProp.ValueKind == JsonValueKind.True || visProp.ValueKind == JsonValueKind.False))
            {
                layer.IsVisible = visProp.GetBoolean();
            }

            if (options.PreserveExtensionData)
            {
                layer.ExtensionData.CaptureUnknownProperties(item, LayerKnownProperties);
            }

            layers.Add(layer);

            if (item.TryGetProperty("children", out JsonElement childrenProp) &&
                childrenProp.ValueKind == JsonValueKind.Array)
            {
                ParseCuboidGroups(childrenProp, layers, elementIndexToLayerId, id, options);
            }

            i++;
        }
    }

    private static void ParseCuboidFaces(
        JsonElement facesObj,
        VoxelRecord record,
        VoxelJsonReadOptions options,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        foreach (JsonProperty faceProp in facesObj.EnumerateObject())
        {
            if (faceProp.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var faceUv = new VoxelCuboidFaceUv();
            if (faceProp.Value.TryGetProperty("uv", out JsonElement uvProp) &&
                uvProp.ValueKind == JsonValueKind.Array)
            {
                var uvValues = new List<double>(4);
                foreach (JsonElement num in uvProp.EnumerateArray())
                {
                    if (num.ValueKind == JsonValueKind.Number && num.TryGetDouble(out double d) && double.IsFinite(d))
                    {
                        uvValues.Add(d);
                    }
                }

                if (uvValues.Count == 4)
                {
                    faceUv.Uv = [.. uvValues];
                }
                else
                {
                    diagnostics.Add(new JsonFormatDiagnostic(
                        JsonErrorCodes.InvalidUv,
                        $"Face '{faceProp.Name}' UV array must contain 4 numbers.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{jsonPath}.{faceProp.Name}.uv"));
                }
            }

            if (faceProp.Value.TryGetProperty("texture", out JsonElement texProp) && texProp.ValueKind == JsonValueKind.String)
            {
                faceUv.Texture = texProp.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(record.MaterialId) && !string.IsNullOrWhiteSpace(faceUv.Texture))
                {
                    record.MaterialId = faceUv.Texture.TrimStart('#');
                }
            }

            if (faceProp.Value.TryGetProperty("cullface", out JsonElement cullProp) && cullProp.ValueKind == JsonValueKind.String)
            {
                faceUv.CullFace = cullProp.GetString();
            }

            if (faceProp.Value.TryGetProperty("rotation", out JsonElement rotProp) &&
                rotProp.ValueKind == JsonValueKind.Number &&
                rotProp.TryGetInt32(out int rotDeg))
            {
                faceUv.Rotation = rotDeg;
            }

            if (faceProp.Value.TryGetProperty("tintindex", out JsonElement tintProp) &&
                tintProp.ValueKind == JsonValueKind.Number &&
                tintProp.TryGetInt32(out int tintIdx))
            {
                faceUv.TintIndex = tintIdx;
            }

            if (options.PreserveExtensionData)
            {
                faceUv.ExtensionData.CaptureUnknownProperties(faceProp.Value, CuboidFaceKnownProperties);
            }

            record.Faces[faceProp.Name] = faceUv;
        }
    }

    private static void ParseMetadataObject(
        JsonElement metaObj,
        VoxelDocumentMetadata metadata,
        VoxelJsonReadOptions options)
    {
        if (metaObj.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind == JsonValueKind.String)
        {
            metadata.Name = nameProp.GetString() ?? metadata.Name;
        }

        if (metaObj.TryGetProperty("author", out JsonElement authorProp) && authorProp.ValueKind == JsonValueKind.String)
        {
            metadata.Author = authorProp.GetString();
        }

        if (metaObj.TryGetProperty("description", out JsonElement descProp) && descProp.ValueKind == JsonValueKind.String)
        {
            metadata.Description = descProp.GetString();
        }

        if (metaObj.TryGetProperty("schemaVersion", out JsonElement schemaVerProp) && schemaVerProp.ValueKind == JsonValueKind.String)
        {
            metadata.SchemaVersion = schemaVerProp.GetString() ?? metadata.SchemaVersion;
        }
        else if (metaObj.TryGetProperty("version", out JsonElement verProp) && verProp.ValueKind == JsonValueKind.String)
        {
            metadata.SchemaVersion = verProp.GetString() ?? metadata.SchemaVersion;
        }

        if (metaObj.TryGetProperty("parent", out JsonElement parentProp) && parentProp.ValueKind == JsonValueKind.String)
        {
            metadata.ParentModel = parentProp.GetString();
        }

        if (metaObj.TryGetProperty("ambientOcclusion", out JsonElement aoProp) &&
            (aoProp.ValueKind == JsonValueKind.True || aoProp.ValueKind == JsonValueKind.False))
        {
            metadata.AmbientOcclusion = aoProp.GetBoolean();
        }

        if (metaObj.TryGetProperty("tags", out JsonElement tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement tag in tagsProp.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String && tag.GetString() is { } s)
                {
                    metadata.Tags.Add(s);
                }
            }
        }

        if (metaObj.TryGetProperty("customMetadata", out JsonElement customProp) && customProp.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty prop in customProp.EnumerateObject())
            {
                metadata.CustomMetadata[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.GetRawText();
            }
        }

        if (options.PreserveExtensionData)
        {
            metadata.ExtensionData.CaptureUnknownProperties(metaObj, MetadataKnownProperties);
        }
    }

    private static void ParseGridObject(
        JsonElement gridObj,
        VoxelGridDefinition grid,
        VoxelJsonReadOptions options,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        if (gridObj.TryGetProperty("dimensions", out JsonElement dimProp) ||
            gridObj.TryGetProperty("dimension", out dimProp) ||
            gridObj.TryGetProperty("size", out dimProp))
        {
            if (TryParseVector3D(dimProp, out Vector3D dim))
            {
                grid.Dimensions = dim;
            }
            else
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidDimensions,
                    "Grid 'dimensions' must be a valid 3D vector.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.dimensions"));
            }
        }

        if (gridObj.TryGetProperty("origin", out JsonElement originProp))
        {
            if (TryParseVector3D(originProp, out Vector3D origin))
            {
                grid.Origin = origin;
            }
            else
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidCoordinate,
                    "Grid 'origin' must be a valid 3D vector.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.origin"));
            }
        }

        if (gridObj.TryGetProperty("voxelSize", out JsonElement voxelSizeProp))
        {
            if (voxelSizeProp.ValueKind == JsonValueKind.Number && voxelSizeProp.TryGetDouble(out double uniformSize))
            {
                grid.VoxelSize = new Vector3D(uniformSize, uniformSize, uniformSize);
            }
            else if (TryParseVector3D(voxelSizeProp, out Vector3D voxelSize))
            {
                grid.VoxelSize = voxelSize;
            }
            else
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidDimensions,
                    "Grid 'voxelSize' must be a positive number or 3D vector.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.voxelSize"));
            }
        }

        if ((gridObj.TryGetProperty("textureSize", out JsonElement texSizeProp) ||
             gridObj.TryGetProperty("texture_size", out texSizeProp)) &&
            TryParseVector2D(texSizeProp, out Vector2D texSize))
        {
            grid.TextureSize = texSize;
        }

        if (options.PreserveExtensionData)
        {
            grid.ExtensionData.CaptureUnknownProperties(gridObj, GridKnownProperties);
        }
    }

    private static void ParseMaterialsSection(
        JsonElement matsElement,
        List<VoxelMaterialReference> materials,
        VoxelJsonReadOptions options,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        if (matsElement.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in matsElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } colorStr)
                {
                    if (VoxelColor.TryParseHex(colorStr, out VoxelColor hexColor))
                    {
                        materials.Add(new VoxelMaterialReference
                        {
                            Id = index.ToString(CultureInfo.InvariantCulture),
                            Name = $"palette_{index}",
                            Color = hexColor
                        });
                    }
                    else
                    {
                        diagnostics.Add(new JsonFormatDiagnostic(
                            JsonErrorCodes.InvalidVoxelElement,
                            $"Invalid hex color '{colorStr}' in palette at index {index}.",
                            IsError: true,
                            FilePath: filePath,
                            JsonPath: $"{jsonPath}[{index}]"));
                    }
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    materials.Add(ParseMaterialObject(item, index, options, diagnostics, filePath, $"{jsonPath}[{index}]"));
                }

                index++;
            }
        }
        else if (matsElement.ValueKind == JsonValueKind.Object)
        {
            int index = 0;
            foreach (JsonProperty prop in matsElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String && prop.Value.GetString() is { } valStr)
                {
                    if (VoxelColor.TryParseHex(valStr, out VoxelColor hexColor))
                    {
                        materials.Add(new VoxelMaterialReference
                        {
                            Id = prop.Name,
                            Name = prop.Name,
                            Color = hexColor
                        });
                    }
                    else
                    {
                        materials.Add(new VoxelMaterialReference
                        {
                            Id = prop.Name,
                            Name = prop.Name,
                            TextureReference = valStr
                        });
                    }
                }
                else if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    VoxelMaterialReference mat = ParseMaterialObject(prop.Value, index, options, diagnostics, filePath, $"{jsonPath}.{prop.Name}");
                    if (string.IsNullOrWhiteSpace(mat.Id))
                    {
                        mat.Id = prop.Name;
                    }

                    if (string.IsNullOrWhiteSpace(mat.Name))
                    {
                        mat.Name = prop.Name;
                    }

                    materials.Add(mat);
                }

                index++;
            }
        }
    }

    private static VoxelMaterialReference ParseMaterialObject(
        JsonElement obj,
        int index,
        VoxelJsonReadOptions options,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        string id = obj.TryGetProperty("id", out JsonElement idProp) && idProp.ValueKind == JsonValueKind.String
            ? idProp.GetString() ?? $"mat_{index}"
            : $"mat_{index}";

        string name = obj.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind == JsonValueKind.String
            ? nameProp.GetString() ?? id
            : id;

        var mat = new VoxelMaterialReference
        {
            Id = id,
            Name = name
        };

        if (obj.TryGetProperty("color", out JsonElement colorProp))
        {
            if (TryParseVoxelColor(colorProp, out VoxelColor color))
            {
                mat.Color = color;
            }
            else
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidVoxelElement,
                    $"Material '{id}' has an invalid color value.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.color"));
            }
        }

        if (obj.TryGetProperty("metallic", out JsonElement metallicProp) &&
            metallicProp.ValueKind == JsonValueKind.Number &&
            metallicProp.TryGetDouble(out double metallic))
        {
            mat.Metallic = metallic;
        }

        if (obj.TryGetProperty("roughness", out JsonElement roughProp) &&
            roughProp.ValueKind == JsonValueKind.Number &&
            roughProp.TryGetDouble(out double roughness))
        {
            mat.Roughness = roughness;
        }

        if (obj.TryGetProperty("opacity", out JsonElement opacityProp) &&
            opacityProp.ValueKind == JsonValueKind.Number &&
            opacityProp.TryGetDouble(out double opacity))
        {
            mat.Opacity = opacity;
        }

        if (obj.TryGetProperty("emissive", out JsonElement emissiveProp) &&
            emissiveProp.ValueKind == JsonValueKind.Number &&
            emissiveProp.TryGetDouble(out double emissive))
        {
            mat.Emissive = emissive;
        }

        if (obj.TryGetProperty("texture", out JsonElement texProp) && texProp.ValueKind == JsonValueKind.String)
        {
            mat.TextureReference = texProp.GetString();
        }

        if (options.PreserveExtensionData)
        {
            mat.ExtensionData.CaptureUnknownProperties(obj, MaterialKnownProperties);
        }

        return mat;
    }

    private static void ParseLayersArray(
        JsonElement layersArray,
        List<VoxelLayerDefinition> layers,
        VoxelJsonReadOptions options,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        int index = 0;
        foreach (JsonElement item in layersArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidVoxelElement,
                    $"Layer at index {index} must be a JSON object.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}[{index}]"));
                index++;
                continue;
            }

            string id = item.TryGetProperty("id", out JsonElement idProp) && idProp.ValueKind == JsonValueKind.String
                ? idProp.GetString() ?? $"layer_{index}"
                : $"layer_{index}";

            string name = item.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? nameProp.GetString() ?? id
                : id;

            var layer = new VoxelLayerDefinition
            {
                Id = id,
                Name = name
            };

            if ((item.TryGetProperty("parentLayerId", out JsonElement parentProp) ||
                 item.TryGetProperty("parent", out parentProp)) &&
                parentProp.ValueKind == JsonValueKind.String)
            {
                layer.ParentLayerId = parentProp.GetString();
            }

            if ((item.TryGetProperty("visible", out JsonElement visProp) ||
                 item.TryGetProperty("isVisible", out visProp)) &&
                (visProp.ValueKind == JsonValueKind.True || visProp.ValueKind == JsonValueKind.False))
            {
                layer.IsVisible = visProp.GetBoolean();
            }

            if ((item.TryGetProperty("locked", out JsonElement lockProp) ||
                 item.TryGetProperty("isLocked", out lockProp)) &&
                (lockProp.ValueKind == JsonValueKind.True || lockProp.ValueKind == JsonValueKind.False))
            {
                layer.IsLocked = lockProp.GetBoolean();
            }

            if (item.TryGetProperty("opacity", out JsonElement opacityProp) &&
                opacityProp.ValueKind == JsonValueKind.Number &&
                opacityProp.TryGetDouble(out double opacity))
            {
                layer.Opacity = opacity;
            }

            if ((item.TryGetProperty("pivot", out JsonElement pivotProp) ||
                 item.TryGetProperty("origin", out pivotProp)) &&
                TryParseVector3D(pivotProp, out Vector3D pivot))
            {
                layer.Pivot = pivot;
            }

            if (item.TryGetProperty("rotation", out JsonElement rotProp) &&
                TryParseVector3D(rotProp, out Vector3D rot))
            {
                layer.Rotation = rot;
            }

            if (options.PreserveExtensionData)
            {
                layer.ExtensionData.CaptureUnknownProperties(item, LayerKnownProperties);
            }

            layers.Add(layer);
            index++;
        }
    }

    private static void ParseVoxelsArray(
        JsonElement voxelsArray,
        List<VoxelRecord> voxels,
        List<VoxelMaterialReference> materials,
        VoxelJsonReadOptions options,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        int index = 0;
        foreach (JsonElement item in voxelsArray.EnumerateArray())
        {
            if (index >= options.MaxVoxelCount)
            {
                throw new JsonFormatException(
                    JsonErrorCodes.LimitExceeded,
                    $"Voxel count exceeds configured maximum of {options.MaxVoxelCount}.",
                    $"{jsonPath}[{index}]");
            }

            string itemPath = $"{jsonPath}[{index}]";

            if (item.ValueKind == JsonValueKind.Array)
            {
                if (TryParseCompactVoxelTuple(item, materials, out VoxelRecord? tupleRecord, out string? errorMsg))
                {
                    voxels.Add(tupleRecord!);
                }
                else
                {
                    diagnostics.Add(new JsonFormatDiagnostic(
                        JsonErrorCodes.InvalidVoxelElement,
                        errorMsg ?? $"Invalid compact voxel tuple at index {index}.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: itemPath));
                }

                index++;
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidVoxelElement,
                    $"Voxel entry at index {index} must be an object or coordinate tuple.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: itemPath));
                index++;
                continue;
            }

            if (!TryReadVoxelPosition(item, out Vector3D pos))
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidCoordinate,
                    $"Voxel entry at index {index} is missing valid 'position' or 'x','y','z' coordinates.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: itemPath));
                index++;
                continue;
            }

            var record = new VoxelRecord { Position = pos };

            if (item.TryGetProperty("size", out JsonElement sizeProp))
            {
                if (sizeProp.ValueKind == JsonValueKind.Number && sizeProp.TryGetDouble(out double uniformSize))
                {
                    record.Size = new Vector3D(uniformSize, uniformSize, uniformSize);
                }
                else if (TryParseVector3D(sizeProp, out Vector3D parsedSize))
                {
                    record.Size = parsedSize;
                }
                else
                {
                    record.Size = new Vector3D(-1, -1, -1); // Triggers validator InvalidDimensions
                }
            }

            if (item.TryGetProperty("rotation", out JsonElement rotProp))
            {
                if (TryParseVector3D(rotProp, out Vector3D parsedRot))
                {
                    record.Rotation = parsedRot;
                }
                else
                {
                    diagnostics.Add(new JsonFormatDiagnostic(
                        JsonErrorCodes.InvalidCoordinate,
                        $"Voxel entry at index {index} has invalid 'rotation'.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{itemPath}.rotation"));
                }
            }

            if (item.TryGetProperty("rotationOrigin", out JsonElement rotOriginProp) &&
                TryParseVector3D(rotOriginProp, out Vector3D rotOrigin))
            {
                record.RotationOrigin = rotOrigin;
            }

            if (item.TryGetProperty("rotationAxis", out JsonElement rotAxisProp) && rotAxisProp.ValueKind == JsonValueKind.String)
            {
                record.RotationAxis = rotAxisProp.GetString();
            }

            if (item.TryGetProperty("rescale", out JsonElement rescaleProp) &&
                (rescaleProp.ValueKind == JsonValueKind.True || rescaleProp.ValueKind == JsonValueKind.False))
            {
                record.Rescale = rescaleProp.GetBoolean();
            }

            if (item.TryGetProperty("shade", out JsonElement shadeProp) &&
                (shadeProp.ValueKind == JsonValueKind.True || shadeProp.ValueKind == JsonValueKind.False))
            {
                record.Shade = shadeProp.GetBoolean();
            }

            if (item.TryGetProperty("color", out JsonElement colorProp))
            {
                if (TryParseVoxelColor(colorProp, out VoxelColor color))
                {
                    record.Color = color;
                }
                else
                {
                    record.Color = new VoxelColor(double.NaN, 0, 0, 1); // Triggers validator InvalidVoxelElement
                }
            }

            if ((item.TryGetProperty("materialId", out JsonElement matProp) ||
                 item.TryGetProperty("material", out matProp)))
            {
                if (matProp.ValueKind == JsonValueKind.String)
                {
                    record.MaterialId = matProp.GetString();
                }
                else if (matProp.ValueKind == JsonValueKind.Number && matProp.TryGetInt32(out int matIndex))
                {
                    record.MaterialId = matIndex.ToString(CultureInfo.InvariantCulture);
                }
            }

            if ((item.TryGetProperty("voxelType", out JsonElement typeProp) ||
                 item.TryGetProperty("type", out typeProp)) &&
                typeProp.ValueKind == JsonValueKind.String)
            {
                record.VoxelType = typeProp.GetString();
            }

            if ((item.TryGetProperty("layerId", out JsonElement layerProp) ||
                 item.TryGetProperty("layer", out layerProp)) &&
                layerProp.ValueKind == JsonValueKind.String)
            {
                record.LayerId = layerProp.GetString();
            }

            if (item.TryGetProperty("name", out JsonElement nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                record.Name = nameProp.GetString();
            }

            if (item.TryGetProperty("faces", out JsonElement facesProp) && facesProp.ValueKind == JsonValueKind.Object)
            {
                ParseCuboidFaces(facesProp, record, options, diagnostics, filePath, $"{itemPath}.faces");
            }

            if (item.TryGetProperty("customProperties", out JsonElement customProp) && customProp.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in customProp.EnumerateObject())
                {
                    record.CustomProperties[p.Name] = p.Value.ValueKind == JsonValueKind.String
                        ? p.Value.GetString() ?? string.Empty
                        : p.Value.GetRawText();
                }
            }

            if (options.PreserveExtensionData)
            {
                record.ExtensionData.CaptureUnknownProperties(item, VoxelKnownProperties);
            }

            voxels.Add(record);
            index++;
        }
    }

    private static bool TryParseCompactVoxelTuple(
        JsonElement tupleArray,
        List<VoxelMaterialReference> materials,
        out VoxelRecord? record,
        out string? errorMessage)
    {
        record = null;
        errorMessage = null;

        int len = tupleArray.GetArrayLength();
        if (len < 3)
        {
            errorMessage = "Compact voxel tuple must contain at least [x, y, z].";
            return false;
        }

        using JsonElement.ArrayEnumerator enumerator = tupleArray.EnumerateArray();
        enumerator.MoveNext();
        if (!TryReadFiniteDouble(enumerator.Current, out double x))
        {
            errorMessage = "Invalid X coordinate in compact voxel tuple.";
            return false;
        }

        enumerator.MoveNext();
        if (!TryReadFiniteDouble(enumerator.Current, out double y))
        {
            errorMessage = "Invalid Y coordinate in compact voxel tuple.";
            return false;
        }

        enumerator.MoveNext();
        if (!TryReadFiniteDouble(enumerator.Current, out double z))
        {
            errorMessage = "Invalid Z coordinate in compact voxel tuple.";
            return false;
        }

        record = new VoxelRecord { Position = new Vector3D(x, y, z) };

        if (len >= 4)
        {
            enumerator.MoveNext();
            JsonElement fourth = enumerator.Current;
            if (fourth.ValueKind == JsonValueKind.String)
            {
                string? strVal = fourth.GetString();
                if (VoxelColor.TryParseHex(strVal, out VoxelColor hexColor))
                {
                    record.Color = hexColor;
                }
                else
                {
                    record.MaterialId = strVal;
                }
            }
            else if (fourth.ValueKind == JsonValueKind.Number && fourth.TryGetInt32(out int paletteIndex))
            {
                string idStr = paletteIndex.ToString(CultureInfo.InvariantCulture);
                record.MaterialId = idStr;
                VoxelMaterialReference? mat = materials.FirstOrDefault(m => m.Id == idStr);
                if (mat?.Color is { } matColor)
                {
                    record.Color = matColor;
                }
            }
        }

        return true;
    }

    private static bool TryReadVoxelPosition(JsonElement obj, out Vector3D position)
    {
        if ((obj.TryGetProperty("position", out JsonElement posProp) ||
             obj.TryGetProperty("pos", out posProp)) &&
            TryParseVector3D(posProp, out position))
        {
            return true;
        }

        if (obj.TryGetProperty("x", out JsonElement xProp) &&
            obj.TryGetProperty("y", out JsonElement yProp) &&
            obj.TryGetProperty("z", out JsonElement zProp) &&
            TryReadFiniteDouble(xProp, out double x) &&
            TryReadFiniteDouble(yProp, out double y) &&
            TryReadFiniteDouble(zProp, out double z))
        {
            position = new Vector3D(x, y, z);
            return true;
        }

        position = default;
        return false;
    }

    private static bool TryReadVector3DProperty(JsonElement obj, string propertyName, out Vector3D vector)
    {
        if (obj.TryGetProperty(propertyName, out JsonElement prop) && TryParseVector3D(prop, out vector))
        {
            return true;
        }

        vector = default;
        return false;
    }

    internal static bool TryParseVector3D(JsonElement element, out Vector3D vector)
    {
        vector = default;
        if (element.ValueKind == JsonValueKind.Array)
        {
            if (element.GetArrayLength() != 3)
            {
                return false;
            }

            using JsonElement.ArrayEnumerator e = element.EnumerateArray();
            e.MoveNext();
            if (!TryReadFiniteDouble(e.Current, out double x)) return false;
            e.MoveNext();
            if (!TryReadFiniteDouble(e.Current, out double y)) return false;
            e.MoveNext();
            if (!TryReadFiniteDouble(e.Current, out double z)) return false;

            vector = new Vector3D(x, y, z);
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if ((element.TryGetProperty("x", out JsonElement xProp) || element.TryGetProperty("width", out xProp)) &&
                (element.TryGetProperty("y", out JsonElement yProp) || element.TryGetProperty("height", out yProp)) &&
                (element.TryGetProperty("z", out JsonElement zProp) || element.TryGetProperty("depth", out zProp)) &&
                TryReadFiniteDouble(xProp, out double x) &&
                TryReadFiniteDouble(yProp, out double y) &&
                TryReadFiniteDouble(zProp, out double z))
            {
                vector = new Vector3D(x, y, z);
                return true;
            }
        }

        return false;
    }

    internal static bool TryParseVector2D(JsonElement element, out Vector2D vector)
    {
        vector = default;
        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 2)
        {
            using JsonElement.ArrayEnumerator e = element.EnumerateArray();
            e.MoveNext();
            if (!TryReadFiniteDouble(e.Current, out double x)) return false;
            e.MoveNext();
            if (!TryReadFiniteDouble(e.Current, out double y)) return false;

            vector = new Vector2D(x, y);
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object &&
            (element.TryGetProperty("x", out JsonElement xProp) || element.TryGetProperty("width", out xProp)) &&
            (element.TryGetProperty("y", out JsonElement yProp) || element.TryGetProperty("height", out yProp)) &&
            TryReadFiniteDouble(xProp, out double ox) &&
            TryReadFiniteDouble(yProp, out double oy))
        {
            vector = new Vector2D(ox, oy);
            return true;
        }

        return false;
    }

    internal static bool TryParseVoxelColor(JsonElement element, out VoxelColor color)
    {
        color = VoxelColor.White;
        if (element.ValueKind == JsonValueKind.String)
        {
            return VoxelColor.TryParseHex(element.GetString(), out color);
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            int len = element.GetArrayLength();
            if (len is < 3 or > 4)
            {
                return false;
            }

            using JsonElement.ArrayEnumerator e = element.EnumerateArray();
            e.MoveNext();
            if (!TryReadFiniteDouble(e.Current, out double r)) return false;
            e.MoveNext();
            if (!TryReadFiniteDouble(e.Current, out double g)) return false;
            e.MoveNext();
            if (!TryReadFiniteDouble(e.Current, out double b)) return false;

            double a = 1.0;
            if (len == 4)
            {
                e.MoveNext();
                if (!TryReadFiniteDouble(e.Current, out a)) return false;
            }

            if (r > 1.0 || g > 1.0 || b > 1.0 || a > 1.0)
            {
                r /= 255.0;
                g /= 255.0;
                b /= 255.0;
                if (len == 4 && a > 1.0)
                {
                    a /= 255.0;
                }
            }

            color = new VoxelColor(r, g, b, a);
            return color.IsValid;
        }

        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("r", out JsonElement rProp) &&
            element.TryGetProperty("g", out JsonElement gProp) &&
            element.TryGetProperty("b", out JsonElement bProp) &&
            TryReadFiniteDouble(rProp, out double orVal) &&
            TryReadFiniteDouble(gProp, out double ogVal) &&
            TryReadFiniteDouble(bProp, out double obVal))
        {
            double oaVal = 1.0;
            if (element.TryGetProperty("a", out JsonElement aProp) && !TryReadFiniteDouble(aProp, out oaVal))
            {
                return false;
            }

            if (orVal > 1.0 || ogVal > 1.0 || obVal > 1.0 || oaVal > 1.0)
            {
                orVal /= 255.0;
                ogVal /= 255.0;
                obVal /= 255.0;
                if (oaVal > 1.0) oaVal /= 255.0;
            }

            color = new VoxelColor(orVal, ogVal, obVal, oaVal);
            return color.IsValid;
        }

        return false;
    }

    private static bool TryReadFiniteDouble(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value) && double.IsFinite(value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}
