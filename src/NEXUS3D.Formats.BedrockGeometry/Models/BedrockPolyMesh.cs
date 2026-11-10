using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Models;

/// <summary>
/// Optional <c>poly_mesh</c> arbitrary polygon mesh attached to a <see cref="BedrockBone"/>.
/// </summary>
public sealed class BedrockPolyMesh
{
    public bool? NormalizedUvs { get; set; }
    public List<Vector3D> Positions { get; set; } = [];
    public List<Vector3D> Normals { get; set; } = [];
    public List<Vector2D> Uvs { get; set; } = [];

    /// <summary>
    /// Polygon index list when <c>polys</c> is an array of faces, where each face is an array of <c>[posIdx, normalIdx, uvIdx]</c> vertex triplets.
    /// </summary>
    public List<int[][]> Polys { get; set; } = [];

    /// <summary>
    /// Optional string mode when <c>polys</c> is specified as <c>"tri_list"</c> or <c>"quad_list"</c>.
    /// </summary>
    public string? PolyType { get; set; }

    public JsonExtensionData ExtensionData { get; set; } = new();

    public BedrockPolyMesh DeepClone() => new()
    {
        NormalizedUvs = NormalizedUvs,
        Positions = [.. Positions],
        Normals = [.. Normals],
        Uvs = [.. Uvs],
        Polys = Polys.Select(face => face.Select(v => (int[])v.Clone()).ToArray()).ToList(),
        PolyType = PolyType,
        ExtensionData = ExtensionData.DeepClone()
    };
}
