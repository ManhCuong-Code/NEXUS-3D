using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Validation;

/// <summary>
/// Validates <see cref="BedrockCube"/> coordinates, dimensions, pivots, rotations, inflation, and UV mappings.
/// </summary>
public sealed class BedrockCubeValidator
{
    public void ValidateCube(
        BedrockCube cube,
        BedrockGeometryDescription description,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath,
        string cubePath)
    {
        ArgumentNullException.ThrowIfNull(cube);

        if (!cube.HasValidOrigin)
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.InvalidNumericValue,
                $"Cube at '{cubePath}' has non-finite 'origin' ({cube.Origin}).",
                IsError: true,
                FilePath: filePath,
                JsonPath: $"{cubePath}.origin"));
        }

        if (!cube.HasValidSize)
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.InvalidNumericValue,
                $"Cube at '{cubePath}' has invalid or negative 'size' ({cube.Size}).",
                IsError: true,
                FilePath: filePath,
                JsonPath: $"{cubePath}.size"));
        }

        if (cube.Pivot.HasValue && !cube.Pivot.Value.IsFinite())
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.InvalidNumericValue,
                $"Cube at '{cubePath}' has non-finite 'pivot' ({cube.Pivot.Value}).",
                IsError: true,
                FilePath: filePath,
                JsonPath: $"{cubePath}.pivot"));
        }

        if (cube.Rotation.HasValue && !cube.Rotation.Value.IsFinite())
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.InvalidNumericValue,
                $"Cube at '{cubePath}' has non-finite 'rotation' ({cube.Rotation.Value}).",
                IsError: true,
                FilePath: filePath,
                JsonPath: $"{cubePath}.rotation"));
        }

        if (cube.Inflate.HasValue && !double.IsFinite(cube.Inflate.Value))
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.InvalidNumericValue,
                $"Cube at '{cubePath}' has non-finite 'inflate' ({cube.Inflate.Value}).",
                IsError: true,
                FilePath: filePath,
                JsonPath: $"{cubePath}.inflate"));
        }

        if (cube.Uv is not null)
        {
            ValidateCubeUv(cube.Uv, description, options, diagnostics, filePath, $"{cubePath}.uv");
        }
    }

    private static void ValidateCubeUv(
        BedrockCubeUv uv,
        BedrockGeometryDescription description,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath,
        string uvPath)
    {
        if (uv.Mode == BedrockUvMode.BoxUv)
        {
            if (!uv.BoxUvOffset.IsFinite())
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    $"Cube Box UV at '{uvPath}' has non-finite coordinates ({uv.BoxUvOffset}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: uvPath));
            }
            else if (description.HasValidTextureDimensions &&
                     (uv.BoxUvOffset.X < 0 || uv.BoxUvOffset.Y < 0 ||
                      uv.BoxUvOffset.X > description.TextureWidth || uv.BoxUvOffset.Y > description.TextureHeight))
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    $"Cube Box UV ({uv.BoxUvOffset.X}, {uv.BoxUvOffset.Y}) exceeds texture bounds ({description.TextureWidth}x{description.TextureHeight}).",
                    IsError: false,
                    FilePath: filePath,
                    JsonPath: uvPath));
            }
        }
        else if (uv.Mode == BedrockUvMode.PerFaceUv)
        {
            foreach ((string faceKey, BedrockFaceUv faceUv) in uv.Faces)
            {
                string facePath = $"{uvPath}.{faceKey}";
                if (!faceUv.HasValidValues)
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Per-face UV '{faceKey}' at '{facePath}' contains non-finite coordinates.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: facePath));
                }
            }
        }
    }
}
