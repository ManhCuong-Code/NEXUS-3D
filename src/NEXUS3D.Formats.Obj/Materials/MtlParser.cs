using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;

namespace NEXUS3D.Formats.Obj.Materials;

/// <summary>
/// Contract for parsing Wavefront Material Template Library (<c>.mtl</c>) documents.
/// </summary>
public interface IMtlParser
{
    Task<MtlParseResult> ParseFileAsync(
        string filePath,
        MtlParseOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<MtlParseResult> ParseAsync(
        Stream stream,
        string? sourcePath = null,
        MtlParseOptions? options = null,
        CancellationToken cancellationToken = default);

    MtlParseResult Parse(
        TextReader reader,
        string? sourcePath = null,
        MtlParseOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// High-fidelity parser and USM mapper for Wavefront MTL (<c>.mtl</c>) material libraries.
/// </summary>
public sealed class MtlParser : IMtlParser
{
    public const string CustomPropertyPrefix = "obj.mtl.";

    public async Task<MtlParseResult> ParseFileAsync(
        string filePath,
        MtlParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        options ??= MtlParseOptions.Default;

        if (!File.Exists(filePath))
        {
            if (options.StrictMode)
            {
                throw new ObjException(
                    ObjErrorCodes.MissingMtlFile,
                    $"Referenced MTL file was not found: '{filePath}'.");
            }

            return new MtlParseResult
            {
                Document = new MtlDocument { SourcePath = filePath },
                Issues =
                [
                    new ValidationIssue(
                        ObjErrorCodes.MissingMtlFile,
                        $"Referenced MTL file was not found: '{filePath}'. Continuing with default material fallback.",
                        ValidationSeverity.Warning,
                        filePath)
                ],
                Elapsed = TimeSpan.Zero
            };
        }

        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        return await ParseAsync(fileStream, filePath, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MtlParseResult> ParseAsync(
        Stream stream,
        string? sourcePath = null,
        MtlParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        await Task.Yield();

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 65536,
            leaveOpen: true);

        return Parse(reader, sourcePath, options, cancellationToken);
    }

    public MtlParseResult Parse(
        TextReader reader,
        string? sourcePath = null,
        MtlParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        options ??= MtlParseOptions.Default;

        var sw = Stopwatch.StartNew();
        var doc = new MtlDocument { SourcePath = sourcePath };
        var issues = new List<ValidationIssue>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        MtlMaterial? currentMaterial = null;
        int physicalLineNumber = 0;
        var continuationBuffer = new StringBuilder();
        int logicalLineStart = 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? rawLine = reader.ReadLine();
            if (rawLine is null)
            {
                if (continuationBuffer.Length > 0)
                {
                    ProcessLogicalLine(
                        continuationBuffer.ToString(),
                        logicalLineStart,
                        doc,
                        ref currentMaterial,
                        seenNames,
                        options,
                        issues);
                    continuationBuffer.Clear();
                }

                break;
            }

            physicalLineNumber++;
            if (rawLine.Length > options.MaxLineLength)
            {
                AddIssue(
                    issues,
                    options,
                    ObjErrorCodes.LimitExceeded,
                    $"MTL line {physicalLineNumber} exceeds maximum length ({rawLine.Length} > {options.MaxLineLength}).",
                    physicalLineNumber,
                    ValidationSeverity.Error);
            }

            ReadOnlySpan<char> trimmedEnd = rawLine.AsSpan().TrimEnd();
            if (trimmedEnd.Length > 0 && trimmedEnd[^1] == '\\' && !IsCommentLine(trimmedEnd))
            {
                if (continuationBuffer.Length == 0)
                {
                    logicalLineStart = physicalLineNumber;
                }

                continuationBuffer.Append(trimmedEnd[..^1]);
                continuationBuffer.Append(' ');
                continue;
            }

            string logicalLine;
            int lineNo;
            if (continuationBuffer.Length > 0)
            {
                continuationBuffer.Append(rawLine);
                logicalLine = continuationBuffer.ToString();
                lineNo = logicalLineStart;
                continuationBuffer.Clear();
            }
            else
            {
                logicalLine = rawLine;
                lineNo = physicalLineNumber;
            }

            ProcessLogicalLine(
                logicalLine,
                lineNo,
                doc,
                ref currentMaterial,
                seenNames,
                options,
                issues);
        }

        sw.Stop();
        return new MtlParseResult
        {
            Document = doc,
            Issues = issues,
            Elapsed = sw.Elapsed
        };
    }

    private static bool IsCommentLine(ReadOnlySpan<char> span)
    {
        ReadOnlySpan<char> trimmed = span.TrimStart();
        return trimmed.Length > 0 && trimmed[0] == '#';
    }

    private static void ProcessLogicalLine(
        string line,
        int lineNumber,
        MtlDocument doc,
        ref MtlMaterial? currentMaterial,
        HashSet<string> seenNames,
        MtlParseOptions options,
        List<ValidationIssue> issues)
    {
        ReadOnlySpan<char> trimmed = line.AsSpan().Trim();
        if (trimmed.IsEmpty)
        {
            return;
        }

        if (trimmed[0] == '#')
        {
            if (options.PreserveComments)
            {
                string comment = trimmed[1..].Trim().ToString();
                if (currentMaterial is null)
                {
                    doc.HeaderComments.Add(comment);
                }
                else
                {
                    currentMaterial.Comments.Add(comment);
                }
            }

            return;
        }

        // Strip inline '#' comment if any
        int hashIndex = trimmed.IndexOf('#');
        if (hashIndex >= 0)
        {
            trimmed = trimmed[..hashIndex].TrimEnd();
            if (trimmed.IsEmpty)
            {
                return;
            }
        }

        // Extract keyword and arguments
        int firstSpace = -1;
        for (int i = 0; i < trimmed.Length; i++)
        {
            if (char.IsWhiteSpace(trimmed[i]))
            {
                firstSpace = i;
                break;
            }
        }

        string keyword = firstSpace < 0 ? trimmed.ToString() : trimmed[..firstSpace].ToString();
        string args = firstSpace < 0 ? string.Empty : trimmed[(firstSpace + 1)..].Trim().ToString();

        if (string.Equals(keyword, "newmtl", StringComparison.OrdinalIgnoreCase))
        {
            string materialName = string.IsNullOrWhiteSpace(args) ? $"Material_{doc.Materials.Count + 1}" : args;
            if (string.IsNullOrWhiteSpace(args))
            {
                AddIssue(
                    issues,
                    options,
                    ObjErrorCodes.InvalidMtlSyntax,
                    $"MTL 'newmtl' statement at line {lineNumber} is missing a material name.",
                    lineNumber,
                    ValidationSeverity.Warning);
            }

            if (!seenNames.Add(materialName))
            {
                AddIssue(
                    issues,
                    options,
                    ObjErrorCodes.InvalidMtlValue,
                    $"Duplicate material name '{materialName}' in MTL at line {lineNumber}.",
                    lineNumber,
                    ValidationSeverity.Warning);
            }

            currentMaterial = new MtlMaterial(materialName, lineNumber);
            doc.Materials.Add(currentMaterial);
            return;
        }

        if (currentMaterial is null)
        {
            currentMaterial = new MtlMaterial("DefaultMaterial", lineNumber);
            seenNames.Add(currentMaterial.Name);
            doc.Materials.Add(currentMaterial);
            AddIssue(
                issues,
                options,
                ObjErrorCodes.InvalidMtlSyntax,
                $"MTL property '{keyword}' at line {lineNumber} appeared before any 'newmtl' declaration; assigned to 'DefaultMaterial'.",
                lineNumber,
                ValidationSeverity.Warning);
        }

        switch (keyword.ToLowerInvariant())
        {
            case "ka":
                if (TryParseColor(args, lineNumber, "Ka", options, issues, out MaterialColor ka))
                {
                    currentMaterial.AmbientColor = ka;
                }
                else if (options.PreserveCustomProperties)
                {
                    currentMaterial.CustomProperties.Add(new MtlProperty(lineNumber, keyword, args));
                }
                break;

            case "kd":
                if (TryParseColor(args, lineNumber, "Kd", options, issues, out MaterialColor kd))
                {
                    currentMaterial.DiffuseColor = kd;
                }
                else if (options.PreserveCustomProperties)
                {
                    currentMaterial.CustomProperties.Add(new MtlProperty(lineNumber, keyword, args));
                }
                break;

            case "ks":
                if (TryParseColor(args, lineNumber, "Ks", options, issues, out MaterialColor ks))
                {
                    currentMaterial.SpecularColor = ks;
                }
                else if (options.PreserveCustomProperties)
                {
                    currentMaterial.CustomProperties.Add(new MtlProperty(lineNumber, keyword, args));
                }
                break;

            case "ke":
                if (TryParseColor(args, lineNumber, "Ke", options, issues, out MaterialColor ke))
                {
                    currentMaterial.EmissiveColor = ke;
                }
                else if (options.PreserveCustomProperties)
                {
                    currentMaterial.CustomProperties.Add(new MtlProperty(lineNumber, keyword, args));
                }
                break;

            case "tf":
                if (TryParseColor(args, lineNumber, "Tf", options, issues, out MaterialColor tf))
                {
                    currentMaterial.TransmissionFilter = tf;
                }
                else if (options.PreserveCustomProperties)
                {
                    currentMaterial.CustomProperties.Add(new MtlProperty(lineNumber, keyword, args));
                }
                break;

            case "ns":
                if (TryParseScalar(args, lineNumber, "Ns", options, issues, out double ns))
                {
                    if (ns < 0.0 || ns > 10000.0)
                    {
                        AddIssue(
                            issues,
                            options,
                            ObjErrorCodes.InvalidMtlValue,
                            $"MTL 'Ns' value {ns} at line {lineNumber} is outside typical [0, 1000] range.",
                            lineNumber,
                            ValidationSeverity.Warning);
                    }

                    currentMaterial.SpecularExponent = Math.Max(0.0, ns);
                }
                break;

            case "ni":
                if (TryParseScalar(args, lineNumber, "Ni", options, issues, out double ni))
                {
                    if (ni < 0.001 || ni > 10.0)
                    {
                        AddIssue(
                            issues,
                            options,
                            ObjErrorCodes.InvalidMtlValue,
                            $"MTL 'Ni' (optical density) value {ni} at line {lineNumber} is outside [0.001, 10.0].",
                            lineNumber,
                            ValidationSeverity.Warning);
                    }

                    currentMaterial.OpticalDensity = ni;
                }
                break;

            case "d":
            {
                string dArgs = args;
                if (dArgs.StartsWith("-halo", StringComparison.OrdinalIgnoreCase))
                {
                    dArgs = dArgs[5..].TrimStart();
                }

                if (TryParseScalar(dArgs, lineNumber, "d", options, issues, out double dVal))
                {
                    if (dVal < 0.0 || dVal > 1.0)
                    {
                        AddIssue(
                            issues,
                            options,
                            ObjErrorCodes.InvalidMtlValue,
                            $"MTL 'd' (dissolve) value {dVal} at line {lineNumber} is outside [0.0, 1.0].",
                            lineNumber,
                            ValidationSeverity.Warning);
                    }

                    currentMaterial.Dissolve = Math.Clamp(dVal, 0.0, 1.0);
                }
                break;
            }

            case "tr":
                if (TryParseScalar(args, lineNumber, "Tr", options, issues, out double trVal))
                {
                    if (trVal < 0.0 || trVal > 1.0)
                    {
                        AddIssue(
                            issues,
                            options,
                            ObjErrorCodes.InvalidMtlValue,
                            $"MTL 'Tr' (transparency) value {trVal} at line {lineNumber} is outside [0.0, 1.0].",
                            lineNumber,
                            ValidationSeverity.Warning);
                    }

                    currentMaterial.Transparency = Math.Clamp(trVal, 0.0, 1.0);
                }
                break;

            case "illum":
                if (int.TryParse(args, NumberStyles.Integer, CultureInfo.InvariantCulture, out int illum))
                {
                    if (illum < 0 || illum > 10)
                    {
                        AddIssue(
                            issues,
                            options,
                            ObjErrorCodes.InvalidMtlValue,
                            $"MTL 'illum' value {illum} at line {lineNumber} is outside standard [0, 10].",
                            lineNumber,
                            ValidationSeverity.Warning);
                    }

                    currentMaterial.IlluminationModel = illum;
                }
                else
                {
                    AddIssue(
                        issues,
                        options,
                        ObjErrorCodes.InvalidMtlSyntax,
                        $"Invalid integer for MTL 'illum' at line {lineNumber}: '{args}'.",
                        lineNumber,
                        ValidationSeverity.Error);
                }
                break;

            case "map_ka":
                currentMaterial.MapKa = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "map_kd":
                currentMaterial.MapKd = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "map_ks":
                currentMaterial.MapKs = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "map_ke":
                currentMaterial.MapKe = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "map_ns":
                currentMaterial.MapNs = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "map_d":
                currentMaterial.MapD = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "map_bump":
            case "bump":
                currentMaterial.MapBump = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "disp":
                currentMaterial.Disp = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "decal":
                currentMaterial.Decal = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "refl":
                currentMaterial.Refl = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            case "norm":
                currentMaterial.Norm = ParseTextureMapStatement(keyword, args, lineNumber, options, issues);
                break;

            default:
                if (options.PreserveCustomProperties)
                {
                    currentMaterial.CustomProperties.Add(new MtlProperty(lineNumber, keyword, args));
                }
                break;
        }
    }

    private static bool TryParseColor(
        string args,
        int lineNumber,
        string keyword,
        MtlParseOptions options,
        List<ValidationIssue> issues,
        out MaterialColor color)
    {
        color = MaterialColor.White;
        if (string.IsNullOrWhiteSpace(args))
        {
            AddIssue(
                issues,
                options,
                ObjErrorCodes.InvalidMtlSyntax,
                $"MTL '{keyword}' at line {lineNumber} has no color components.",
                lineNumber,
                ValidationSeverity.Error);
            return false;
        }

        string[] parts = args.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 &&
            (string.Equals(parts[0], "spectral", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(parts[0], "xyz", StringComparison.OrdinalIgnoreCase)))
        {
            // Non-RGB color curves are preserved in CustomProperties
            return false;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double r) || !double.IsFinite(r))
        {
            AddIssue(
                issues,
                options,
                ObjErrorCodes.InvalidMtlSyntax,
                $"Invalid red component in MTL '{keyword}' at line {lineNumber}: '{args}'.",
                lineNumber,
                ValidationSeverity.Error);
            return false;
        }

        double g = r;
        double b = r;
        if (parts.Length >= 3)
        {
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out g) ||
                !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out b) ||
                !double.IsFinite(g) || !double.IsFinite(b))
            {
                AddIssue(
                    issues,
                    options,
                    ObjErrorCodes.InvalidMtlSyntax,
                    $"Invalid RGB components in MTL '{keyword}' at line {lineNumber}: '{args}'.",
                    lineNumber,
                    ValidationSeverity.Error);
                return false;
            }
        }
        else if (parts.Length == 2)
        {
            AddIssue(
                issues,
                options,
                ObjErrorCodes.InvalidMtlSyntax,
                $"MTL '{keyword}' at line {lineNumber} requires 1 or 3 components, found 2.",
                lineNumber,
                ValidationSeverity.Error);
            return false;
        }

