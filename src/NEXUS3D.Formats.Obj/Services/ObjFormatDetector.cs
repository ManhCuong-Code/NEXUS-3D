using System.Text;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Formats.Obj.Services;

/// <summary>
/// Specialized detector contract for Wavefront OBJ (<c>.obj</c>) and MTL (<c>.mtl</c>) streams.
/// </summary>
public interface IObjFormatDetector : IFormatDetector
{
    bool IsLikelyObjContent(ReadOnlySpan<char> textSample);
}

/// <summary>
/// Inspects file extensions and text stream headers to detect Wavefront OBJ (<c>.obj</c>) documents without mutating stream position.
/// </summary>
public sealed class ObjFormatDetector : IObjFormatDetector
{
    private const int SampleByteCount = 8192;

    private static readonly HashSet<string> PrimaryGeometryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "v", "vt", "vn", "vp", "f", "l", "p"
    };

    private static readonly HashSet<string> StructuralKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "o", "g", "s", "mtllib", "usemtl"
    };

    public async Task<FormatDetectionResult> DetectAsync(
        Stream source,
        string? fileExtensionHint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        long originalPosition = source.CanSeek ? source.Position : 0;
        try
        {
            byte[] buffer = new byte[SampleByteCount];
            int bytesRead = await source.ReadAsync(buffer.AsMemory(0, SampleByteCount), cancellationToken).ConfigureAwait(false);

            if (bytesRead == 0)
            {
                return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "Empty stream.");
            }

            // Reject binary streams containing null bytes (outside UTF-8 BOM)
            int startOffset = 0;
            if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                startOffset = 3;
            }

            for (int i = startOffset; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                {
                    return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "Binary stream detected.");
                }
            }

            string sample = Encoding.UTF8.GetString(buffer, startOffset, bytesRead - startOffset);
            bool hasObjExtension = !string.IsNullOrWhiteSpace(fileExtensionHint) &&
                fileExtensionHint.Trim().EndsWith(".obj", StringComparison.OrdinalIgnoreCase);

            if (IsLikelyObjContent(sample))
            {
                return new FormatDetectionResult(
                    IsRecognized: true,
                    FormatType: ModelFormatType.Obj,
                    FormatId: "wavefront:obj",
                    Version: 1,
                    Details: "Wavefront OBJ ASCII mesh.");
            }

            if (hasObjExtension && sample.AsSpan().TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                return new FormatDetectionResult(
                    IsRecognized: true,
                    FormatType: ModelFormatType.Obj,
                    FormatId: "wavefront:obj",
                    Version: 1,
                    Details: "Wavefront OBJ comment header with .obj extension.");
            }

            return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "No Wavefront OBJ statements found.");
        }
        finally
        {
            if (source.CanSeek)
            {
                source.Position = originalPosition;
            }
        }
    }

    public bool IsLikelyObjContent(ReadOnlySpan<char> textSample)
    {
        if (textSample.IsEmpty)
        {
            return false;
        }

        int geometryStatementCount = 0;
        int structuralStatementCount = 0;
        int unrecognizedLineCount = 0;

        foreach (ReadOnlySpan<char> rawLine in textSample.EnumerateLines())
        {
            ReadOnlySpan<char> trimmed = rawLine.Trim();
            if (trimmed.IsEmpty || trimmed[0] == '#')
            {
                continue;
            }

            // JSON or XML starts with '{', '[', '<'
            if (trimmed[0] is '{' or '[' or '<')
            {
                return false;
            }

            int spaceIdx = -1;
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (char.IsWhiteSpace(trimmed[i]))
                {
                    spaceIdx = i;
                    break;
                }
            }

            if (spaceIdx <= 0)
            {
                continue;
            }

            string keyword = trimmed[..spaceIdx].ToString();
            if (PrimaryGeometryKeywords.Contains(keyword))
            {
                geometryStatementCount++;
            }
            else if (StructuralKeywords.Contains(keyword))
            {
                structuralStatementCount++;
            }
            else
            {
                unrecognizedLineCount++;
            }
        }

        return geometryStatementCount > 0 || (structuralStatementCount >= 2 && unrecognizedLineCount == 0);
    }
}

