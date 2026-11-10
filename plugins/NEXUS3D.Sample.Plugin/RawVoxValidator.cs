using System.Text;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Sample.Plugin;

/// <summary>
/// Validates stream content against the RawVox format grammar.
/// </summary>
public sealed class RawVoxValidator : IFormatValidator
{
    public string FormatIdentifier => "RAWVOX";

    public async Task<ValidationResult> ValidateStreamAsync(
        Stream source,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        if (source == null || !source.CanRead)
        {
            result.AddError("STREAM_UNREADABLE", "Stream is null or unreadable.");
            return result;
        }

        long originalPos = source.CanSeek ? source.Position : 0;
        try
        {
            using var reader = new StreamReader(source, Encoding.UTF8, leaveOpen: true);
            string? firstLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (firstLine is null || !firstLine.StartsWith("RAWVOX1", StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("INVALID_HEADER", "File does not start with valid RAWVOX1 magic header.");
            }

            return result;
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

