using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Editor.Abstractions;
using NEXUS3D.Editor.Engine.Mathematics;

namespace NEXUS3D.Editor.Engine.Commands;

/// <summary>
/// Command recording baking of local transforms into vertex positions and child offsets.
/// </summary>
public sealed class ApplyTransformCommand : IEditorCommand
{
    private sealed record NodeBackup(
        SceneNode Node,
        Transform3D OriginalTransform,
        List<(MeshData Mesh, List<Vertex> Vertices)> Meshes,
        List<(SceneNode Child, Transform3D ChildTransform)> Children);

    private readonly List<NodeBackup> _backups = [];

    public string Name => "Apply / Bake Transform";

    public ApplyTransformCommand(IEnumerable<SceneNode> nodes)
    {
        foreach (var node in nodes)
        {
            var meshes = new List<(MeshData, List<Vertex>)>();
            if (node.Object is not null)
            {
                foreach (var m in node.Object.Meshes)
                {
                    meshes.Add((m, m.Vertices.Select(v => v.DeepClone()).ToList()));
                }
            }

            var children = node.Children.Select(c => (c, c.LocalTransform.DeepClone())).ToList();

            _backups.Add(new NodeBackup(
                node,
                node.LocalTransform.DeepClone(),
                meshes,
                children));
        }
    }

    public void Execute()
    {
        foreach (var backup in _backups)
        {
            var matrix = backup.OriginalTransform.ToMatrix();

            // Bake into vertices
            foreach (var (mesh, _) in backup.Meshes)
            {
                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    var v = mesh.Vertices[i];
                    v.Position = matrix.TransformPoint(v.Position);
                    if (v.Normal.HasValue)
                    {
                        v.Normal = matrix.TransformNormal(v.Normal.Value).Normalize();
                    }
                }
            }

            // Bake into child local transforms
            foreach (var (child, childOrig) in backup.Children)
            {
                var combined = matrix * childOrig.ToMatrix();
                var (pos, rot, scale) = TransformMath.Decompose(combined);
                child.LocalTransform = new Transform3D(pos, rot, scale);
            }

            backup.Node.LocalTransform = Transform3D.Identity();
        }
    }

    public void Undo()
    {
        foreach (var backup in _backups)
        {
            backup.Node.LocalTransform = backup.OriginalTransform.DeepClone();

            foreach (var (mesh, origVertices) in backup.Meshes)
            {
                mesh.Vertices = origVertices.Select(v => v.DeepClone()).ToList();
            }

            foreach (var (child, childOrig) in backup.Children)
            {
                child.LocalTransform = childOrig.DeepClone();
            }
        }
    }
}
