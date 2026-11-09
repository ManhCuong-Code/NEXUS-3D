using System.Text.Json;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;
using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Domain.Materials;

/// <summary>
/// Normalized RGBA color value object.
/// </summary>
public readonly record struct MaterialColor(double R, double G, double B, double A = 1.0)
{
    public static MaterialColor White => new(1.0, 1.0, 1.0, 1.0);
    public static MaterialColor Black => new(0.0, 0.0, 0.0, 1.0);
    public static MaterialColor Transparent => new(0.0, 0.0, 0.0, 0.0);

    public bool IsValid =>
        double.IsFinite(R) && double.IsFinite(G) && double.IsFinite(B) && double.IsFinite(A) &&
        R >= 0.0 && R <= 1.0 &&
        G >= 0.0 && G <= 1.0 &&
        B >= 0.0 && B <= 1.0 &&
        A >= 0.0 && A <= 1.0;
}

/// <summary>
/// Alpha rendering mode for materials.
/// </summary>
public enum TransparencyMode
{
    Opaque = 0,
    AlphaCutout = 1,
    AlphaBlend = 2,
    Additive = 3
}

/// <summary>
/// Typed custom material property entry.
/// </summary>
public readonly record struct MaterialProperty(string Key, string Value, string ValueType = "string");

/// <summary>
/// Binds a texture by its <see cref="Guid"/> to a named material slot (e.g., "Albedo", "Normal", "Roughness", "Emissive")
/// along with UV offset, scale, and rotation.
/// </summary>
public sealed class TextureSlot : IDeepCloneable<TextureSlot>
{
    public Guid TextureId { get; set; }
    public Vector2D Offset { get; set; } = Vector2D.Zero;
    public Vector2D Scale { get; set; } = Vector2D.One;
    public double Rotation { get; set; }
    public string? Channel { get; set; }

    public TextureSlot()
    {
    }

    public TextureSlot(Guid textureId, string? channel = null)
    {
        TextureId = textureId;
        Channel = channel;
    }

    public TextureSlot DeepClone() => new()
    {
        TextureId = TextureId,
        Offset = Offset,
        Scale = Scale,
        Rotation = Rotation,
        Channel = Channel
    };
}

/// <summary>
/// Format-independent surface material definition.
/// Supports both classic diffuse/specular (MTL/Voxel) and physically based rendering (PBR) attributes.
/// </summary>
public sealed class MaterialData : Entity, ISceneComponent, IDeepCloneable<MaterialData>
{
    public string Name { get; set; } = "DefaultMaterial";
    public MaterialColor BaseColor { get; set; } = MaterialColor.White;
    public double Metallic { get; set; } = 0.0;
    public double Roughness { get; set; } = 0.5;
    public double Opacity { get; set; } = 1.0;
    public bool IsPbrMetallicRoughnessSpecified { get; set; }
    public TransparencyMode TransparencyMode { get; set; } = TransparencyMode.Opaque;
    public Dictionary<string, TextureSlot> TextureSlots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, JsonElement> CustomProperties { get; set; } = new(StringComparer.Ordinal);

    public MaterialData()
    {
    }

    public MaterialData(string name)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
    }

    public MaterialData DeepClone()
    {
        var clone = new MaterialData(Name)
        {
            Id = Id,
            BaseColor = BaseColor,
            Metallic = Metallic,
            Roughness = Roughness,
            Opacity = Opacity,
            IsPbrMetallicRoughnessSpecified = IsPbrMetallicRoughnessSpecified,
            TransparencyMode = TransparencyMode
        };

        foreach (var slot in TextureSlots)
        {
            clone.TextureSlots[slot.Key] = slot.Value.DeepClone();
        }

        foreach (var prop in CustomProperties)
        {
            clone.CustomProperties[prop.Key] = prop.Value.Clone();
        }

        return clone;
    }
}

/// <summary>
/// Manages the collection of <see cref="MaterialData"/> within a <see cref="Scene.Scene3D"/>.
/// </summary>
public sealed class MaterialLibrary : IDeepCloneable<MaterialLibrary>
{
    public List<MaterialData> Materials { get; set; } = [];

    public int Count => Materials.Count;

    public bool Add(MaterialData material)
    {
        DomainGuard.NotNull(material);
        if (Materials.Any(m => m.Id == material.Id))
        {
            return false;
        }

        Materials.Add(material);
        return true;
    }

    public MaterialData? FindById(Guid id) =>
        Materials.FirstOrDefault(m => m.Id == id);

    public MaterialData? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Materials.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public bool ContainsId(Guid id) =>
        Materials.Any(m => m.Id == id);

    public bool Remove(Guid id) =>
        Materials.RemoveAll(m => m.Id == id) > 0;

    public MaterialLibrary DeepClone() => new()
    {
        Materials = Materials.Select(m => m.DeepClone()).ToList()
    };
}

