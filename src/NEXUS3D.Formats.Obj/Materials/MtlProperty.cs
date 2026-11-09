using System.Globalization;
using NEXUS3D.Domain.Mathematics;

namespace NEXUS3D.Formats.Obj.Materials;

/// <summary>
/// Represents an unmapped or custom property line inside a Wavefront MTL material definition.
/// Preserved for lossless round-trip export.
/// </summary>
public readonly record struct MtlProperty(
    int LineNumber,
    string Keyword,
    string RawValue);

/// <summary>
/// Represents a parsed MTL texture map statement (e.g., <c>map_Kd</c>, <c>map_Bump</c>, <c>bump</c>, <c>disp</c>, <c>decal</c>, <c>refl</c>, <c>norm</c>)
/// including optional MTL texture flags (<c>-bm</c>, <c>-o</c>, <c>-s</c>, <c>-t</c>, <c>-clamp</c>, etc.) and relative texture path (supporting spaces).
/// </summary>
public sealed class MtlTextureMapStatement
{
    public string Keyword { get; set; } = "map_Kd";
    public string TexturePath { get; set; } = string.Empty;
    public Vector3D Offset { get; set; } = Vector3D.Zero;
    public Vector3D Scale { get; set; } = Vector3D.One;
    public Vector3D Turbulence { get; set; } = Vector3D.Zero;
    public double? BumpMultiplier { get; set; }
    public bool? Clamp { get; set; }
    public string? Channel { get; set; }
    public string? ReflectionType { get; set; }
    public List<string> RawOptions { get; set; } = [];

    public MtlTextureMapStatement()
    {
    }

    public MtlTextureMapStatement(string keyword, string texturePath)
    {
        Keyword = keyword;
        TexturePath = texturePath;
    }

    public MtlTextureMapStatement DeepClone() => new(Keyword, TexturePath)
    {
        Offset = Offset,
        Scale = Scale,
        Turbulence = Turbulence,
        BumpMultiplier = BumpMultiplier,
        Clamp = Clamp,
        Channel = Channel,
        ReflectionType = ReflectionType,
        RawOptions = [.. RawOptions]
    };

    /// <summary>
    /// Formats the texture statement arguments (options + texture path) for MTL export.
    /// </summary>
    public string FormatArguments(string floatFormat = "G6")
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(ReflectionType))
        {
            parts.Add($"-type {ReflectionType}");
        }

        if (BumpMultiplier.HasValue)
        {
            parts.Add($"-bm {BumpMultiplier.Value.ToString(floatFormat, CultureInfo.InvariantCulture)}");
        }

        if (Offset != Vector3D.Zero)
        {
            parts.Add(
                $"-o {Offset.X.ToString(floatFormat, CultureInfo.InvariantCulture)} " +
                $"{Offset.Y.ToString(floatFormat, CultureInfo.InvariantCulture)} " +
                $"{Offset.Z.ToString(floatFormat, CultureInfo.InvariantCulture)}");
        }

        if (Scale != Vector3D.One)
        {
            parts.Add(
                $"-s {Scale.X.ToString(floatFormat, CultureInfo.InvariantCulture)} " +
                $"{Scale.Y.ToString(floatFormat, CultureInfo.InvariantCulture)} " +
                $"{Scale.Z.ToString(floatFormat, CultureInfo.InvariantCulture)}");
        }

        if (Turbulence != Vector3D.Zero)
        {
            parts.Add(
                $"-t {Turbulence.X.ToString(floatFormat, CultureInfo.InvariantCulture)} " +
                $"{Turbulence.Y.ToString(floatFormat, CultureInfo.InvariantCulture)} " +
                $"{Turbulence.Z.ToString(floatFormat, CultureInfo.InvariantCulture)}");
        }

        if (Clamp.HasValue)
        {
            parts.Add($"-clamp {(Clamp.Value ? "on" : "off")}");
        }

        if (!string.IsNullOrWhiteSpace(Channel))
        {
            parts.Add($"-imfchan {Channel}");
        }

        for (int i = 0; i < RawOptions.Count; i++)
        {
            parts.Add(RawOptions[i]);
        }

        parts.Add(TexturePath);
        return string.Join(' ', parts);
    }
}

