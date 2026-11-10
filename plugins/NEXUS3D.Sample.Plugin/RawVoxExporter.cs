using System.Globalization;
using System.Text;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Sample.Plugin;

/// <summary>
/// Serializes a Universal Scene Model (<see cref="Scene3D"/>) to the RawVox 3D text format.
/// </summary>
public sealed class RawVoxExporter : IFormatExporter
{
    public string FormatIdentifier => "RAWVOX";

    public async Task<FormatExportReport> ExportSceneAsync(
        Scene3D scene,
        Stream destination,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (scene == null)
        {
            return new FormatExportReport(false, 0, new LossReport(), [], new Error("SCENE_NULL", "Scene cannot be null."));
        }

        if (destination == null || !destination.CanWrite)
        {
            return new FormatExportReport(false, 0, new LossReport(), [], new Error("DESTINATION_UNWRITABLE", "Destination stream cannot be written."));
        }

        long startPosition = destination.CanSeek ? destination.Position : 0;

        try
        {
            using var writer = new StreamWriter(destination, Encoding.UTF8, leaveOpen: true);
            await writer.WriteLineAsync("RAWVOX1").ConfigureAwait(false);
            await writer.WriteLineAsync($"NAME {scene.Name}").ConfigureAwait(false);

            // Collect voxel grids and meshes
            var allGrids = new List<VoxelGrid>();
            var allMeshes = new List<NEXUS3D.Domain.Meshes.MeshData>();

            foreach (var node in scene.Graph.Traverse())
            {
                if (node.Object is not { } obj) continue;
                allGrids.AddRange(obj.VoxelGrids);
                allMeshes.AddRange(obj.Meshes);
            }

            // Write Voxel Data if present
            if (allGrids.Count > 0)
            {
                var primaryGrid = allGrids[0];
                var palette = primaryGrid.Palette.Entries;

                await writer.WriteLineAsync($"PALETTE {palette.Count}").ConfigureAwait(false);
                for (int i = 0; i < palette.Count; i++)
                {
                    string name = palette[i].State.GetFullIdentifier();
                    await writer.WriteLineAsync($"{i} {name}").ConfigureAwait(false);
                }

                var voxels = primaryGrid.Voxels.ToList();
                await writer.WriteLineAsync($"VOXELS {voxels.Count}").ConfigureAwait(false);
                foreach (var voxel in voxels)
                {
                    await writer.WriteLineAsync($"{voxel.Position.X} {voxel.Position.Y} {voxel.Position.Z} {voxel.PaletteIndex}").ConfigureAwait(false);
                }
            }

            // Write Mesh Data if present
            if (allMeshes.Count > 0)
            {
                var primaryMesh = allMeshes[0];
                var vertices = primaryMesh.Vertices;
                var faces = primaryMesh.Faces;

                await writer.WriteLineAsync($"VERTICES {vertices.Count}").ConfigureAwait(false);
                foreach (var v in vertices)
                {
                    await writer.WriteLineAsync(string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", v.Position.X, v.Position.Y, v.Position.Z)).ConfigureAwait(false);
                }

                await writer.WriteLineAsync($"FACES {faces.Count}").ConfigureAwait(false);
                foreach (var f in faces)
                {
                    var idx = f.VertexIndices;
                    if (idx.Count >= 3)
                    {
                        await writer.WriteLineAsync($"{idx[0]} {idx[1]} {idx[2]}").ConfigureAwait(false);
                    }
                }
            }

            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            long bytesWritten = destination.CanSeek ? destination.Position - startPosition : 100;
            return new FormatExportReport(true, bytesWritten, new LossReport(), []);
        }
        catch (Exception ex)
        {
            return new FormatExportReport(false, 0, new LossReport(), [], new Error("EXPORT_EXCEPTION", ex.Message));
        }
    }
}
