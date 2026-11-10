using System.Text;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Sample.Plugin;

/// <summary>
/// Detects RawVox 3D stream headers based on the RAWVOX1 magic header.
/// </summary>
public sealed class RawVoxDetector : IFormatDetector
{
    private static readonly byte[] MagicHeader = "RAWVOX1"u8.ToArray();

    public async Task<FormatDetectionResult> DetectAsync(
        Stream source,
        string? fileExtensionHint = null,
        CancellationToken cancellationToken = default)
    {
        if (source == null || !source.CanRead)
        {
            return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "Unreadable stream.");
        }

        long originalPos = source.CanSeek ? source.Position : 0;
        try
        {
            byte[] buffer = new byte[MagicHeader.Length];
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (read >= MagicHeader.Length && buffer.AsSpan().SequenceEqual(MagicHeader))
            {
                return new FormatDetectionResult(
                    IsRecognized: true,
                    FormatType: ModelFormatType.Custom,
                    FormatId: "RAWVOX",
                    Version: 1,
                    Details: "RawVox 3D Voxel Format Specification v1.0");
            }

            // Fallback: Check extension hint
            if (!string.IsNullOrEmpty(fileExtensionHint) &&
                (fileExtensionHint.EndsWith(".rawvox", StringComparison.OrdinalIgnoreCase) ||
                 fileExtensionHint.EndsWith(".rvox", StringComparison.OrdinalIgnoreCase)))
            {
                return new FormatDetectionResult(
                    IsRecognized: true,
                    FormatType: ModelFormatType.Custom,
                    FormatId: "RAWVOX",
                    Version: 1,
                    Details: "Detected by file extension hint");
            }

            return new FormatDetectionResult(false, ModelFormatType.Unknown, string.Empty, 0, "Header does not match RAWVOX1.");
        }
        finally
        {
            if (source.CanSeek)
            {
                source.Position = originalPos;
            }
        }
    }
}

