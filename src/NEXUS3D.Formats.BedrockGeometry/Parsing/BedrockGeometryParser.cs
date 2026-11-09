using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Validation;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Parsing;

/// <summary>
/// Parses Minecraft Bedrock Geometry (<c>.geo.json</c>) documents across Legacy (<c>1.8.0</c>, <c>1.10.0</c>),
/// Intermediate (<c>1.12.0</c>), and Modern (<c>1.16.0</c>, <c>1.19.0</c>, <c>1.21.0</c>) schema versions.
/// </summary>
public sealed class BedrockGeometryParser
{
    private static readonly HashSet<string> ModernRootKnownProperties = new(StringComparer.Ordinal)
    {
        "format_version", "minecraft:geometry"
    };

    private static readonly HashSet<string> ModernGeometryKnownProperties = new(StringComparer.Ordinal)
    {
        "description", "bones", "cape"
    };

    private static readonly HashSet<string> ModernDescriptionKnownProperties = new(StringComparer.Ordinal)
    {
        "identifier", "texture_width", "texture_height",
        "visible_bounds_width", "visible_bounds_height", "visible_bounds_offset"
    };

    private static readonly HashSet<string> LegacyGeometryKnownProperties = new(StringComparer.Ordinal)
    {
        "texturewidth", "textureheight", "texture_width", "texture_height",
        "visible_bounds_width", "visible_bounds_height", "visible_bounds_offset",
        "bones", "cape"
    };

    private static readonly HashSet<string> BoneKnownProperties = new(StringComparer.Ordinal)
    {
        "name", "parent", "pivot", "rotation", "bind_pose_rotation", "mirror",
        "inflate", "debug", "render_group_id", "binding", "material", "neverRender", "reset",
        "cubes"
    };

    private static readonly HashSet<string> CubeKnownProperties = new(StringComparer.Ordinal)
    {
        "origin", "size", "pivot", "rotation", "inflate", "mirror"
    };

    private readonly BedrockGeometryValidator _validator = new();

    public BedrockGeometryParseResult ParseFile(string filePath, BedrockGeometryReadOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Parse(stream, options, filePath);
    }

    public BedrockGeometryParseResult Parse(string jsonText, BedrockGeometryReadOptions? options = null, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(jsonText);
        byte[] bytes = Encoding.UTF8.GetBytes(jsonText);
        return Parse(bytes.AsMemory(), options, filePath);
    }

    public BedrockGeometryParseResult Parse(Stream stream, BedrockGeometryReadOptions? options = null, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        options ??= BedrockGeometryReadOptions.Default;

        var sw = Stopwatch.StartNew();
        using JsonDocument jsonDoc = BedrockJsonExtensionHelpers.ParseDocument(stream, options, filePath);
        return ParseDocumentCore(jsonDoc.RootElement, options, filePath, sw);
    }

    public BedrockGeometryParseResult Parse(ReadOnlyMemory<byte> utf8Bytes, BedrockGeometryReadOptions? options = null, string? filePath = null)
    {
        options ??= BedrockGeometryReadOptions.Default;

        var sw = Stopwatch.StartNew();
        using JsonDocument jsonDoc = BedrockJsonExtensionHelpers.ParseDocument(utf8Bytes, options, filePath);
        return ParseDocumentCore(jsonDoc.RootElement, options, filePath, sw);
    }

