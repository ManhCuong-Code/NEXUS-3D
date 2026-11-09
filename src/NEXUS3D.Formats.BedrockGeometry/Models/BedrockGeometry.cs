using System.Text.Json;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Models;

/// <summary>
/// Represents a single geometry definition within a <see cref="BedrockGeometryDocument"/>.
/// </summary>
public sealed class BedrockGeometry
{
    public BedrockGeometryDescription Description { get; set; } = new();

    /// <summary>Optional parent geometry identifier (used by legacy <c>geometry.child:geometry.parent</c> inheritance).</summary>
    public string? ParentGeometryIdentifier { get; set; }

    /// <summary>Optional cape identifier in Bedrock geometry.</summary>
    public string? Cape { get; set; }

    /// <summary>Bones belonging to this geometry in their original JSON declaration order.</summary>
    public List<BedrockBone> Bones { get; set; } = [];

    /// <summary>Raw JSON array of bones preserved for hierarchy parsing and inspection.</summary>
    public List<JsonElement> RawBoneElements { get; set; } = [];

    public JsonExtensionData ExtensionData { get; set; } = new();

    /// <summary>
    /// Returns the bones ordered topologically from root bones to child bones,
    /// safely handling files where child bones are declared before their parent bones.
    /// </summary>
    public IReadOnlyList<BedrockBone> GetTopologicalBones()
    {
        if (Bones.Count <= 1)
        {
            return Bones;
        }

        var boneByName = new Dictionary<string, BedrockBone>(StringComparer.Ordinal);
        foreach (BedrockBone bone in Bones)
        {
            if (!string.IsNullOrWhiteSpace(bone.Name) && !boneByName.ContainsKey(bone.Name))
            {
                boneByName[bone.Name] = bone;
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<BedrockBone>(Bones.Count);

        void Visit(BedrockBone bone)
        {
            if (string.IsNullOrWhiteSpace(bone.Name))
            {
                ordered.Add(bone);
                return;
            }

            if (visited.Contains(bone.Name))
            {
                return;
            }

            if (!visiting.Add(bone.Name))
            {
                // Cycle detected; break recursion
                return;
            }

            if (!string.IsNullOrWhiteSpace(bone.Parent) &&
                boneByName.TryGetValue(bone.Parent, out BedrockBone? parentBone))
            {
                Visit(parentBone);
            }

            visiting.Remove(bone.Name);
            if (visited.Add(bone.Name))
            {
                ordered.Add(bone);
            }
        }

        foreach (BedrockBone bone in Bones)
        {
            Visit(bone);
        }

        // Append any remaining bones that were part of a cycle
        foreach (BedrockBone bone in Bones)
        {
            if (string.IsNullOrWhiteSpace(bone.Name) || !visited.Contains(bone.Name))
            {
                ordered.Add(bone);
            }
        }

        return ordered;
    }

    public BedrockGeometry DeepClone() => new()
    {
        Description = Description.DeepClone(),
        ParentGeometryIdentifier = ParentGeometryIdentifier,
        Cape = Cape,
        Bones = Bones.Select(b => b.DeepClone()).ToList(),
        RawBoneElements = RawBoneElements.Select(e => e.Clone()).ToList(),
        ExtensionData = ExtensionData.DeepClone()
    };
}
