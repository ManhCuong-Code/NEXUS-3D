using System.Globalization;
using System.Text;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Exporting;

namespace NEXUS3D.Formats.Obj.Materials;

/// <summary>
/// Contract for exporting a Wavefront <see cref="MtlDocument"/> to <c>.mtl</c> streams or files.
/// </summary>
public interface IMtlExporter
{
    Task<IReadOnlyList<ValidationIssue>> ExportFileAsync(
        MtlDocument document,
        string mtlFilePath,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ValidationIssue>> ExportAsync(
        MtlDocument document,
        Stream outputStream,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<ValidationIssue> Export(
        MtlDocument document,
        TextWriter writer,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exports a Wavefront <see cref="MtlDocument"/> to a standards-compliant <c>.mtl</c> file.
/// Ensures unique material names, culture-invariant float formatting, and preservation of custom properties and texture map options.
/// </summary>
public sealed class MtlExporter : IMtlExporter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<IReadOnlyList<ValidationIssue>> ExportFileAsync(
        MtlDocument document,
        string mtlFilePath,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(mtlFilePath);
        options ??= ObjExportOptions.Default;

        if (File.Exists(mtlFilePath) && !options.AllowOverwrite)
        {
            throw new ObjException(
                ObjErrorCodes.ExportFailed,
                $"Target MTL file '{mtlFilePath}' already exists and AllowOverwrite is false.");
        }

        string? dir = Path.GetDirectoryName(mtlFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var fs = new FileStream(
            mtlFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        return await ExportAsync(document, fs, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ValidationIssue>> ExportAsync(
        MtlDocument document,
        Stream outputStream,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(outputStream);
        await Task.Yield();

        await using var writer = new StreamWriter(
            outputStream,
            Utf8NoBom,
            bufferSize: 65536,
            leaveOpen: true);

        IReadOnlyList<ValidationIssue> issues = Export(document, writer, options, cancellationToken);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return issues;
    }

    public IReadOnlyList<ValidationIssue> Export(
        MtlDocument document,
        TextWriter writer,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);
        options ??= ObjExportOptions.Default;

        var issues = new List<ValidationIssue>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string floatFormat = options.GetFloatFormatSpecifier();

        if (options.WriteComments)
        {
            writer.WriteLine("# NEXUS 3D - Wavefront MTL Exporter");
            for (int i = 0; i < document.HeaderComments.Count; i++)
            {
                string comment = document.HeaderComments[i];
                if (comment.StartsWith("NEXUS 3D - Wavefront MTL Exporter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                writer.Write("# ");
                writer.WriteLine(comment);
            }
        }

        for (int i = 0; i < document.Materials.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MtlMaterial mat = document.Materials[i];

            if (i > 0 || options.WriteComments)
            {
                writer.WriteLine();
            }

            string baseName = string.IsNullOrWhiteSpace(mat.Name) ? $"Material_{i + 1}" : mat.Name.Trim();
            string uniqueName = baseName;
            if (!usedNames.Add(uniqueName))
            {
                int suffix = 2;
                while (!usedNames.Add($"{baseName}_{suffix}"))
                {
                    suffix++;
                }

                uniqueName = $"{baseName}_{suffix}";
                issues.Add(new ValidationIssue(
                    ObjErrorCodes.InvalidMtlValue,
                    $"Duplicate material name '{baseName}' was disambiguated to '{uniqueName}' during MTL export.",
                    ValidationSeverity.Warning,
                    $"Material:{baseName}"));
            }

            if (options.WriteComments)
            {
                for (int c = 0; c < mat.Comments.Count; c++)
                {
                    writer.Write("# ");
                    writer.WriteLine(mat.Comments[c]);
                }
            }

            writer.Write("newmtl ");
            writer.WriteLine(uniqueName);

            if (mat.AmbientColor.HasValue)
            {
                WriteColor(writer, "Ka", mat.AmbientColor.Value, options);
            }

            if (mat.DiffuseColor.HasValue)
            {
                WriteColor(writer, "Kd", mat.DiffuseColor.Value, options);
            }

            if (mat.SpecularColor.HasValue)
            {
                WriteColor(writer, "Ks", mat.SpecularColor.Value, options);
            }

            if (mat.EmissiveColor.HasValue)
            {
                WriteColor(writer, "Ke", mat.EmissiveColor.Value, options);
            }

            if (mat.TransmissionFilter.HasValue)
            {
                WriteColor(writer, "Tf", mat.TransmissionFilter.Value, options);
            }

            if (mat.SpecularExponent.HasValue)
            {
                writer.Write("Ns ");
                writer.WriteLine(options.FormatDouble(mat.SpecularExponent.Value));
            }

            if (mat.OpticalDensity.HasValue)
            {
                writer.Write("Ni ");
                writer.WriteLine(options.FormatDouble(mat.OpticalDensity.Value));
            }

            if (mat.Dissolve.HasValue)
            {
                writer.Write("d ");
                writer.WriteLine(options.FormatDouble(Math.Clamp(mat.Dissolve.Value, 0.0, 1.0)));
            }

            if (mat.Transparency.HasValue)
            {
                writer.Write("Tr ");
                writer.WriteLine(options.FormatDouble(Math.Clamp(mat.Transparency.Value, 0.0, 1.0)));
            }

            if (mat.IlluminationModel.HasValue)
            {
                writer.Write("illum ");
                writer.WriteLine(mat.IlluminationModel.Value.ToString(CultureInfo.InvariantCulture));
            }

            // Write texture maps
            WriteTextureStatement(writer, mat.MapKa, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.MapKd, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.MapKs, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.MapKe, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.MapNs, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.MapD, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.MapBump, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.Disp, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.Decal, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.Refl, floatFormat, uniqueName, issues);
            WriteTextureStatement(writer, mat.Norm, floatFormat, uniqueName, issues);

            // Restore preserved custom properties
            for (int p = 0; p < mat.CustomProperties.Count; p++)
            {
                MtlProperty prop = mat.CustomProperties[p];
                if (string.IsNullOrWhiteSpace(prop.Keyword))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(prop.RawValue))
                {
                    writer.WriteLine(prop.Keyword);
                }
                else
                {
                    writer.Write(prop.Keyword);
                    writer.Write(' ');
                    writer.WriteLine(prop.RawValue);
                }
            }
        }

        return issues;
    }

    private static void WriteColor(
        TextWriter writer,
        string keyword,
        MaterialColor color,
        ObjExportOptions options)
    {
        writer.Write(keyword);
        writer.Write(' ');
        writer.Write(options.FormatDouble(Math.Clamp(color.R, 0.0, 1.0)));
        writer.Write(' ');
        writer.Write(options.FormatDouble(Math.Clamp(color.G, 0.0, 1.0)));
        writer.Write(' ');
        writer.WriteLine(options.FormatDouble(Math.Clamp(color.B, 0.0, 1.0)));
    }

    private static void WriteTextureStatement(
        TextWriter writer,
        MtlTextureMapStatement? statement,
        string floatFormat,
        string materialName,
        List<ValidationIssue> issues)
    {
        if (statement is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(statement.TexturePath))
        {
            issues.Add(new ValidationIssue(
                ObjErrorCodes.MissingTextureFile,
                $"Skipping empty texture path for '{statement.Keyword}' in material '{materialName}'.",
                ValidationSeverity.Warning,
                $"Material:{materialName}"));
            return;
        }

        string keyword = string.IsNullOrWhiteSpace(statement.Keyword) ? "map_Kd" : statement.Keyword;
        writer.Write(keyword);
        writer.Write(' ');
        writer.WriteLine(statement.FormatArguments(floatFormat));
    }
}
