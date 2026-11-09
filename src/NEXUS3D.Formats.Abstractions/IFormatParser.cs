using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Scene;

namespace NEXUS3D.Formats.Abstractions;

/// <summary>
/// Parses a format stream or file directly into a Universal Scene Model (<see cref="Scene3D"/>).
/// </summary>
public interface IFormatParser
{
    string FormatIdentifier { get; }

    Task<Result<Scene3D>> ParseToSceneAsync(
        Stream source,
        ParseOptions? options = null,
        CancellationToken cancellationToken = default);
}

