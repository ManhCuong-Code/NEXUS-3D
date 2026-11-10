using System.Numerics;
using NEXUS3D.Domain.Meshes;

namespace NEXUS3D.Voxelization.Engine.Processing;

/// <summary>
/// Identifies and groups disconnected polygon components and mesh islands.
/// </summary>
public static class ConnectedComponentAnalyzer
{
    public static int CountConnectedComponents(MeshData mesh)
    {
        if (mesh is null || mesh.Faces.Count == 0 || mesh.Vertices.Count == 0)
        {
            return 0;
        }

        // Build vertex adjacency graph
        var adj = new Dictionary<int, List<int>>();
        foreach (var face in mesh.Faces)
        {
            var indices = face.VertexIndices;
            for (int i = 0; i < indices.Count; i++)
            {
                int u = indices[i];
                int v = indices[(i + 1) % indices.Count];

                if (!adj.TryGetValue(u, out var listU))
                {
                    listU = [];
                    adj[u] = listU;
                }
                listU.Add(v);

                if (!adj.TryGetValue(v, out var listV))
                {
                    listV = [];
                    adj[v] = listV;
                }
                listV.Add(u);
            }
        }

        var visited = new HashSet<int>();
        int components = 0;

        foreach (var vertex in adj.Keys)
        {
            if (visited.Add(vertex))
            {
                components++;
                var queue = new Queue<int>();
                queue.Enqueue(vertex);

                while (queue.Count > 0)
                {
                    int curr = queue.Dequeue();
                    if (adj.TryGetValue(curr, out var neighbors))
                    {
                        foreach (var n in neighbors)
                        {
                            if (visited.Add(n))
                            {
                                queue.Enqueue(n);
                            }
                        }
                    }
                }
            }
        }

        return components;
    }
}
