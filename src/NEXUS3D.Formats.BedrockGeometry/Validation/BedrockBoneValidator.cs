using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Validation;

/// <summary>
/// Validates <see cref="BedrockBone"/> properties, parent references, duplicate bone names, parent-child cycles,
/// locators, and poly_mesh entries.
/// </summary>
public sealed class BedrockBoneValidator
{
    private readonly BedrockCubeValidator _cubeValidator = new();

    public void ValidateBones(
        IReadOnlyList<BedrockBone> bones,
        BedrockGeometryDescription description,
        BedrockGeometryReadOptions options,
        List<BedrockGeometryDiagnostic> diagnostics,
        string? filePath,
        string bonesArrayPath)
    {
        ArgumentNullException.ThrowIfNull(bones);

        if (bones.Count > options.MaxBonesPerGeometry)
        {
            diagnostics.Add(new BedrockGeometryDiagnostic(
                JsonFormatErrorCodes.LimitExceeded,
                $"Bone count ({bones.Count}) exceeds maximum allowed ({options.MaxBonesPerGeometry}).",
                IsError: true,
                FilePath: filePath,
                JsonPath: bonesArrayPath));
        }

        var boneByName = new Dictionary<string, (BedrockBone Bone, int Index)>(StringComparer.Ordinal);

        for (int i = 0; i < bones.Count; i++)
        {
            BedrockBone bone = bones[i];
            string bonePath = $"{bonesArrayPath}[{i}]";

            if (string.IsNullOrWhiteSpace(bone.Name))
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.MissingRequiredProperty,
                    $"Bone at index {i} has an empty or missing 'name'.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.name"));
            }
            else if (!boneByName.TryAdd(bone.Name, (bone, i)))
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.DuplicateIdentifierOrCoordinate,
                    $"Duplicate bone name '{bone.Name}' at index {i}.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.name"));
            }

            if (!bone.Pivot.IsFinite())
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    $"Bone '{bone.Name}' has non-finite 'pivot' ({bone.Pivot}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.pivot"));
            }

            if (!bone.Rotation.IsFinite())
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    $"Bone '{bone.Name}' has non-finite 'rotation' ({bone.Rotation}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.rotation"));
            }

            if (bone.BindPoseRotation.HasValue && !bone.BindPoseRotation.Value.IsFinite())
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    $"Bone '{bone.Name}' has non-finite 'bind_pose_rotation' ({bone.BindPoseRotation.Value}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.bind_pose_rotation"));
            }

            if (bone.Inflate.HasValue && !double.IsFinite(bone.Inflate.Value))
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.InvalidNumericValue,
                    $"Bone '{bone.Name}' has non-finite 'inflate' ({bone.Inflate.Value}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.inflate"));
            }

            if (bone.Cubes.Count > options.MaxCubesPerBone)
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.LimitExceeded,
                    $"Bone '{bone.Name}' cube count ({bone.Cubes.Count}) exceeds maximum allowed ({options.MaxCubesPerBone}).",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.cubes"));
            }

            for (int c = 0; c < bone.Cubes.Count; c++)
            {
                _cubeValidator.ValidateCube(
                    bone.Cubes[c],
                    description,
                    options,
                    diagnostics,
                    filePath,
                    $"{bonePath}.cubes[{c}]");
            }

            foreach ((string locName, BedrockLocator locator) in bone.Locators)
            {
                if (!locator.HasValidCoordinates)
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.InvalidNumericValue,
                        $"Locator '{locName}' on bone '{bone.Name}' has non-finite offset or rotation.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{bonePath}.locators['{locName}']"));
                }
            }

            if (bone.PolyMesh is { } polyMesh)
            {
                for (int p = 0; p < polyMesh.Positions.Count; p++)
                {
                    if (!polyMesh.Positions[p].IsFinite())
                    {
                        diagnostics.Add(new BedrockGeometryDiagnostic(
                            JsonFormatErrorCodes.InvalidNumericValue,
                            $"PolyMesh position at index {p} on bone '{bone.Name}' is non-finite.",
                            IsError: true,
                            FilePath: filePath,
                            JsonPath: $"{bonePath}.poly_mesh.positions[{p}]"));
                    }
                }
            }
        }

        // Validate parent references and detect parent-child cycles
        for (int i = 0; i < bones.Count; i++)
        {
            BedrockBone bone = bones[i];
            if (string.IsNullOrWhiteSpace(bone.Parent))
            {
                continue;
            }

            string bonePath = $"{bonesArrayPath}[{i}]";

            if (string.Equals(bone.Parent, bone.Name, StringComparison.Ordinal))
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.BrokenReferenceOrHierarchy,
                    $"Bone '{bone.Name}' cannot reference itself as parent.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.parent"));
                continue;
            }

            if (!boneByName.ContainsKey(bone.Parent))
            {
                diagnostics.Add(new BedrockGeometryDiagnostic(
                    JsonFormatErrorCodes.BrokenReferenceOrHierarchy,
                    $"Bone '{bone.Name}' references non-existent parent bone '{bone.Parent}'.",
                    IsError: true,
                    FilePath: filePath,
                    JsonPath: $"{bonePath}.parent"));
                continue;
            }

            var visited = new HashSet<string>(StringComparer.Ordinal) { bone.Name };
            string? currentParent = bone.Parent;

            while (!string.IsNullOrWhiteSpace(currentParent))
            {
                if (!visited.Add(currentParent))
                {
                    diagnostics.Add(new BedrockGeometryDiagnostic(
                        JsonFormatErrorCodes.BrokenReferenceOrHierarchy,
                        $"Bone hierarchy cycle detected involving bone '{bone.Name}' and ancestor '{currentParent}'.",
                        IsError: true,
                        FilePath: filePath,
                        JsonPath: $"{bonePath}.parent"));
                    break;
                }

                if (!boneByName.TryGetValue(currentParent, out var parentEntry))
                {
                    break;
                }

                currentParent = parentEntry.Bone.Parent;
            }
        }
    }
}
