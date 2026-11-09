using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Formats.Abstractions;

/// <summary>
/// Common contract for validating a 3D/schematic stream or parsed document against format rules.
/// </summary>
public interface IFormatValidator
{
    string FormatIdentifier { get; }

    Task<ValidationResult> ValidateStreamAsync(
        Stream source,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default);
}

