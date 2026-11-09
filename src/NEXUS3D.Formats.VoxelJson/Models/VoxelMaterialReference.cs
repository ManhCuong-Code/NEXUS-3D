using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.VoxelJson.Models;

/// <summary>
/// Normalized RGBA color value for a voxel or voxel material.
/// Supports hex strings (<c>#RRGGBB</c>, <c>#RRGGBBAA</c>), 0..255 byte tuples, and 0..1 normalized floats.
/// </summary>
public readonly record struct VoxelColor(double R, double G, double B, double A = 1.0)
{
    public static VoxelColor White => new(1.0, 1.0, 1.0, 1.0);

    public bool IsValid =>
        double.IsFinite(R) && double.IsFinite(G) && double.IsFinite(B) && double.IsFinite(A) &&
        R is >= 0.0 and <= 1.0 &&
        G is >= 0.0 and <= 1.0 &&
        B is >= 0.0 and <= 1.0 &&
        A is >= 0.0 and <= 1.0;

    public string ToHexString(bool includeAlpha = false)
    {
        int r = Math.Clamp((int)Math.Round(R * 255.0), 0, 255);
        int g = Math.Clamp((int)Math.Round(G * 255.0), 0, 255);
        int b = Math.Clamp((int)Math.Round(B * 255.0), 0, 255);
        int a = Math.Clamp((int)Math.Round(A * 255.0), 0, 255);

        return includeAlpha || a < 255
            ? $"#{r:X2}{g:X2}{b:X2}{a:X2}"
            : $"#{r:X2}{g:X2}{b:X2}";
    }

    public static bool TryParseHex(string? hex, out VoxelColor color)
    {
        color = White;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        ReadOnlySpan<char> span = hex.AsSpan().Trim();
        if (span.StartsWith("#", StringComparison.Ordinal))
        {
            span = span[1..];
        }
        else if (span.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            span = span[2..];
        }

        if (span.Length == 6 &&
            byte.TryParse(span[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r6) &&
            byte.TryParse(span.Slice(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g6) &&
            byte.TryParse(span.Slice(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b6))
        {
            color = new VoxelColor(r6 / 255.0, g6 / 255.0, b6 / 255.0, 1.0);
            return true;
        }

        if (span.Length == 8 &&
            byte.TryParse(span[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r8) &&
            byte.TryParse(span.Slice(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g8) &&
            byte.TryParse(span.Slice(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b8) &&
            byte.TryParse(span.Slice(6, 2), System.Globalization.NumberStyles.HexNumber, null, out byte a8))
        {
            color = new VoxelColor(r8 / 255.0, g8 / 255.0, b8 / 255.0, a8 / 255.0);
            return true;
        }

        return false;
    }
}

/// <summary>
/// Material or texture reference entry inside a <see cref="VoxelDocument"/>.
/// </summary>
public sealed class VoxelMaterialReference
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public VoxelColor? Color { get; set; }
    public double Metallic { get; set; }
    public double Roughness { get; set; } = 0.5;
    public double Opacity { get; set; } = 1.0;
    public double Emissive { get; set; }
    public string? TextureReference { get; set; }
    public JsonExtensionData ExtensionData { get; set; } = new();

    public VoxelMaterialReference DeepClone() => new()
    {
        Id = Id,
        Name = Name,
        Color = Color,
        Metallic = Metallic,
        Roughness = Roughness,
        Opacity = Opacity,
        Emissive = Emissive,
        TextureReference = TextureReference,
        ExtensionData = ExtensionData.DeepClone()
    };
}
