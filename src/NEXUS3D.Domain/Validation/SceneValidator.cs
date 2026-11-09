using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Domain.Validation;

/// <summary>
/// Master validator for <see cref="Scene3D"/>.
/// Aggregates findings across SceneGraph, Coordinates, Meshes, VoxelGrids, Materials, Textures,
/// Skeletons, Animations, Metadata, and cross-entity ID references without halting on first non-fatal issue.
/// </summary>
public sealed class SceneValidator : IValidator<Scene3D>
{
    private readonly MeshValidator _meshValidator = new();
    private readonly VoxelGridValidator _voxelValidator = new();
    private readonly MaterialValidator _materialValidator = new();
    private readonly SkeletonValidator _skeletonValidator = new();
    private readonly AnimationValidator _animationValidator = new();

    public ValidationResult Validate(Scene3D? scene)
    {
        var result = new ValidationResult();
        if (scene is null)
        {
            result.AddError(UsmErrorCodes.InvalidScene, "Scene3D instance cannot be null.", "Scene");
            return result;
        }

        if (scene.Id == Guid.Empty)
        {
            result.AddError(UsmErrorCodes.InvalidScene, "Scene ID cannot be Guid.Empty.", "Scene.Id");
        }

        if (string.IsNullOrWhiteSpace(scene.Name))
        {
            result.AddError(UsmErrorCodes.InvalidScene, "Scene Name cannot be null or whitespace.", "Scene.Name", scene.Id);
        }

        if (scene.SchemaVersion <= 0 || scene.SchemaVersion > Scene3D.CurrentSchemaVersion)
        {
            result.AddError(
                UsmErrorCodes.UnsupportedSchemaVersion,
                $"Scene SchemaVersion '{scene.SchemaVersion}' is invalid or unsupported (Current={Scene3D.CurrentSchemaVersion}).",
                "Scene.SchemaVersion",
                scene.Id);
        }

        // Coordinate System Validation
        if (scene.Coordinates is null ||
            !scene.Coordinates.SourceSystem.IsValid ||
            !scene.Coordinates.CurrentSystem.IsValid ||
            !scene.Coordinates.AxisMapping.IsOrthogonal ||
            !double.IsFinite(scene.Coordinates.ScaleFactor) ||
            scene.Coordinates.ScaleFactor <= 0.0)
        {
            result.AddError(
                UsmErrorCodes.InvalidCoordinateSystem,
                "Scene CoordinateMetadata contains non-orthogonal axes or invalid scale factor.",
                "Scene.Coordinates",
                scene.Id);
        }

        // Textures Validation
        var knownTextureIds = new HashSet<Guid>();
        for (int i = 0; i < scene.Textures.Count; i++)
        {
            var tex = scene.Textures[i];
            result.Merge(_materialValidator.ValidateTexture(tex));
            if (tex.Id != Guid.Empty && !knownTextureIds.Add(tex.Id))
            {
                result.AddError(UsmErrorCodes.DuplicateId, $"Duplicate Texture ID '{tex.Id}'.", $"Scene.Textures[{i}]", tex.Id);
            }
        }

        // Materials Validation & Texture Slot Cross-Reference
        var knownMaterialIds = new HashSet<Guid>();
        for (int i = 0; i < scene.Materials.Materials.Count; i++)
        {
            var mat = scene.Materials.Materials[i];
            result.Merge(_materialValidator.Validate(mat));
            if (mat.Id != Guid.Empty && !knownMaterialIds.Add(mat.Id))
            {
                result.AddError(UsmErrorCodes.DuplicateId, $"Duplicate Material ID '{mat.Id}'.", $"Scene.Materials[{i}]", mat.Id);
            }

            foreach (var slot in mat.TextureSlots)
            {
                if (slot.Value.TextureId != Guid.Empty && !knownTextureIds.Contains(slot.Value.TextureId))
                {
                    result.AddError(
                        UsmErrorCodes.TextureReferenceNotFound,
                        $"Material '{mat.Name}' slot '{slot.Key}' references non-existent TextureId '{slot.Value.TextureId}'.",
                        $"Scene.Materials[{mat.Name}].TextureSlots[{slot.Key}]",
                        mat.Id);
                }
            }
        }

        // Skeletons Validation
        var knownSkeletonIds = new HashSet<Guid>();
        var knownBoneIds = new HashSet<Guid>();
        for (int i = 0; i < scene.Skeletons.Count; i++)
        {
            var skel = scene.Skeletons[i];
            result.Merge(_skeletonValidator.Validate(skel));
            if (skel.Id != Guid.Empty && !knownSkeletonIds.Add(skel.Id))
            {
                result.AddError(UsmErrorCodes.DuplicateId, $"Duplicate Skeleton ID '{skel.Id}'.", $"Scene.Skeletons[{i}]", skel.Id);
            }

            foreach (var bone in skel.Bones)
            {
                knownBoneIds.Add(bone.Id);
            }
        }

        // SceneGraph & Nodes Validation
        if (scene.Graph is null)
        {
            result.AddError(UsmErrorCodes.InvalidScene, "SceneGraph cannot be null.", "Scene.Graph", scene.Id);
            return result;
        }

        ValidationResult graphValidation = scene.Graph.Validate();
        result.Merge(graphValidation);

        var knownNodeIds = new HashSet<Guid>();
        if (!scene.Graph.ContainsCycle())
        {
            foreach (var node in scene.Graph.Traverse())
            {
                knownNodeIds.Add(node.Id);

                if (node.Object is { } obj)
                {
                    if (obj.SkeletonId.HasValue && !knownSkeletonIds.Contains(obj.SkeletonId.Value))
                    {
                        result.AddError(
                            UsmErrorCodes.InvalidSkeleton,
                            $"SceneObject '{obj.Name}' references unknown SkeletonId '{obj.SkeletonId.Value}'.",
                            $"Node[{node.Id}].Object.SkeletonId",
                            obj.Id);
                    }

                    foreach (var mesh in obj.Meshes)
                    {
                        result.Merge(_meshValidator.Validate(mesh));

                        foreach (var face in mesh.Faces)
                        {
                            if (face.MaterialId.HasValue && !knownMaterialIds.Contains(face.MaterialId.Value))
                            {
                                result.AddError(
                                    UsmErrorCodes.MaterialReferenceNotFound,
                                    $"Mesh '{mesh.Name}' face references unknown MaterialId '{face.MaterialId.Value}'.",
                                    $"Mesh[{mesh.Id}].Faces",
                                    mesh.Id);
                            }
                        }

                        foreach (var sub in mesh.Submeshes)
                        {
                            if (sub.MaterialId.HasValue && !knownMaterialIds.Contains(sub.MaterialId.Value))
                            {
                                result.AddError(
                                    UsmErrorCodes.MaterialReferenceNotFound,
                                    $"Submesh '{sub.Name}' references unknown MaterialId '{sub.MaterialId.Value}'.",
                                    $"Mesh[{mesh.Id}].Submeshes",
                                    mesh.Id);
                            }
                        }
                    }

                    foreach (var grid in obj.VoxelGrids)
                    {
                        result.Merge(_voxelValidator.Validate(grid));

                        foreach (var entry in grid.Palette.Entries)
                        {
                            if (entry.MaterialId.HasValue && !knownMaterialIds.Contains(entry.MaterialId.Value))
                            {
                                result.AddError(
                                    UsmErrorCodes.MaterialReferenceNotFound,
                                    $"VoxelPalette entry '{entry.State}' references unknown MaterialId '{entry.MaterialId.Value}'.",
                                    $"VoxelGrid[{grid.Id}].Palette",
                                    grid.Id);
                            }
                        }
                    }
                }
            }
        }

        // Animations Validation
        for (int i = 0; i < scene.Animations.Count; i++)
        {
            var anim = scene.Animations[i];
            result.Merge(_animationValidator.Validate(anim));

            foreach (var track in anim.Tracks)
            {
                if (track.TargetId != Guid.Empty &&
                    !knownNodeIds.Contains(track.TargetId) &&
                    !knownBoneIds.Contains(track.TargetId))
                {
                    result.AddError(
                        UsmErrorCodes.InvalidAnimation,
                        $"Animation '{anim.Name}' track targets unknown Node/Bone ID '{track.TargetId}'.",
                        $"Scene.Animations[{i}].Tracks",
                        anim.Id);
                }
            }
        }

        // Metadata & RawDataContainers Validation
        if (scene.Metadata is null)
        {
            result.AddError(UsmErrorCodes.InvalidMetadata, "SceneMetadata cannot be null.", "Scene.Metadata", scene.Id);
        }
        else
        {
            foreach (var ext in scene.Metadata.Extensions.Values)
            {
                if (!ext.IsValidNamespace)
                {
                    result.AddError(
                        UsmErrorCodes.InvalidMetadata,
                        $"Invalid ExtensionData namespace '{ext.Namespace}'.",
                        "Scene.Metadata.Extensions",
                        scene.Id);
                }
            }

            foreach (var raw in scene.Metadata.PreservedRawData)
            {
                if (!raw.IsWithinSizeLimit())
                {
                    result.AddError(
                        UsmErrorCodes.DataLimitExceeded,
                        $"RawDataContainer '{raw.Name}' exceeds maximum allowed payload size ({raw.MaxAllowedBytes} bytes).",
                        "Scene.Metadata.PreservedRawData",
                        raw.Id);
                }
                else if (!raw.IsValid())
                {
                    result.AddError(
                        UsmErrorCodes.InvalidRawData,
                        $"RawDataContainer '{raw.Name}' has missing source format or empty payload.",
                        "Scene.Metadata.PreservedRawData",
                        raw.Id);
                }
            }
        }

        return result;
    }
}