    private BedrockGeometryParseResult ParseDocumentCore(
        JsonElement root,
        BedrockGeometryReadOptions options,
        string? filePath,
        Stopwatch sw)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.EmptyOrInvalidRoot,
                "Bedrock Geometry JSON root must be a JSON object.",
                "$");
        }

        if (BedrockGeometryDetector.IsBedrockAnimationDocument(root))
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.MisplacedAnimationDocument,
                "The JSON document is a Minecraft Bedrock Animation file ('animations'), not a Bedrock Geometry (.geo.json) model.",
                "$.animations");
        }

        if (root.TryGetProperty("minecraft:geometry", out JsonElement rawGeoProp) &&
            rawGeoProp.ValueKind != JsonValueKind.Array)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.TypeMismatch,
                "Property 'minecraft:geometry' must be a JSON array.",
                "$['minecraft:geometry']");
        }

        if (!BedrockGeometryDetector.IsBedrockGeometryDocument(
                root,
                out BedrockGeometrySchemaFamily schemaFamily,
                out string formatVersion))
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.UnsupportedSchemaOrDialect,
                "Unrecognized Bedrock Geometry JSON structure. Expected 'minecraft:geometry' array or legacy 'geometry.<name>' root objects.",
                "$");
        }

        var diagnostics = new List<BedrockGeometryDiagnostic>();
        var document = new BedrockGeometryDocument
        {
            FormatVersion = formatVersion,
            SchemaFamily = schemaFamily
        };

        if (schemaFamily == BedrockGeometrySchemaFamily.Legacy180)
        {
            ParseLegacyDocument(root, document, options, diagnostics, filePath);
        }
        else
        {
            ParseModernOrIntermediateDocument(root, document, options, diagnostics, filePath);
        }

        IReadOnlyList<BedrockGeometryDiagnostic> validationDiagnostics = _validator.Validate(document, options, filePath);
        foreach (BedrockGeometryDiagnostic diag in validationDiagnostics)
        {
            if (!diagnostics.Any(existing => existing.Code == diag.Code && existing.JsonPath == diag.JsonPath && existing.Message == diag.Message))
            {
                diagnostics.Add(diag);
            }
        }

        if (options.StrictMode)
        {
            BedrockGeometryDiagnostic? firstError = diagnostics.FirstOrDefault(d => d.IsError);
            if (firstError is not null)
            {
                throw new JsonFormatException(
                    firstError.Code,
                    firstError.Message,
                    firstError.JsonPath);
            }
        }

        sw.Stop();
        return new BedrockGeometryParseResult
        {
            Document = document,
            Diagnostics = diagnostics,
            Elapsed = sw.Elapsed
        };
    }

    private static void ParseLegacyDocument(
        JsonElement root,
        BedrockGeometryDocument document,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath)
    {
        int geoCount = 0;
        foreach (JsonProperty prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, "format_version", StringComparison.Ordinal))
            {
                continue;
            }

            if (!prop.Name.StartsWith("geometry.", StringComparison.OrdinalIgnoreCase))
            {
                if (options.PreserveExtensionData)
                {
                    document.RootExtensionData.Set(prop.Name, prop.Value);
                }

                continue;
            }

            if (geoCount >= options.MaxGeometryCount)
            {
                throw new JsonFormatException(
                    JsonFormatErrorCodes.LimitExceeded,
                    $"Geometry count exceeds maximum allowed ({options.MaxGeometryCount}).",
                    $"$.{prop.Name}");
            }

            string geoPath = $"$['{prop.Name}']";
            if (prop.Value.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.TypeMismatch,
                    $"Legacy geometry entry '{prop.Name}' must be a JSON object.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: geoPath));
                continue;
            }

            string identifier = prop.Name;
            string? parentIdentifier = null;
            int colonIdx = prop.Name.IndexOf(':', StringComparison.Ordinal);
            if (colonIdx > 0 && colonIdx < prop.Name.Length - 1)
            {
                identifier = prop.Name[..colonIdx].Trim();
                parentIdentifier = prop.Name[(colonIdx + 1)..].Trim();
            }

            var geometry = new Models.BedrockGeometry
            {
                ParentGeometryIdentifier = parentIdentifier,
                Description = new BedrockGeometryDescription
                {
                    Identifier = identifier
                }
            };

            JsonElement geoObj = prop.Value;

            if ((geoObj.TryGetProperty("texturewidth", out JsonElement twProp) ||
                 geoObj.TryGetProperty("texture_width", out twProp)))
            {
                if (twProp.ValueKind == JsonValueKind.Number && twProp.TryGetInt32(out int tw) && tw > 0)
                {
                    geometry.Description.TextureWidth = tw;
                }
                else
                {
                    geometry.Description.TextureWidth = -1;
                }
            }

            if ((geoObj.TryGetProperty("textureheight", out JsonElement thProp) ||
                 geoObj.TryGetProperty("texture_height", out thProp)))
            {
                if (thProp.ValueKind == JsonValueKind.Number && thProp.TryGetInt32(out int th) && th > 0)
                {
                    geometry.Description.TextureHeight = th;
                }
                else
                {
                    geometry.Description.TextureHeight = -1;
                }
            }

            ParseVisibleBounds(geoObj, geometry.Description, diagnostics, filePath, geoPath);

            if (geoObj.TryGetProperty("cape", out JsonElement capeProp) && capeProp.ValueKind == JsonValueKind.String)
            {
                geometry.Cape = capeProp.GetString();
            }

            if (geoObj.TryGetProperty("bones", out JsonElement bonesProp))
            {
                if (bonesProp.ValueKind == JsonValueKind.Array)
                {
                    ParseBonesArray(bonesProp, geometry, options, diagnostics, filePath, $"{geoPath}.bones");
                }
                else
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.TypeMismatch,
                        $"Property 'bones' in '{identifier}' must be a JSON array.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{geoPath}.bones"));
                }
            }

            if (options.PreserveExtensionData)
            {
                geometry.ExtensionData.CaptureUnknownProperties(geoObj, LegacyGeometryKnownProperties);
            }

            document.Geometries.Add(geometry);
            geoCount++;
        }
    }

    private static void ParseModernOrIntermediateDocument(
        JsonElement root,
        BedrockGeometryDocument document,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath)
    {
        if (options.PreserveExtensionData)
        {
            document.RootExtensionData.CaptureUnknownProperties(root, ModernRootKnownProperties);
        }

        if (!root.TryGetProperty("minecraft:geometry", out JsonElement geoArray) ||
            geoArray.ValueKind != JsonValueKind.Array)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.MissingRequiredProperty,
                "Bedrock Geometry document is missing required 'minecraft:geometry' array.",
                "$['minecraft:geometry']");
        }

        int index = 0;
        foreach (JsonElement geoObj in geoArray.EnumerateArray())
        {
            if (index >= options.MaxGeometryCount)
            {
                throw new JsonFormatException(
                    JsonFormatErrorCodes.LimitExceeded,
                    $"Geometry count exceeds maximum allowed ({options.MaxGeometryCount}).",
                    $"$['minecraft:geometry'][{index}]");
            }

            string geoPath = $"$['minecraft:geometry'][{index}]";
            if (geoObj.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.TypeMismatch,
                    $"Geometry entry at index {index} must be a JSON object.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: geoPath));
                index++;
                continue;
            }

            var geometry = new Models.BedrockGeometry();
            if (geoObj.TryGetProperty("description", out JsonElement descObj) &&
                descObj.ValueKind == JsonValueKind.Object)
            {
                ParseDescriptionObject(descObj, geometry.Description, options, diagnostics, filePath, $"{geoPath}.description");
            }
            else
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.MissingRequiredProperty,
                    $"Geometry entry at index {index} is missing required 'description' object.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{geoPath}.description"));
            }

            if (geoObj.TryGetProperty("cape", out JsonElement capeProp) && capeProp.ValueKind == JsonValueKind.String)
            {
                geometry.Cape = capeProp.GetString();
            }

            if (geoObj.TryGetProperty("bones", out JsonElement bonesProp))
            {
                if (bonesProp.ValueKind == JsonValueKind.Array)
                {
                    ParseBonesArray(bonesProp, geometry, options, diagnostics, filePath, $"{geoPath}.bones");
                }
                else
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.TypeMismatch,
                        $"Property 'bones' at '{geoPath}.bones' must be a JSON array.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{geoPath}.bones"));
                }
            }

            if (options.PreserveExtensionData)
            {
                geometry.ExtensionData.CaptureUnknownProperties(geoObj, ModernGeometryKnownProperties);
            }

            document.Geometries.Add(geometry);
            index++;
        }
    }

    private static void ParseBonesArray(
        JsonElement bonesArray,
        Models.BedrockGeometry geometry,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath,
        string bonesPath)
    {
        int boneIdx = 0;
        foreach (JsonElement boneElem in bonesArray.EnumerateArray())
        {
            if (boneIdx >= options.MaxBonesPerGeometry)
            {
                throw new JsonFormatException(
                    JsonFormatErrorCodes.LimitExceeded,
                    $"Bone count exceeds maximum allowed ({options.MaxBonesPerGeometry}).",
                    $"{bonesPath}[{boneIdx}]");
            }

            geometry.RawBoneElements.Add(boneElem.Clone());
            string bonePath = $"{bonesPath}[{boneIdx}]";

            if (boneElem.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.TypeMismatch,
                    $"Bone at '{bonePath}' must be a JSON object.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: bonePath));
                boneIdx++;
                continue;
            }

            var bone = new BedrockBone();

            if (boneElem.TryGetProperty("name", out JsonElement nameProp) &&
                nameProp.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(nameProp.GetString()))
            {
                bone.Name = nameProp.GetString()!.Trim();
            }
            else
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.MissingRequiredProperty,
                    $"Bone at '{bonePath}' is missing a valid non-empty 'name'.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.name"));
            }

            if (boneElem.TryGetProperty("parent", out JsonElement parentProp))
            {
                if (parentProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(parentProp.GetString()))
                {
                    bone.Parent = parentProp.GetString()!.Trim();
                }
                else if (parentProp.ValueKind != JsonValueKind.Null)
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.TypeMismatch,
                        $"Bone '{bone.Name}' property 'parent' must be a non-empty string.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{bonePath}.parent"));
                }
            }

            if (boneElem.TryGetProperty("pivot", out JsonElement pivotProp))
            {
                if (TryParseVector3D(pivotProp, out Vector3D pivot))
                {
                    bone.Pivot = pivot;
                }
                else
                {
                    bone.Pivot = new Vector3D(double.NaN, 0, 0);
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Bone '{bone.Name}' property 'pivot' must be a 3-number array [x, y, z].",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{bonePath}.pivot"));
                }
            }

            if (boneElem.TryGetProperty("rotation", out JsonElement rotProp))
            {
                if (TryParseVector3D(rotProp, out Vector3D rot))
                {
                    bone.Rotation = rot;
                }
                else
                {
                    bone.Rotation = new Vector3D(double.NaN, 0, 0);
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Bone '{bone.Name}' property 'rotation' must be a 3-number array [x, y, z].",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{bonePath}.rotation"));
                }
            }

            if (boneElem.TryGetProperty("bind_pose_rotation", out JsonElement bindRotProp))
            {
                if (TryParseVector3D(bindRotProp, out Vector3D bindRot))
                {
                    bone.BindPoseRotation = bindRot;
                }
                else
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Bone '{bone.Name}' property 'bind_pose_rotation' must be a 3-number array [x, y, z].",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{bonePath}.bind_pose_rotation"));
                }
            }

            if (boneElem.TryGetProperty("mirror", out JsonElement mirrorProp) &&
                (mirrorProp.ValueKind == JsonValueKind.True || mirrorProp.ValueKind == JsonValueKind.False))
            {
                bone.Mirror = mirrorProp.GetBoolean();
            }

            if (boneElem.TryGetProperty("inflate", out JsonElement inflateProp))
            {
                if (TryReadFiniteDouble(inflateProp, out double inflate))
                {
                    bone.Inflate = inflate;
                }
                else
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Bone '{bone.Name}' property 'inflate' must be a finite number.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{bonePath}.inflate"));
                }
            }

            if (boneElem.TryGetProperty("debug", out JsonElement debugProp) &&
                (debugProp.ValueKind == JsonValueKind.True || debugProp.ValueKind == JsonValueKind.False))
            {
                bone.Debug = debugProp.GetBoolean();
            }

            if (boneElem.TryGetProperty("render_group_id", out JsonElement rgProp) &&
                rgProp.ValueKind == JsonValueKind.Number &&
                rgProp.TryGetInt32(out int rgId))
            {
                bone.RenderGroupId = rgId;
            }

            if (boneElem.TryGetProperty("binding", out JsonElement bindingProp) &&
                bindingProp.ValueKind == JsonValueKind.String)
            {
                bone.Binding = bindingProp.GetString();
            }

            if (boneElem.TryGetProperty("material", out JsonElement matProp) &&
                matProp.ValueKind == JsonValueKind.String)
            {
                bone.Material = matProp.GetString();
            }

            if (boneElem.TryGetProperty("neverRender", out JsonElement nrProp) &&
                (nrProp.ValueKind == JsonValueKind.True || nrProp.ValueKind == JsonValueKind.False))
            {
                bone.NeverRender = nrProp.GetBoolean();
            }

            if (boneElem.TryGetProperty("reset", out JsonElement resetProp) &&
                (resetProp.ValueKind == JsonValueKind.True || resetProp.ValueKind == JsonValueKind.False))
            {
                bone.Reset = resetProp.GetBoolean();
            }

            if (boneElem.TryGetProperty("cubes", out JsonElement cubesProp))
            {
                if (cubesProp.ValueKind == JsonValueKind.Array)
                {
                    ParseCubesArray(cubesProp, bone, options, diagnostics, filePath, $"{bonePath}.cubes");
                }
                else
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.TypeMismatch,
                        $"Property 'cubes' on bone '{bone.Name}' must be a JSON array.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{bonePath}.cubes"));
                }
            }

            if (options.PreserveExtensionData)
            {
                bone.ExtensionData.CaptureUnknownProperties(boneElem, BoneKnownProperties);
            }

            geometry.Bones.Add(bone);
            boneIdx++;
        }
    }

    private static void ParseCubesArray(
        JsonElement cubesArray,
        BedrockBone bone,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath,
        string cubesPath)
    {
        int cubeIdx = 0;
        foreach (JsonElement cubeElem in cubesArray.EnumerateArray())
        {
            if (cubeIdx >= options.MaxCubesPerBone)
            {
                throw new JsonFormatException(
                    JsonFormatErrorCodes.LimitExceeded,
                    $"Cube count on bone '{bone.Name}' exceeds maximum allowed ({options.MaxCubesPerBone}).",
                    $"{cubesPath}[{cubeIdx}]");
            }

            string cubePath = $"{cubesPath}[{cubeIdx}]";
            if (cubeElem.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.TypeMismatch,
                    $"Cube at '{cubePath}' must be a JSON object.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: cubePath));
                cubeIdx++;
                continue;
            }

            var cube = new BedrockCube();

            if (cubeElem.TryGetProperty("origin", out JsonElement originProp))
            {
                if (TryParseVector3D(originProp, out Vector3D origin))
                {
                    cube.Origin = origin;
                }
                else
                {
                    cube.Origin = new Vector3D(double.NaN, 0, 0);
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Cube at '{cubePath}' has invalid 'origin'; expected [x, y, z].",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{cubePath}.origin"));
                }
            }
            else
            {
                cube.Origin = new Vector3D(double.NaN, 0, 0);
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.MissingRequiredProperty,
                    $"Cube at '{cubePath}' is missing required 'origin'.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{cubePath}.origin"));
            }

            if (cubeElem.TryGetProperty("size", out JsonElement sizeProp))
            {
                if (TryParseVector3D(sizeProp, out Vector3D size))
                {
                    cube.Size = size;
                }
                else
                {
                    cube.Size = new Vector3D(-1, -1, -1);
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Cube at '{cubePath}' has invalid 'size'; expected [w, h, d].",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{cubePath}.size"));
                }
            }
            else
            {
                cube.Size = new Vector3D(-1, -1, -1);
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.MissingRequiredProperty,
                    $"Cube at '{cubePath}' is missing required 'size'.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{cubePath}.size"));
            }

            if (cubeElem.TryGetProperty("pivot", out JsonElement pivotProp))
            {
                if (TryParseVector3D(pivotProp, out Vector3D pivot))
                {
                    cube.Pivot = pivot;
                }
                else
                {
                    cube.Pivot = new Vector3D(double.NaN, 0, 0);
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Cube at '{cubePath}' has invalid 'pivot'; expected [x, y, z].",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{cubePath}.pivot"));
                }
            }

            if (cubeElem.TryGetProperty("rotation", out JsonElement rotProp))
            {
                if (TryParseVector3D(rotProp, out Vector3D rot))
                {
                    cube.Rotation = rot;
                }
                else
                {
                    cube.Rotation = new Vector3D(double.NaN, 0, 0);
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Cube at '{cubePath}' has invalid 'rotation'; expected [x, y, z].",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{cubePath}.rotation"));
                }
            }

            if (cubeElem.TryGetProperty("inflate", out JsonElement inflateProp))
            {
                if (TryReadFiniteDouble(inflateProp, out double inflate))
                {
                    cube.Inflate = inflate;
                }
                else
                {
                    cube.Inflate = double.NaN;
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Cube at '{cubePath}' has non-finite 'inflate'.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{cubePath}.inflate"));
                }
            }

            if (cubeElem.TryGetProperty("mirror", out JsonElement mirrorProp) &&
                (mirrorProp.ValueKind == JsonValueKind.True || mirrorProp.ValueKind == JsonValueKind.False))
            {
                cube.Mirror = mirrorProp.GetBoolean();
            }

            if (options.PreserveExtensionData)
            {
                cube.ExtensionData.CaptureUnknownProperties(cubeElem, CubeKnownProperties);
            }

            bone.Cubes.Add(cube);
            cubeIdx++;
        }
    }

    private static void ParseDescriptionObject(
        JsonElement descObj,
        BedrockGeometryDescription description,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        if (descObj.TryGetProperty("identifier", out JsonElement idProp) &&
            idProp.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(idProp.GetString()))
        {
            description.Identifier = idProp.GetString()!.Trim();
        }
        else
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.MissingRequiredProperty,
                "Geometry description is missing a non-empty 'identifier'.",
                IsError: true,
                FilePath: filePath,
                JsonPath: $"{jsonPath}.identifier"));
        }

        if (descObj.TryGetProperty("texture_width", out JsonElement twProp))
        {
            if (twProp.ValueKind == JsonValueKind.Number && twProp.TryGetInt32(out int tw) && tw > 0)
            {
                description.TextureWidth = tw;
            }
            else
            {
                description.TextureWidth = -1;
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Geometry 'texture_width' must be a positive integer.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.texture_width"));
            }
        }

        if (descObj.TryGetProperty("texture_height", out JsonElement thProp))
        {
            if (thProp.ValueKind == JsonValueKind.Number && thProp.TryGetInt32(out int th) && th > 0)
            {
                description.TextureHeight = th;
            }
            else
            {
                description.TextureHeight = -1;
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Geometry 'texture_height' must be a positive integer.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.texture_height"));
            }
        }

        ParseVisibleBounds(descObj, description, diagnostics, filePath, jsonPath);

        if (options.PreserveExtensionData)
        {
            description.ExtensionData.CaptureUnknownProperties(descObj, ModernDescriptionKnownProperties);
        }
    }

    private static void ParseVisibleBounds(
        JsonElement obj,
        BedrockGeometryDescription description,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath,
        string jsonPath)
    {
        if (obj.TryGetProperty("visible_bounds_width", out JsonElement vbwProp))
        {
            if (vbwProp.ValueKind == JsonValueKind.Number && vbwProp.TryGetDouble(out double vbw) && double.IsFinite(vbw))
            {
                description.VisibleBoundsWidth = vbw;
            }
            else
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Property 'visible_bounds_width' must be a finite number.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.visible_bounds_width"));
            }
        }

        if (obj.TryGetProperty("visible_bounds_height", out JsonElement vbhProp))
        {
            if (vbhProp.ValueKind == JsonValueKind.Number && vbhProp.TryGetDouble(out double vbh) && double.IsFinite(vbh))
            {
                description.VisibleBoundsHeight = vbh;
            }
            else
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Property 'visible_bounds_height' must be a finite number.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.visible_bounds_height"));
            }
        }

        if (obj.TryGetProperty("visible_bounds_offset", out JsonElement vboProp))
        {
            if (TryParseVector3D(vboProp, out Vector3D vbo))
            {
                description.VisibleBoundsOffset = vbo;
            }
            else
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    "Property 'visible_bounds_offset' must be a 3-number array [x, y, z].",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{jsonPath}.visible_bounds_offset"));
            }
        }
    }

    internal static bool TryParseVector3D(JsonElement element, out Vector3D vector)
    {
        vector = default;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 3)
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

    internal static bool TryParseVector2D(JsonElement element, out Vector2D vector)
    {
        vector = default;
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 2)
        {
            return false;
        }

        using JsonElement.ArrayEnumerator e = element.EnumerateArray();
        e.MoveNext();
        if (!TryReadFiniteDouble(e.Current, out double u)) return false;
        e.MoveNext();
        if (!TryReadFiniteDouble(e.Current, out double v)) return false;

        vector = new Vector2D(u, v);
        return true;
    }

    internal static bool TryReadFiniteDouble(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value) && double.IsFinite(value))
        {
            return true;
        }

        value = 0.0;
        return false;
    }
}
