using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Domain.Factories;

/// <summary>
/// Creates properly initialized <see cref="Scene3D"/> aggregates with default Root node, Coordinates, and Metadata.
/// </summary>
public sealed class SceneFactory
{
    public Scene3D Create(string name, CoordinateMetadata? coordinates = null)
    {
        DomainGuard.NotNullOrWhiteSpace(name);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        var metadata = new SceneMetadata { Title = name.Trim() };
        metadata.ProvenanceHistory.Add(new DataProvenance(DataOriginKind.Original, nameof(SceneFactory), "CreateScene"));

        return new Scene3D
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            SchemaVersion = Scene3D.CurrentSchemaVersion,
            Graph = SceneGraph.CreateDefault(),
            Metadata = metadata,
            Settings = new SceneSettings(),
            Coordinates = coordinates?.DeepClone() ?? CoordinateMetadata.CreateDefault(),
            CreatedAt = now,
            ModifiedAt = now
        };
    }
}

/// <summary>
/// Factory for creating <see cref="SceneNode"/> instances with optional <see cref="SceneObject"/> payloads.
/// </summary>
public sealed class SceneNodeFactory
{
    public SceneNode CreateEmptyNode(string name, Transform3D? localTransform = null) =>
        new(DomainGuard.NotNullOrWhiteSpace(name))
        {
            Id = Guid.NewGuid(),
            LocalTransform = localTransform?.DeepClone() ?? Transform3D.Identity()
        };

    public SceneNode CreateMeshNode(string name, MeshData mesh, Transform3D? localTransform = null)
    {
        DomainGuard.NotNullOrWhiteSpace(name);
        DomainGuard.NotNull(mesh);

        var obj = new SceneObject(name);
        obj.Meshes.Add(mesh);

        return new SceneNode(name, obj)
        {
            Id = Guid.NewGuid(),
            LocalTransform = localTransform?.DeepClone() ?? Transform3D.Identity()
        };
    }

    public SceneNode CreateVoxelNode(string name, VoxelGrid grid, Transform3D? localTransform = null)
    {
        DomainGuard.NotNullOrWhiteSpace(name);
        DomainGuard.NotNull(grid);

        var obj = new SceneObject(name);
        obj.VoxelGrids.Add(grid);

        return new SceneNode(name, obj)
        {
            Id = Guid.NewGuid(),
            LocalTransform = localTransform?.DeepClone() ?? Transform3D.Identity()
        };
    }
}

/// <summary>
/// Factory for creating standard <see cref="MeshData"/> primitives (such as a unit cube or quad).
/// </summary>
public sealed class MeshFactory
{
    public MeshData CreateEmpty(string name, MeshTopology topology = MeshTopology.Polygons) =>
        new(DomainGuard.NotNullOrWhiteSpace(name), topology);

    public MeshData CreateBox(string name, Vector3D size, Guid? materialId = null)
    {
        DomainGuard.NotNullOrWhiteSpace(name);
        Vector3D half = size * 0.5;

        var mesh = new MeshData(name, MeshTopology.Polygons);
        mesh.Vertices.AddRange(
        [
            new Vertex(new Vector3D(-half.X, -half.Y, -half.Z), new Vector3D(0, 0, -1), new Vector2D(0, 0)),
            new Vertex(new Vector3D( half.X, -half.Y, -half.Z), new Vector3D(0, 0, -1), new Vector2D(1, 0)),
            new Vertex(new Vector3D( half.X,  half.Y, -half.Z), new Vector3D(0, 0, -1), new Vector2D(1, 1)),
            new Vertex(new Vector3D(-half.X,  half.Y, -half.Z), new Vector3D(0, 0, -1), new Vector2D(0, 1)),
            new Vertex(new Vector3D(-half.X, -half.Y,  half.Z), new Vector3D(0, 0,  1), new Vector2D(0, 0)),
            new Vertex(new Vector3D( half.X, -half.Y,  half.Z), new Vector3D(0, 0,  1), new Vector2D(1, 0)),
            new Vertex(new Vector3D( half.X,  half.Y,  half.Z), new Vector3D(0, 0,  1), new Vector2D(1, 1)),
            new Vertex(new Vector3D(-half.X,  half.Y,  half.Z), new Vector3D(0, 0,  1), new Vector2D(0, 1))
        ]);

        mesh.Faces.AddRange(
        [
            new Face([0, 3, 2, 1], materialId),
            new Face([4, 5, 6, 7], materialId),
            new Face([0, 1, 5, 4], materialId),
            new Face([2, 3, 7, 6], materialId),
            new Face([0, 4, 7, 3], materialId),
            new Face([1, 2, 6, 5], materialId)
        ]);

        return mesh;
    }
}

/// <summary>
/// Factory for creating <see cref="VoxelGrid"/> instances.
/// </summary>
public sealed class VoxelGridFactory
{
    public VoxelGrid CreateEmpty(string name, VoxelSize? voxelSize = null) =>
        new(DomainGuard.NotNullOrWhiteSpace(name))
        {
            VoxelSize = voxelSize ?? VoxelSize.UnitCube
        };
}

