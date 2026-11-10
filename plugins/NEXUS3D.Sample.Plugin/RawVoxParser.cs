using System.Globalization;
using System.Text;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Sample.Plugin;

/// <summary>
/// Parses RawVox 3D text streams into Universal Scene Model (<see cref="Scene3D"/>).
/// </summary>
public sealed class RawVoxParser : IFormatParser
{
    public string FormatIdentifier => "RAWVOX";

    public async Task<Result<Scene3D>> ParseToSceneAsync(
        Stream source,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (source == null || !source.CanRead)
        {
            return Result<Scene3D>.Failure(new Error("STREAM_UNREADABLE", "Cannot read source stream."));
        }

        try
        {
            using var reader = new StreamReader(source, Encoding.UTF8, leaveOpen: true);
            string? header = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (header is null || !header.StartsWith("RAWVOX1", StringComparison.OrdinalIgnoreCase))
            {
                return Result<Scene3D>.Failure(new Error("RAWVOX_INVALID_HEADER", "Stream does not begin with RAWVOX1 magic header."));
            }

            string sceneName = "RawVoxModel";
            var grid = new VoxelGrid("RawVoxVoxelGrid");
            MeshData? mesh = null;

            var paletteStates = new Dictionary<int, BlockState>();

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                string command = parts[0].ToUpperInvariant();

                switch (command)
                {
                    case "NAME" when parts.Length > 1:
                        sceneName = parts[1];
                        break;

                    case "PALETTE" when parts.Length > 1 && int.TryParse(parts[1], out int palCount):
                        for (int i = 0; i < palCount; i++)
                        {
                            string? palLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                            if (palLine == null) break;
                            var palParts = palLine.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (palParts.Length >= 2 && int.TryParse(palParts[0], out int idx))
                            {
                                string blockId = palParts[1];
                                string ns = "minecraft";
                                string blockName = blockId;
                                if (blockId.Contains(':'))
                                {
                                    var colonParts = blockId.Split(':', 2);
                                    ns = colonParts[0];
                                    blockName = colonParts[1];
                                }
                                var state = new BlockState(ns, blockName);
                                grid.Palette.Add(new VoxelPaletteEntry(state, displayName: palParts[1]));
                                paletteStates[idx] = state;
                            }
                        }
                        break;

                    case "VOXELS" when parts.Length > 1 && int.TryParse(parts[1], out int voxCount):
                        for (int i = 0; i < voxCount; i++)
                        {
                            string? voxLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                            if (voxLine == null) break;
                            var vParts = voxLine.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (vParts.Length >= 4 &&
                                int.TryParse(vParts[0], out int x) &&
                                int.TryParse(vParts[1], out int y) &&
                                int.TryParse(vParts[2], out int z) &&
                                int.TryParse(vParts[3], out int palIdx))
                            {
                                paletteStates.TryGetValue(palIdx, out var state);
                                grid.SetVoxel(new Voxel(new VoxelPosition(x, y, z), palIdx, state));
                            }
                        }
                        break;

                    case "VERTICES" when parts.Length > 1 && int.TryParse(parts[1], out int vCount):
                        mesh ??= new MeshData("RawVoxMesh");
                        for (int i = 0; i < vCount; i++)
                        {
                            string? vLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                            if (vLine == null) break;
                            var coords = vLine.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (coords.Length >= 3 &&
                                double.TryParse(coords[0], CultureInfo.InvariantCulture, out double vx) &&
                                double.TryParse(coords[1], CultureInfo.InvariantCulture, out double vy) &&
                                double.TryParse(coords[2], CultureInfo.InvariantCulture, out double vz))
                            {
                                mesh.Vertices.Add(new Vertex(new Vector3D(vx, vy, vz)));
                            }
                        }
                        break;

                    case "FACES" when parts.Length > 1 && int.TryParse(parts[1], out int fCount):
                        mesh ??= new MeshData("RawVoxMesh");
                        for (int i = 0; i < fCount; i++)
                        {
                            string? fLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                            if (fLine == null) break;
                            var fIndices = fLine.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (fIndices.Length >= 3 &&
                                int.TryParse(fIndices[0], out int i0) &&
                                int.TryParse(fIndices[1], out int i1) &&
                                int.TryParse(fIndices[2], out int i2))
                            {
                                mesh.Faces.Add(new Face(new[] { i0, i1, i2 }));
                            }
                        }
                        break;
                }
            }

            var scene = new Scene3D { Name = sceneName };
            var sceneObj = new SceneObject(sceneName);

            if (grid.Voxels.Count > 0)
            {
                sceneObj.VoxelGrids.Add(grid);
            }

            if (mesh is not null && mesh.Vertices.Count > 0)
            {
                sceneObj.Meshes.Add(mesh);
            }

            var node = new SceneNode(sceneName, sceneObj);
            scene.Graph.AddNode(scene.Graph.Root, node);

            return Result<Scene3D>.Success(scene);
        }
        catch (Exception ex)
        {
            return Result<Scene3D>.Failure(new Error("RAWVOX_PARSE_EXCEPTION", ex.Message));
        }
    }
}
