using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Domain.Meshes;

/// <summary>
/// Partition of faces within a <see cref="MeshData"/> sharing a material or logical group.
/// </summary>
public sealed class MeshSubmesh : Entity, ISceneComponent, IDeepCloneable<MeshSubmesh>
{
    public string Name { get; set; } = "Submesh";
    public int FaceStartIndex { get; set; }
    public int FaceCount { get; set; }
    public Guid? MaterialId { get; set; }

    public MeshSubmesh()
    {
    }

    public MeshSubmesh(string name, int faceStartIndex, int faceCount, Guid? materialId = null)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
        FaceStartIndex = faceStartIndex;
        FaceCount = faceCount;
        MaterialId = materialId;
    }

    public MeshSubmesh DeepClone() => new(Name, FaceStartIndex, FaceCount, MaterialId)
    {
        Id = Id
    };
}

/// <summary>
/// Computed summary statistics for a <see cref="MeshData"/> instance.
/// </summary>
public sealed record MeshStatistics(
    int VertexCount,
    int FaceCount,
    int EstimatedTriangleCount,
    int SubmeshCount,
    bool HasNormals,
    bool HasTexCoords,
    bool HasVertexColors,
    BoundingBox Bounds);

/// <summary>
/// Represents a 3D polygon or triangle mesh in the Universal Scene Model.
/// </summary>
public sealed class MeshData : Entity, ISceneComponent, IDeepCloneable<MeshData>
{
    public string Name { get; set; } = "Mesh";
    public List<Vertex> Vertices { get; set; } = [];
    public List<Face> Faces { get; set; } = [];
    public List<MeshSubmesh> Submeshes { get; set; } = [];
    public MeshTopology Topology { get; set; } = MeshTopology.Polygons;

    public BoundingBox Bounds => CalculateBounds();

    public MeshData()
    {
    }

    public MeshData(string name, MeshTopology topology = MeshTopology.Polygons)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
        Topology = topology;
    }

    public BoundingBox CalculateBounds()
    {
        if (Vertices.Count == 0)
        {
            return BoundingBox.Empty;
        }

        BoundingBox box = BoundingBox.Empty;
        for (int i = 0; i < Vertices.Count; i++)
        {
            box = box.Encapsulate(Vertices[i].Position);
        }

        return box;
    }

    public MeshStatistics CalculateStatistics()
    {
        int triEstimate = 0;
        for (int i = 0; i < Faces.Count; i++)
        {
            int count = Faces[i].VertexIndices.Count;
            if (count >= 3)
            {
                triEstimate += count - 2;
            }
        }

        bool hasNormals = Vertices.Any(v => v.Normal.HasValue);
        bool hasTexCoords = Vertices.Any(v => v.TexCoord.HasValue);
        bool hasColors = Vertices.Any(v => v.Color.HasValue);

        return new MeshStatistics(
            VertexCount: Vertices.Count,
            FaceCount: Faces.Count,
            EstimatedTriangleCount: triEstimate,
            SubmeshCount: Submeshes.Count,
            HasNormals: hasNormals,
            HasTexCoords: hasTexCoords,
            HasVertexColors: hasColors,
            Bounds: CalculateBounds());
    }

    public ValidationResult Validate() => new MeshValidator().Validate(this);

    public MeshData DeepClone() => new(Name, Topology)
    {
        Id = Id,
        Vertices = Vertices.Select(v => v.DeepClone()).ToList(),
        Faces = Faces.Select(f => f.DeepClone()).ToList(),
        Submeshes = Submeshes.Select(s => s.DeepClone()).ToList()
    };
}

