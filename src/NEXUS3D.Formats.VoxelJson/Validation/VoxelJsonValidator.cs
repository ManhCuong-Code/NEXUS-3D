using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Models;
using NEXUS3D.Formats.VoxelJson.Parsing;

namespace NEXUS3D.Formats.VoxelJson.Validation;

/// <summary>
/// Validates a <see cref="VoxelDocument"/> against structural, geometric, and referential constraints.
/// </summary>
public sealed class VoxelJsonValidator
{
    public IReadOnlyList<JsonFormatDiagnostic> Validate(
        VoxelDocument document,
        VoxelJsonReadOptions? options = null,
        string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= VoxelJsonReadOptions.Default;

        var diagnostics = new List<JsonFormatDiagnostic>();

        if (!document.Grid.Origin.IsFinite())
        {
            diagnostics.Add(new JsonFormatDiagnostic(
                JsonErrorCodes.InvalidCoordinate,
                "Voxel grid origin contains non-finite coordinates.",
                IsError: true,
                FilePath: filePath,
                JsonPath: "$.grid.origin"));
        }

        if (!document.Grid.HasValidVoxelSize)
        {
            diagnostics.Add(new JsonFormatDiagnostic(
                JsonErrorCodes.InvalidDimensions,
                $"Voxel grid cell size ({document.Grid.VoxelSize}) must be finite and strictly positive.",
                IsError: true,
                FilePath: filePath,
                JsonPath: "$.grid.voxelSize"));
        }

        if (!document.Grid.HasValidDimensions)
        {
            diagnostics.Add(new JsonFormatDiagnostic(
                JsonErrorCodes.InvalidDimensions,
                $"Voxel grid dimensions ({document.Grid.Dimensions}) must be finite and strictly positive.",
                IsError: true,
                FilePath: filePath,
                JsonPath: "$.grid.dimensions"));
        }

        if (document.Voxels.Count > options.MaxVoxelCount)
        {
            diagnostics.Add(new JsonFormatDiagnostic(
                JsonErrorCodes.LimitExceeded,
                $"Voxel count ({document.Voxels.Count}) exceeds maximum allowed ({options.MaxVoxelCount}).",
                IsError: true,
                FilePath: filePath,
                JsonPath: "$.voxels"));
        }

        var materialIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < document.Materials.Count; i++)
        {
            VoxelMaterialReference material = document.Materials[i];
            if (string.IsNullOrWhiteSpace(material.Id))
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidVoxelElement,
                    $"Material at index {i} has an empty id.",
                    IsError: options.StrictMode,
                    FilePath: filePath,
                    JsonPath: $"$.materials[{i}].id"));
                continue;
            }

            if (!materialIds.Add(material.Id))
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidVoxelElement,
                    $"Duplicate material id '{material.Id}' at index {i}.",
                    IsError: options.StrictMode,
                    FilePath: filePath,
                    JsonPath: $"$.materials[{i}].id"));
            }

            if (material.Color.HasValue && !material.Color.Value.IsValid)
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidVoxelElement,
                    $"Material '{material.Id}' has an invalid RGBA color ({material.Color.Value}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"$.materials[{i}].color"));
            }
        }

        var layerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < document.Layers.Count; i++)
        {
            VoxelLayerDefinition layer = document.Layers[i];
            if (string.IsNullOrWhiteSpace(layer.Id))
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidVoxelElement,
                    $"Layer at index {i} has an empty id.",
                    IsError: options.StrictMode,
                    FilePath: filePath,
                    JsonPath: $"$.layers[{i}].id"));
                continue;
            }

            layerIds.Add(layer.Id);
        }

        var seenCoordinates = new Dictionary<(long X, long Y, long Z, string Layer), int>();

        for (int i = 0; i < document.Voxels.Count; i++)
        {
            VoxelRecord voxel = document.Voxels[i];
            string itemPath = $"$.voxels[{i}]";

            if (!voxel.HasValidPosition)
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidCoordinate,
                    $"Voxel at index {i} has non-finite position ({voxel.Position}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{itemPath}.position"));
            }

            if (!voxel.HasValidSize)
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidDimensions,
                    $"Voxel at index {i} has invalid or non-positive size ({voxel.Size}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{itemPath}.size"));
            }

            if (voxel.Rotation.HasValue && !voxel.Rotation.Value.IsFinite())
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidCoordinate,
                    $"Voxel at index {i} has non-finite rotation ({voxel.Rotation.Value}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{itemPath}.rotation"));
            }

            if (voxel.Color.HasValue && !voxel.Color.Value.IsValid)
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.InvalidVoxelElement,
                    $"Voxel at index {i} has an invalid RGBA color ({voxel.Color.Value}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{itemPath}.color"));
            }

            if (!string.IsNullOrWhiteSpace(voxel.MaterialId) &&
                materialIds.Count > 0 &&
                !materialIds.Contains(voxel.MaterialId))
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.BrokenReference,
                    $"Voxel at index {i} references unknown material '{voxel.MaterialId}'.",
                    IsError: options.StrictMode,
                    FilePath: filePath,
                    JsonPath: $"{itemPath}.materialId"));
            }

            if (!string.IsNullOrWhiteSpace(voxel.LayerId) &&
                layerIds.Count > 0 &&
                !layerIds.Contains(voxel.LayerId))
            {
                diagnostics.Add(new JsonFormatDiagnostic(
                    JsonErrorCodes.BrokenReference,
                    $"Voxel at index {i} references unknown layer '{voxel.LayerId}'.",
                    IsError: options.StrictMode,
                    FilePath: filePath,
                    JsonPath: $"{itemPath}.layerId"));
            }

            if (voxel.HasValidPosition)
            {
                var key = (
                    (long)Math.Round(voxel.Position.X * 1_000_000.0),
                    (long)Math.Round(voxel.Position.Y * 1_000_000.0),
                    (long)Math.Round(voxel.Position.Z * 1_000_000.0),
                    voxel.LayerId ?? string.Empty);

                if (seenCoordinates.TryGetValue(key, out int previousIndex))
                {
                    bool isError = options.DuplicatePolicy == DuplicateVoxelPolicy.RejectWithError;
                    diagnostics.Add(new JsonFormatDiagnostic(
                        JsonErrorCodes.DuplicateVoxel,
                        $"Duplicate voxel coordinate {voxel.Position} on layer '{voxel.LayerId ?? "default"}' at index {i} (previously at index {previousIndex}).",
                        IsError: isError,
                        FilePath: filePath,
                        JsonPath: itemPath));
                }
                else
                {
                    seenCoordinates[key] = i;
                }
            }
        }

        return diagnostics;
    }

    public static List<VoxelRecord> ApplyDuplicatePolicy(
        IReadOnlyList<VoxelRecord> rawVoxels,
        DuplicateVoxelPolicy policy,
        List<JsonFormatDiagnostic> diagnostics,
        string? filePath,
        out int duplicateCount)
    {
        duplicateCount = 0;
        if (rawVoxels.Count <= 1)
        {
            return [.. rawVoxels];
        }

        var indexByCoord = new Dictionary<(long X, long Y, long Z, string Layer), int>();
        var result = new List<VoxelRecord>(rawVoxels.Count);

        for (int i = 0; i < rawVoxels.Count; i++)
        {
            VoxelRecord voxel = rawVoxels[i];
            if (!voxel.HasValidPosition)
            {
                result.Add(voxel);
                continue;
            }

            var key = (
                (long)Math.Round(voxel.Position.X * 1_000_000.0),
                (long)Math.Round(voxel.Position.Y * 1_000_000.0),
                (long)Math.Round(voxel.Position.Z * 1_000_000.0),
                voxel.LayerId ?? string.Empty);

            if (!indexByCoord.TryGetValue(key, out int existingListIndex))
            {
                indexByCoord[key] = result.Count;
                result.Add(voxel);
                continue;
            }

            duplicateCount++;
            bool isError = policy == DuplicateVoxelPolicy.RejectWithError;
            string policyAction = policy switch
            {
                DuplicateVoxelPolicy.KeepAll => "Keeping all duplicate entries.",
                DuplicateVoxelPolicy.KeepLastWithWarning => "Keeping last entry.",
                DuplicateVoxelPolicy.KeepFirstWithWarning => "Keeping first entry.",
                DuplicateVoxelPolicy.RejectWithError => "Duplicate coordinates rejected by policy.",
                _ => string.Empty
            };

            diagnostics.Add(new JsonFormatDiagnostic(
                JsonErrorCodes.DuplicateVoxel,
                $"Duplicate voxel coordinate ({voxel.Position.X}, {voxel.Position.Y}, {voxel.Position.Z}) at index {i}. {policyAction}",
                IsError: isError,
                FilePath: filePath,
                JsonPath: $"$.voxels[{i}]"));

            switch (policy)
            {
                case DuplicateVoxelPolicy.KeepAll:
                case DuplicateVoxelPolicy.RejectWithError:
                    result.Add(voxel);
                    break;
                case DuplicateVoxelPolicy.KeepLastWithWarning:
                    result[existingListIndex] = voxel;
                    break;
                case DuplicateVoxelPolicy.KeepFirstWithWarning:
                    break;
            }
        }

        return result;
    }
}
