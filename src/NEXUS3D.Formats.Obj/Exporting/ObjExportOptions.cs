using System.Globalization;

namespace NEXUS3D.Formats.Obj.Exporting;

/// <summary>
/// Configuration options for exporting Wavefront OBJ (<c>.obj</c>), companion MTL (<c>.mtl</c>), and texture files.
/// </summary>
public sealed class ObjExportOptions
{
    /// <summary>Number of significant decimal digits for floating-point numbers (default: 6).</summary>
    public int FloatPrecision { get; init; } = 6;

    /// <summary>Whether to triangulate quads and N-gons using Ear-Clipping before writing faces (default: false).</summary>
    public bool TriangulatePolygons { get; init; }

    /// <summary>Whether to export vertex normals (<c>vn</c>) and face normal indices (default: true).</summary>
    public bool WriteNormals { get; init; } = true;

    /// <summary>Whether to export texture coordinates (<c>vt</c>) and face UV indices (default: true).</summary>
    public bool WriteTexCoords { get; init; } = true;

    /// <summary>Whether to write header and element comments (<c>#</c>) (default: true).</summary>
    public bool WriteComments { get; init; } = true;

    /// <summary>Whether to export a companion <c>.mtl</c> file alongside the <c>.obj</c> file when materials exist (default: true).</summary>
    public bool ExportCompanionMtl { get; init; } = true;

    /// <summary>Whether to copy referenced texture files to the export directory (default: false).</summary>
    public bool CopyTextures { get; init; }

    /// <summary>Whether to preserve relative subdirectories (e.g., <c>textures/albedo.png</c>) when copying textures (default: true).</summary>
    public bool PreserveTextureDirectoryStructure { get; init; } = true;

    /// <summary>Whether existing target files may be overwritten during export (default: true).</summary>
    public bool AllowOverwrite { get; init; } = true;

    /// <summary>Whether to verify exported files by parsing them back before finalizing atomic export (default: true).</summary>
    public bool VerifyAfterExport { get; init; } = true;

    /// <summary> Whether to restore preserved unrecognized OBJ statements on export (default: true).</summary>
    public bool WriteUnrecognizedStatements { get; init; } = true;

    /// <summary>Optional explicit companion <c>.mtl</c> file name (e.g., <c>materials.mtl</c>).</summary>
    public string? MtlFileName { get; init; }

    /// <summary>Optional source directory used to locate relative textures when copying textures on export.</summary>
    public string? SourceBaseDirectory { get; init; }

    public static ObjExportOptions Default { get; } = new();

    /// <summary>
    /// Builds the fixed-point or general format specifier corresponding to <see cref="FloatPrecision"/>.
    /// </summary>
    public string GetFloatFormatSpecifier()
    {
        int clamped = Math.Clamp(FloatPrecision, 1, 15);
        return "0." + new string('#', clamped);
    }

    public string FormatDouble(double value)
    {
        if (!double.IsFinite(value) || Math.Abs(value) < 1e-15)
        {
            return "0";
        }

        return value.ToString(GetFloatFormatSpecifier(), CultureInfo.InvariantCulture);
    }
}

