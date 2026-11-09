using NEXUS3D.Domain.Textures;

namespace NEXUS3D.Formats.Obj.Textures;

/// <summary>
/// Represents a resolved texture reference originating from a Wavefront MTL texture map statement.
/// Bridges MTL texture paths with Universal Scene Model <see cref="TextureData"/> and <see cref="NEXUS3D.Domain.Textures.TextureReference"/>.
/// </summary>
public sealed class ObjTextureReference
{
    public string MaterialName { get; init; } = string.Empty;
    public string SlotName { get; init; } = "Albedo";
    public string MtlKeyword { get; init; } = "map_Kd";
    public string RawPath { get; init; } = string.Empty;
    public string NormalizedRelativePath { get; init; } = string.Empty;
    public string? ResolvedFullPath { get; init; }
    public TextureFormat Format { get; init; } = TextureFormat.Unknown;
    public TextureColorSpace ColorSpace { get; init; } = TextureColorSpace.Srgb;
    public bool ExistsOnDisk { get; init; }
    public int Width { get; init; } = 16;
    public int Height { get; init; } = 16;
    public long? FileSizeBytes { get; init; }

    /// <summary>
    /// Converts this resolved OBJ/MTL texture descriptor into a Universal Scene Model <see cref="TextureData"/> entity
    /// without eagerly loading raw pixel buffers into memory.
    /// </summary>
    public TextureData ToUsmTextureData()
    {
        string name = Path.GetFileNameWithoutExtension(NormalizedRelativePath);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Texture";
        }

        var usmRef = new TextureReference
        {
            StorageKind = TextureStorageKind.External,
            LoadState = ExistsOnDisk ? TextureLoadState.Unloaded : TextureLoadState.Missing,
            RelativePath = NormalizedRelativePath
        };

        var texData = new TextureData(
            name,
            Math.Max(1, Width),
            Math.Max(1, Height),
            usmRef)
        {
            Format = Format == TextureFormat.Unknown ? TextureFormat.Png : Format,
            ColorSpace = ColorSpace
        };

        texData.Metadata.Tags["obj.rawPath"] = RawPath;
        texData.Metadata.Tags["obj.mtlKeyword"] = MtlKeyword;
        texData.Metadata.Tags["obj.slotName"] = SlotName;
        if (!string.IsNullOrWhiteSpace(ResolvedFullPath))
        {
            texData.Metadata.Tags["obj.resolvedFullPath"] = ResolvedFullPath;
        }

        if (FileSizeBytes.HasValue)
        {
            texData.Metadata.Tags["obj.fileSizeBytes"] = FileSizeBytes.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return texData;
    }
}
