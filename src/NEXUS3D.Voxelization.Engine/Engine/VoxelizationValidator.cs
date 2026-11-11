using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Voxelization.Abstractions.Contracts;
using NEXUS3D.Voxelization.Abstractions.Models;
using NEXUS3D.Voxelization.Engine.Processing;

namespace NEXUS3D.Voxelization.Engine.Engine;

/// <summary>
/// Pre-execution validator enforcing geometry integrity and resource safety limits.
/// </summary>
public sealed class VoxelizationValidator : IVoxelizationValidator
{
    private readonly MeshPreprocessor _preprocessor = new();

    public (bool IsValid, string? ErrorMessage, IReadOnlyList<VoxelizationWarning> Warnings) Validate(
        Scene3D scene,
        IVoxelizationSettings settings)
    {
        var warnings = new List<VoxelizationWarning>();
        if (scene is null)
        {
            return (false, "Scene cannot be null.", warnings);
        }

        var settingsResult = settings.Validate();
        if (settingsResult.IsFailure)
        {
            return (false, settingsResult.ErrorMessage, warnings);
        }

        // Count meshes and voxel grids in scene
        int meshCount = 0;
        int voxelGridCount = 0;
        foreach (var node in scene.Graph.Nodes)
        {
            if (node.Object?.Meshes.Count > 0)
            {
                meshCount += node.Object.Meshes.Count;
            }
            if (node.Object?.VoxelGrids.Count > 0)
            {
                voxelGridCount += node.Object.VoxelGrids.Count;
            }
        }

        if (meshCount == 0 && voxelGridCount == 0)
        {
            return (false, "Scene does not contain any 3D polygon meshes or voxel grids to voxelize.", warnings);
        }

        return (true, null, warnings);
    }

    public (bool IsValid, string? ErrorMessage, IReadOnlyList<VoxelizationWarning> Warnings) ValidateMesh(
        MeshData mesh,
        IVoxelizationSettings settings)
    {
        var warnings = new List<VoxelizationWarning>();
        if (mesh is null)
        {
            return (false, "Mesh cannot be null.", warnings);
        }

        var settingsResult = settings.Validate();
        if (settingsResult.IsFailure)
        {
            return (false, settingsResult.ErrorMessage, warnings);
        }

        if (mesh.Vertices.Count == 0 || mesh.Faces.Count == 0)
        {
            return (false, $"Mesh '{mesh.Name}' contains no vertices or faces.", warnings);
        }

        var prep = _preprocessor.Preprocess(mesh);
        warnings.AddRange(prep.Warnings);

        if (prep.Triangles.Count == 0)
        {
            return (false, $"Mesh '{mesh.Name}' produced 0 valid non-degenerate triangles.", warnings);
        }

        return (true, null, warnings);
    }
}
