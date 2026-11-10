namespace NEXUS3D.Formats.Json.Abstractions;

/// <summary>
/// Common serialization and formatting options for exporting JSON-based 3D model documents.
/// </summary>
public record JsonWriteOptions
{
    public static JsonWriteOptions Default { get; } = new();

    /// <summary>Whether to format output JSON with indentation (default: true).</summary>
    public bool PrettyPrint { get; init; } = true;

    /// <summary>Number of decimal places for floating-point coordinate rounding on export (default: 6).</summary>
    public int CoordinatePrecision { get; init; } = 6;

    /// <summary>Whether to preserve original element order on export (default: true).</summary>
    public bool PreserveOrder { get; init; } = true;

    /// <summary>Whether to write document metadata fields (default: true).</summary>
    public bool PreserveMetadata { get; init; } = true;

    /// <summary>Whether to write preserved <see cref="JsonExtensionData"/> properties back to JSON (default: true).</summary>
    public bool WriteExtensionData { get; init; } = true;

    /// <summary>Whether existing target files may be overwritten (default: true).</summary>
    public bool AllowOverwrite { get; init; } = true;

    /// <summary>Whether to parse and verify the written JSON output before finalizing atomic export (default: true).</summary>
    public bool VerifyAfterWrite { get; init; } = true;

    public double RoundCoordinate(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.InvalidNumericValue,
                $"Cannot export non-finite coordinate value: {value}.");
        }

        if (Math.Abs(value) < 1e-12)
        {
            return 0.0;
        }

        int digits = Math.Clamp(CoordinatePrecision, 1, 15);
        return Math.Round(value, digits, MidpointRounding.AwayFromZero);
    }
}