        if (r < 0.0 || r > 1.0 || g < 0.0 || g > 1.0 || b < 0.0 || b > 1.0)
        {
            AddIssue(
                issues,
                options,
                ObjErrorCodes.InvalidMtlValue,
                $"MTL '{keyword}' color ({r}, {g}, {b}) at line {lineNumber} is outside normalized [0, 1] range; clamping to [0, 1].",
                lineNumber,
                ValidationSeverity.Warning);
        }

        color = new MaterialColor(
            Math.Clamp(r, 0.0, 1.0),
            Math.Clamp(g, 0.0, 1.0),
            Math.Clamp(b, 0.0, 1.0),
            1.0);
        return true;
    }

    private static bool TryParseScalar(
        string args,
        int lineNumber,
        string keyword,
        MtlParseOptions options,
        List<ValidationIssue> issues,
        out double value)
    {
        value = 0.0;
        string[] parts = args.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            !double.IsFinite(value))
        {
            AddIssue(
                issues,
                options,
                ObjErrorCodes.InvalidMtlSyntax,
                $"Invalid numeric value for MTL '{keyword}' at line {lineNumber}: '{args}'.",
                lineNumber,
                ValidationSeverity.Error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses a Wavefront MTL texture map statement including option flags (<c>-bm</c>, <c>-o</c>, <c>-s</c>, <c>-t</c>, <c>-clamp</c>, etc.)
    /// and a relative texture file path (preserving spaces in file/directory names).
    /// </summary>
    public static MtlTextureMapStatement? ParseTextureMapStatement(
        string keyword,
        string rawArgs,
        int lineNumber,
        MtlParseOptions options,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(rawArgs))
        {
            AddIssue(
                issues,
                options,
                ObjErrorCodes.InvalidMtlSyntax,
                $"MTL texture map '{keyword}' at line {lineNumber} is missing a file path.",
                lineNumber,
                ValidationSeverity.Error);
            return null;
        }

        var statement = new MtlTextureMapStatement { Keyword = keyword };

        // Tokenize with character start indices so we can slice the exact remaining path (including spaces)
        var tokens = TokenizeWithPositions(rawArgs);
        int index = 0;

        while (index < tokens.Count)
        {
            string token = tokens[index].Text;
            if (!token.StartsWith('-'))
            {
                break;
            }

            string lowerOpt = token.ToLowerInvariant();
            switch (lowerOpt)
            {
                case "-bm":
                    if (index + 1 < tokens.Count - 1 &&
                        double.TryParse(tokens[index + 1].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double bm))
                    {
                        statement.BumpMultiplier = bm;
                        index += 2;
                        continue;
                    }
                    break;

                case "-o":
                    index++;
                    statement.Offset = ConsumeVectorOption(tokens, ref index, Vector3D.Zero);
                    continue;

                case "-s":
                    index++;
                    statement.Scale = ConsumeVectorOption(tokens, ref index, Vector3D.One);
                    continue;

                case "-t":
                    index++;
                    statement.Turbulence = ConsumeVectorOption(tokens, ref index, Vector3D.Zero);
                    continue;

                case "-clamp":
                    if (index + 1 < tokens.Count - 1)
                    {
                        statement.Clamp = string.Equals(tokens[index + 1].Text, "on", StringComparison.OrdinalIgnoreCase);
                        index += 2;
                        continue;
                    }
                    break;

                case "-imfchan":
                    if (index + 1 < tokens.Count - 1)
                    {
                        statement.Channel = tokens[index + 1].Text;
                        index += 2;
                        continue;
                    }
                    break;

                case "-type":
                    if (index + 1 < tokens.Count - 1)
                    {
                        statement.ReflectionType = tokens[index + 1].Text;
                        index += 2;
                        continue;
                    }
                    break;

                case "-mm":
                    if (index + 2 < tokens.Count - 1)
                    {
                        statement.RawOptions.Add($"-mm {tokens[index + 1].Text} {tokens[index + 2].Text}");
                        index += 3;
                        continue;
                    }
                    break;

                case "-blendu":
                case "-blendv":
                case "-cc":
                case "-boost":
                case "-texres":
                    if (index + 1 < tokens.Count - 1)
                    {
                        statement.RawOptions.Add($"{token} {tokens[index + 1].Text}");
                        index += 2;
                        continue;
                    }
                    break;
            }

            // If we reach here, the token starts with '-' but isn't a recognized option with remaining filename tokens
            break;
        }

        if (index >= tokens.Count)
        {
            AddIssue(
                issues,
                options,
                ObjErrorCodes.InvalidMtlSyntax,
                $"MTL texture map '{keyword}' at line {lineNumber} contains options but no texture file path.",
                lineNumber,
                ValidationSeverity.Error);
            return null;
        }

        int pathStart = tokens[index].StartIndex;
        string rawPath = rawArgs[pathStart..].Trim();
        if (rawPath.Length >= 2 && rawPath[0] == '"' && rawPath[^1] == '"')
        {
            rawPath = rawPath[1..^1].Trim();
        }

        statement.TexturePath = rawPath;
        return statement;
    }

    private static Vector3D ConsumeVectorOption(
        List<(string Text, int StartIndex)> tokens,
        ref int index,
        Vector3D defaultVal)
    {
        double x = defaultVal.X;
        double y = defaultVal.Y;
        double z = defaultVal.Z;

        // Always leave at least 1 token at the end for the texture file path
        if (index < tokens.Count - 1 &&
            double.TryParse(tokens[index].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v0))
        {
            x = v0;
            index++;

            if (index < tokens.Count - 1 &&
                double.TryParse(tokens[index].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v1))
            {
                y = v1;
                index++;

                if (index < tokens.Count - 1 &&
                    double.TryParse(tokens[index].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v2))
                {
                    z = v2;
                    index++;
                }
            }
        }

        return new Vector3D(x, y, z);
    }

    private static List<(string Text, int StartIndex)> TokenizeWithPositions(string text)
    {
        var list = new List<(string Text, int StartIndex)>();
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                break;
            }

            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            list.Add((text[start..i], start));
        }

        return list;
    }

    /// <summary>
    /// Maps an <see cref="MtlMaterial"/> to the Universal Scene Model <see cref="MaterialData"/>,
    /// preserving all classic Wavefront properties in <see cref="MaterialData.CustomProperties"/>.
    /// </summary>
    public static MaterialData ToUsmMaterial(
        MtlMaterial mtl,
        Func<MtlTextureMapStatement, string, TextureSlot?>? textureSlotResolver = null)
    {
        ArgumentNullException.ThrowIfNull(mtl);

        double opacity = mtl.EffectiveOpacity;
        MaterialColor baseColor = mtl.DiffuseColor.HasValue
            ? mtl.DiffuseColor.Value with { A = opacity }
            : new MaterialColor(0.8, 0.8, 0.8, opacity);

        double roughness = 0.5;
        if (mtl.SpecularExponent.HasValue)
        {
            double normalizedNs = Math.Clamp(mtl.SpecularExponent.Value / 1000.0, 0.0, 1.0);
            roughness = Math.Clamp(1.0 - Math.Sqrt(normalizedNs), 0.0, 1.0);
        }

        var usm = new MaterialData(mtl.Name)
        {
            BaseColor = baseColor,
            Opacity = opacity,
            Roughness = roughness,
            Metallic = 0.0,
            TransparencyMode = opacity < 1.0 ? TransparencyMode.AlphaBlend : TransparencyMode.Opaque
        };

        // Resolve texture slots if callback provided
        foreach (var (slotName, statement) in mtl.GetTextureMaps())
        {
            TextureSlot? slot = textureSlotResolver?.Invoke(statement, slotName);
            if (slot is not null)
            {
                slot.Offset = new Vector2D(statement.Offset.X, statement.Offset.Y);
                slot.Scale = new Vector2D(statement.Scale.X, statement.Scale.Y);
                if (!string.IsNullOrWhiteSpace(statement.Channel))
                {
                    slot.Channel = statement.Channel;
                }

                usm.TextureSlots[slotName] = slot;
            }
        }

        // Preserve exact Wavefront MTL attributes in CustomProperties for lossless round-trip
        if (mtl.AmbientColor.HasValue)
        {
            SetJsonProperty(usm, "Ka", new[] { mtl.AmbientColor.Value.R, mtl.AmbientColor.Value.G, mtl.AmbientColor.Value.B });
        }

        if (mtl.DiffuseColor.HasValue)
        {
            SetJsonProperty(usm, "Kd", new[] { mtl.DiffuseColor.Value.R, mtl.DiffuseColor.Value.G, mtl.DiffuseColor.Value.B });
        }

        if (mtl.SpecularColor.HasValue)
        {
            SetJsonProperty(usm, "Ks", new[] { mtl.SpecularColor.Value.R, mtl.SpecularColor.Value.G, mtl.SpecularColor.Value.B });
        }

        if (mtl.EmissiveColor.HasValue)
        {
            SetJsonProperty(usm, "Ke", new[] { mtl.EmissiveColor.Value.R, mtl.EmissiveColor.Value.G, mtl.EmissiveColor.Value.B });
        }

        if (mtl.TransmissionFilter.HasValue)
        {
            SetJsonProperty(usm, "Tf", new[] { mtl.TransmissionFilter.Value.R, mtl.TransmissionFilter.Value.G, mtl.TransmissionFilter.Value.B });
        }

        if (mtl.SpecularExponent.HasValue)
        {
            SetJsonProperty(usm, "Ns", mtl.SpecularExponent.Value);
        }

        if (mtl.OpticalDensity.HasValue)
        {
            SetJsonProperty(usm, "Ni", mtl.OpticalDensity.Value);
        }

        if (mtl.Dissolve.HasValue)
        {
            SetJsonProperty(usm, "d", mtl.Dissolve.Value);
        }

        if (mtl.Transparency.HasValue)
        {
            SetJsonProperty(usm, "Tr", mtl.Transparency.Value);
        }

        if (mtl.IlluminationModel.HasValue)
        {
            SetJsonProperty(usm, "illum", mtl.IlluminationModel.Value);
        }

        foreach (var (slotName, statement) in mtl.GetTextureMaps())
        {
            SetJsonProperty(usm, $"map.{slotName}", statement);
        }

        if (mtl.CustomProperties.Count > 0)
        {
            SetJsonProperty(usm, "custom", mtl.CustomProperties);
        }

        if (mtl.Comments.Count > 0)
        {
            SetJsonProperty(usm, "comments", mtl.Comments);
        }

        return usm;
    }

    /// <summary>
    /// Reconstructs an <see cref="MtlMaterial"/> from a Universal Scene Model <see cref="MaterialData"/>.
    /// Uses preserved <c>obj.mtl.*</c> custom properties when available, or synthesizes standard MTL fields from USM PBR/diffuse properties.
    /// </summary>
    public static MtlMaterial FromUsmMaterial(
        MaterialData usm,
        Func<Guid, string?>? texturePathLookup = null)
    {
        ArgumentNullException.ThrowIfNull(usm);

        var mtl = new MtlMaterial(usm.Name)
        {
            DiffuseColor = new MaterialColor(usm.BaseColor.R, usm.BaseColor.G, usm.BaseColor.B, 1.0)
        };

        if (usm.Opacity < 1.0)
        {
            mtl.Dissolve = Math.Clamp(usm.Opacity, 0.0, 1.0);
        }

        if (TryGetColorProperty(usm, "Ka", out MaterialColor ka))
        {
            mtl.AmbientColor = ka;
        }

        if (TryGetColorProperty(usm, "Kd", out MaterialColor kd))
        {
            mtl.DiffuseColor = kd;
        }

        if (TryGetColorProperty(usm, "Ks", out MaterialColor ks))
        {
            mtl.SpecularColor = ks;
        }

        if (TryGetColorProperty(usm, "Ke", out MaterialColor ke))
        {
            mtl.EmissiveColor = ke;
        }

        if (TryGetColorProperty(usm, "Tf", out MaterialColor tf))
        {
            mtl.TransmissionFilter = tf;
        }

        if (TryGetDoubleProperty(usm, "Ns", out double ns))
        {
            mtl.SpecularExponent = ns;
        }
        else if (usm.Roughness >= 0.0 && usm.Roughness <= 1.0)
        {
            double smoothness = 1.0 - usm.Roughness;
            mtl.SpecularExponent = Math.Round(smoothness * smoothness * 1000.0, 4);
        }

        if (TryGetDoubleProperty(usm, "Ni", out double ni))
        {
            mtl.OpticalDensity = ni;
        }

        if (TryGetDoubleProperty(usm, "d", out double d))
        {
            mtl.Dissolve = d;
        }

        if (TryGetDoubleProperty(usm, "Tr", out double tr))
        {
            mtl.Transparency = tr;
        }

        if (usm.CustomProperties.TryGetValue(CustomPropertyPrefix + "illum", out JsonElement illumEl) &&
            illumEl.ValueKind == JsonValueKind.Number &&
            illumEl.TryGetInt32(out int illum))
        {
            mtl.IlluminationModel = illum;
        }

        // Restore texture maps from preserved statements or USM TextureSlots
        mtl.MapKd = RestoreTextureStatement(usm, "Albedo", "map_Kd", texturePathLookup);
        mtl.MapKa = RestoreTextureStatement(usm, "Ambient", "map_Ka", texturePathLookup);
        mtl.MapKs = RestoreTextureStatement(usm, "Specular", "map_Ks", texturePathLookup);
        mtl.MapKe = RestoreTextureStatement(usm, "Emissive", "map_Ke", texturePathLookup);
        mtl.MapNs = RestoreTextureStatement(usm, "Shininess", "map_Ns", texturePathLookup);
        mtl.MapD = RestoreTextureStatement(usm, "Opacity", "map_d", texturePathLookup);
        mtl.MapBump = RestoreTextureStatement(usm, "Bump", "map_Bump", texturePathLookup);
        mtl.Norm = RestoreTextureStatement(usm, "Normal", "norm", texturePathLookup);
        mtl.Disp = RestoreTextureStatement(usm, "Displacement", "disp", texturePathLookup);
        mtl.Decal = RestoreTextureStatement(usm, "Decal", "decal", texturePathLookup);
        mtl.Refl = RestoreTextureStatement(usm, "Reflection", "refl", texturePathLookup);

        if (usm.CustomProperties.TryGetValue(CustomPropertyPrefix + "custom", out JsonElement customEl))
        {
            var customList = customEl.Deserialize<List<MtlProperty>>();
            if (customList is not null)
            {
                mtl.CustomProperties.AddRange(customList);
            }
        }

        if (usm.CustomProperties.TryGetValue(CustomPropertyPrefix + "comments", out JsonElement commentsEl))
        {
            var comments = commentsEl.Deserialize<List<string>>();
            if (comments is not null)
            {
                mtl.Comments.AddRange(comments);
            }
        }

        return mtl;
    }

    private static MtlTextureMapStatement? RestoreTextureStatement(
        MaterialData usm,
        string slotName,
        string defaultKeyword,
        Func<Guid, string?>? texturePathLookup)
    {
        MtlTextureMapStatement? statement = null;
        if (usm.CustomProperties.TryGetValue($"{CustomPropertyPrefix}map.{slotName}", out JsonElement mapEl))
        {
            statement = mapEl.Deserialize<MtlTextureMapStatement>();
        }

        if (usm.TextureSlots.TryGetValue(slotName, out TextureSlot? slot))
        {
            string? resolvedPath = texturePathLookup?.Invoke(slot.TextureId);
            if (statement is not null)
            {
                if (!string.IsNullOrWhiteSpace(resolvedPath))
                {
                    statement.TexturePath = resolvedPath;
                }

                return statement;
            }

            if (!string.IsNullOrWhiteSpace(resolvedPath))
            {
                return new MtlTextureMapStatement(defaultKeyword, resolvedPath)
                {
                    Offset = new Vector3D(slot.Offset.X, slot.Offset.Y, 0.0),
                    Scale = new Vector3D(slot.Scale.X, slot.Scale.Y, 1.0),
                    Channel = slot.Channel
                };
            }
        }

        return statement;
    }

    private static void SetJsonProperty<T>(MaterialData usm, string suffix, T value)
    {
        usm.CustomProperties[CustomPropertyPrefix + suffix] = JsonSerializer.SerializeToElement(value);
    }

    private static bool TryGetColorProperty(MaterialData usm, string suffix, out MaterialColor color)
    {
        color = MaterialColor.White;
        if (usm.CustomProperties.TryGetValue(CustomPropertyPrefix + suffix, out JsonElement el) &&
            el.ValueKind == JsonValueKind.Array &&
            el.GetArrayLength() == 3)
        {
            double r = el[0].GetDouble();
            double g = el[1].GetDouble();
            double b = el[2].GetDouble();
            color = new MaterialColor(r, g, b, 1.0);
            return true;
        }

        return false;
    }

    private static bool TryGetDoubleProperty(MaterialData usm, string suffix, out double value)
    {
        value = 0.0;
        if (usm.CustomProperties.TryGetValue(CustomPropertyPrefix + suffix, out JsonElement el) &&
            el.ValueKind == JsonValueKind.Number &&
            el.TryGetDouble(out value))
        {
            return true;
        }

        return false;
    }

    private static void AddIssue(
        List<ValidationIssue> issues,
        MtlParseOptions options,
        string code,
        string message,
        int lineNumber,
        ValidationSeverity defaultSeverity)
    {
        ValidationSeverity severity = options.StrictMode && defaultSeverity == ValidationSeverity.Warning
            ? ValidationSeverity.Error
            : defaultSeverity;

        issues.Add(new ValidationIssue(code, message, severity, $"Line {lineNumber}"));

        if (options.StrictMode && severity >= ValidationSeverity.Error)
        {
            throw new ObjException(code, message, lineNumber: lineNumber);
        }
    }
}
