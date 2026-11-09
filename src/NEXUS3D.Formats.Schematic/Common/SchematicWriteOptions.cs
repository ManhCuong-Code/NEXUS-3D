using NEXUS3D.Formats.Schematic.Nbt.Compression;

namespace NEXUS3D.Formats.Schematic.Common;

/// <summary>
/// Options controlling schematic exporting, target format selection, compression, and verification.
/// </summary>
public sealed record SchematicWriteOptions
{
    public static SchematicWriteOptions Default { get; } = new();

    public SchematicFormatId TargetFormat { get; init; } = SchematicFormatId.SpongeV2;
    public int DataVersion { get; init; } = 3465;
    public bool StrictMapping { get; init; } = false;
    public bool PreserveUnknownData { get; init; } = true;
    public NbtCompressionType Compression { get; init; } = NbtCompressionType.GZip;
    public SchematicOffset? OverrideOffset { get; init; }
    public bool AllowOverwrite { get; init; } = true;
    public bool VerifyTemporaryOutputBeforeCommit { get; init; } = true;
}

