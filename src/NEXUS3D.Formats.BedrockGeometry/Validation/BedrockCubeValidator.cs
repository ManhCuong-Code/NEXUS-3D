using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Validation;

/// <summary>
/// Validates <see cref="BedrockCube"/> coordinates, dimensions, pivots, rotations, and inflation values.
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
    }
}
