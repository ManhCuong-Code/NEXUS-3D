using NEXUS3D.Domain.Materials;

namespace NEXUS3D.Formats.Obj.Materials;

/// <summary>
/// Represents a single material block (<c>newmtl &lt;name&gt;</c>) inside a Wavefront MTL file.
/// Supports all standard Wavefront illumination and texture map statements while preserving custom/unrecognized lines.
/// </summary>
public sealed class MtlMaterial
{
    public string Name { get; set; } = "DefaultMaterial";
    public int LineNumber { get; set; }

    /// <summary>Ambient reflectivity (<c>Ka r g b</c>).</summary>
    public MaterialColor? AmbientColor { get; set; }

    /// <summary>Diffuse reflectivity (<c>Kd r g b</c>).</summary>
    public MaterialColor? DiffuseColor { get; set; }

    /// <summary>Specular reflectivity (<c>Ks r g b</c>).</summary>
    public MaterialColor? SpecularColor { get; set; }

    /// <summary>Emissive color (<c>Ke r g b</c>).</summary>
    public MaterialColor? EmissiveColor { get; set; }

    /// <summary>Transmission filter color (<c>Tf r g b</c>).</summary>
    public MaterialColor? TransmissionFilter { get; set; }

    /// <summary>Specular exponent / shininess (<c>Ns</c>, typically 0..1000).</summary>
    public double? SpecularExponent { get; set; }

    /// <summary>Optical density / index of refraction (<c>Ni</c>, typically 0.001..10.0).</summary>
    public double? OpticalDensity { get; set; }

    /// <summary>Dissolve / opacity (<c>d</c>, 0.0 = transparent, 1.0 = opaque).</summary>
    public double? Dissolve { get; set; }

    /// <summary>Transparency (<c>Tr</c>, 0.0 = opaque, 1.0 = transparent; <c>Opacity = 1 - Tr</c>).</summary>
    public double? Transparency { get; set; }

    /// <summary>Wavefront illumination model (<c>illum</c>, 0..10).</summary>
    public int? IlluminationModel { get; set; }

    /// <summary>Ambient texture map (<c>map_Ka</c>).</summary>
    public MtlTextureMapStatement? MapKa { get; set; }

    /// <summary>Diffuse / albedo texture map (<c>map_Kd</c>).</summary>
    public MtlTextureMapStatement? MapKd { get; set; }

    /// <summary>Specular texture map (<c>map_Ks</c>).</summary>
    public MtlTextureMapStatement? MapKs { get; set; }

    /// <summary>Emissive texture map (<c>map_Ke</c>).</summary>
    public MtlTextureMapStatement? MapKe { get; set; }

    /// <summary>Specular highlight / shininess texture map (<c>map_Ns</c>).</summary>
    public MtlTextureMapStatement? MapNs { get; set; }

    /// <summary>Alpha / dissolve texture map (<c>map_d</c>).</summary>
    public MtlTextureMapStatement? MapD { get; set; }

    /// <summary>Bump texture map (<c>map_Bump</c> or <c>bump</c>).</summary>
    public MtlTextureMapStatement? MapBump { get; set; }

    /// <summary>Displacement texture map (<c>disp</c>).</summary>
    public MtlTextureMapStatement? Disp { get; set; }

    /// <summary>Stencil decal texture map (<c>decal</c>).</summary>
    public MtlTextureMapStatement? Decal { get; set; }

    /// <summary>Environment reflection texture map (<c>refl</c>).</summary>
    public MtlTextureMapStatement? Refl { get; set; }

    /// <summary>Normal map extension (<c>norm</c>).</summary>
    public MtlTextureMapStatement? Norm { get; set; }

    /// <summary>Custom or unrecognized MTL properties preserved for round-trip fidelity.</summary>
    public List<MtlProperty> CustomProperties { get; set; } = [];

    /// <summary>Comments encountered inside this material block.</summary>
    public List<string> Comments { get; set; } = [];

    public MtlMaterial()
    {
    }

    public MtlMaterial(string name, int lineNumber = 0)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "DefaultMaterial" : name.Trim();
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Computes the effective opacity in <c>[0.0, 1.0]</c> prioritizing <see cref="Dissolve"/> (<c>d</c>)
    /// and falling back to <c>1.0 - Tr</c> when <see cref="Transparency"/> (<c>Tr</c>) is specified.
    /// </summary>
    public double EffectiveOpacity
    {
        get
        {
            if (Dissolve.HasValue)
            {
                return Math.Clamp(Dissolve.Value, 0.0, 1.0);
            }

            if (Transparency.HasValue)
            {
                return Math.Clamp(1.0 - Transparency.Value, 0.0, 1.0);
            }

            return 1.0;
        }
    }

    /// <summary>
    /// Enumerates all non-null texture map statements defined on this material.
    /// </summary>
    public IEnumerable<(string SlotName, MtlTextureMapStatement Statement)> GetTextureMaps()
    {
        if (MapKd is not null) yield return ("Albedo", MapKd);
        if (MapKa is not null) yield return ("Ambient", MapKa);
        if (MapKs is not null) yield return ("Specular", MapKs);
        if (MapKe is not null) yield return ("Emissive", MapKe);
        if (MapNs is not null) yield return ("Shininess", MapNs);
        if (MapD is not null) yield return ("Opacity", MapD);
        if (MapBump is not null) yield return ("Bump", MapBump);
        if (Norm is not null) yield return ("Normal", Norm);
        if (Disp is not null) yield return ("Displacement", Disp);
        if (Decal is not null) yield return ("Decal", Decal);
        if (Refl is not null) yield return ("Reflection", Refl);
    }

    public MtlMaterial DeepClone() => new(Name, LineNumber)
    {
        AmbientColor = AmbientColor,
        DiffuseColor = DiffuseColor,
        SpecularColor = SpecularColor,
        EmissiveColor = EmissiveColor,
        TransmissionFilter = TransmissionFilter,
        SpecularExponent = SpecularExponent,
        OpticalDensity = OpticalDensity,
        Dissolve = Dissolve,
        Transparency = Transparency,
        IlluminationModel = IlluminationModel,
        MapKa = MapKa?.DeepClone(),
        MapKd = MapKd?.DeepClone(),
        MapKs = MapKs?.DeepClone(),
        MapKe = MapKe?.DeepClone(),
        MapNs = MapNs?.DeepClone(),
        MapD = MapD?.DeepClone(),
        MapBump = MapBump?.DeepClone(),
        Disp = Disp?.DeepClone(),
        Decal = Decal?.DeepClone(),
        Refl = Refl?.DeepClone(),
        Norm = Norm?.DeepClone(),
        CustomProperties = [.. CustomProperties],
        Comments = [.. Comments]
    };
}

