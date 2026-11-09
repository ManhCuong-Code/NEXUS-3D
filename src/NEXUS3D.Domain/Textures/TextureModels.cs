using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;

namespace NEXUS3D.Domain.Textures;

public enum TextureFormat
{
    Unknown = 0,
    Png = 1,
    Jpg = 2,
    Tga = 3,
    Bmp = 4,
    WebP = 5,
    Dds = 6,
    RawRgba8 = 7
}

public enum TextureColorSpace
{
    Srgb = 0,
    Linear = 1,
    NormalMap = 2,
    Data = 3
}

public enum TextureStorageKind
{
    External = 0,
    Embedded = 1,
    Missing = 2
}

public enum TextureLoadState
{
    Unloaded = 0,
    Loaded = 1,
    Missing = 2
}

/// <summary>
/// Describes how a texture asset is located and its current load state without storing raw megabyte buffers in Domain.
/// </summary>
public sealed class TextureReference : IDeepCloneable<TextureReference>
{
    public TextureStorageKind StorageKind { get; set; } = TextureStorageKind.External;
    public TextureLoadState LoadState { get; set; } = TextureLoadState.Unloaded;
    public string? RelativePath { get; set; }
    public string? EmbeddedResourceKey { get; set; }
    public string? ChecksumSha256 { get; set; }

    public bool IsValid => StorageKind switch
    {
        TextureStorageKind.External => !string.IsNullOrWhiteSpace(RelativePath),
        TextureStorageKind.Embedded => !string.IsNullOrWhiteSpace(EmbeddedResourceKey),
        TextureStorageKind.Missing => false,
        _ => false
    };

    public TextureReference DeepClone() => new()
    {
        StorageKind = StorageKind,
        LoadState = LoadState,
        RelativePath = RelativePath,
        EmbeddedResourceKey = EmbeddedResourceKey,
        ChecksumSha256 = ChecksumSha256
    };
}

/// <summary>
/// Additional descriptive metadata for a <see cref="TextureData"/> asset.
/// </summary>
public sealed class TextureMetadata : IDeepCloneable<TextureMetadata>
{
    public int MipMapLevels { get; set; } = 1;
    public bool HasAlphaChannel { get; set; } = true;
    public int BitsPerChannel { get; set; } = 8;
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public TextureMetadata DeepClone() => new()
    {
        MipMapLevels = MipMapLevels,
        HasAlphaChannel = HasAlphaChannel,
        BitsPerChannel = BitsPerChannel,
        Tags = new Dictionary<string, string>(Tags, StringComparer.OrdinalIgnoreCase)
    };
}

/// <summary>
/// Represents a 2D texture asset descriptor in the Universal Scene Model.
/// </summary>
public sealed class TextureData : Entity, ISceneComponent, IDeepCloneable<TextureData>
{
    public string Name { get; set; } = "Texture";
    public int Width { get; set; } = 16;
    public int Height { get; set; } = 16;
    public TextureFormat Format { get; set; } = TextureFormat.Png;
    public TextureColorSpace ColorSpace { get; set; } = TextureColorSpace.Srgb;
    public TextureReference Reference { get; set; } = new();
    public TextureMetadata Metadata { get; set; } = new();

    public TextureData()
    {
    }

    public TextureData(string name, int width, int height, TextureReference reference)
    {
        Name = DomainGuard.NotNullOrWhiteSpace(name);
        Width = width;
        Height = height;
        Reference = DomainGuard.NotNull(reference);
    }

    public TextureData DeepClone() => new()
    {
        Id = Id,
        Name = Name,
        Width = Width,
        Height = Height,
        Format = Format,
        ColorSpace = ColorSpace,
        Reference = Reference.DeepClone(),
        Metadata = Metadata.DeepClone()
    };
}

