using System.Text;
using System.Text.Json;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Metadata;

namespace NEXUS3D.Domain.Voxels;

/// <summary>
/// Format-agnostic representation of a voxel block type and its key-value state properties.
/// Supports custom namespaces, modded blocks, and standard voxel identifiers.
/// </summary>
public sealed class BlockState : IDeepCloneable<BlockState>, IEquatable<BlockState>
{
    public string Namespace { get; set; } = "default";
    public string Name { get; set; } = "air";
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.Ordinal);

    public BlockState()
    {
    }

    public BlockState(string @namespace, string name, IDictionary<string, string>? properties = null)
    {
        Namespace = string.IsNullOrWhiteSpace(@namespace) ? "default" : @namespace.Trim();
        Name = DomainGuard.NotNullOrWhiteSpace(name).Trim();
        if (properties is not null)
        {
            foreach (var kvp in properties)
            {
                Properties[kvp.Key] = kvp.Value;
            }
        }
    }

    public bool IsAir =>
        string.Equals(Name, "air", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Name, "cave_air", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Name, "void_air", StringComparison.OrdinalIgnoreCase);

    public string GetFullIdentifier() => $"{Namespace}:{Name}";

    /// <summary>
    /// Returns the canonical state string including sorted properties, e.g. "minecraft:oak_stairs[facing=north,half=bottom]".
    /// </summary>
    public string GetCanonicalStateKey()
    {
        if (Properties.Count == 0)
        {
            return GetFullIdentifier();
        }

        var sb = new StringBuilder(GetFullIdentifier());
        sb.Append('[');
        bool first = true;
        foreach (var kvp in Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                sb.Append(',');
            }

            sb.Append(kvp.Key).Append('=').Append(kvp.Value);
            first = false;
        }

        sb.Append(']');
        return sb.ToString();
    }

    public bool TryGetProperty(string key, out string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = null;
            return false;
        }

        return Properties.TryGetValue(key, out value);
    }

    public BlockState DeepClone() => new(Namespace, Name, Properties);

    public bool Equals(BlockState? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(GetCanonicalStateKey(), other.GetCanonicalStateKey(), StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as BlockState);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(GetCanonicalStateKey());

    public override string ToString() => GetCanonicalStateKey();
}

/// <summary>
/// Holds extended tile/block entity data (such as signs, containers, banners, or custom metadata)
/// without coupling the Domain layer to format-specific classes.
/// </summary>
public sealed class BlockEntityData : IDeepCloneable<BlockEntityData>
{
    public string TypeId { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> Properties { get; set; } = new(StringComparer.Ordinal);
    public RawDataContainer? RawData { get; set; }

    public BlockEntityData()
    {
    }

    public BlockEntityData(string typeId)
    {
        TypeId = DomainGuard.NotNullOrWhiteSpace(typeId);
    }

    public BlockEntityData DeepClone()
    {
        var clone = new BlockEntityData
        {
            TypeId = TypeId,
            RawData = RawData?.DeepClone()
        };

        foreach (var kvp in Properties)
        {
            clone.Properties[kvp.Key] = kvp.Value.Clone();
        }

        return clone;
    }
}

