using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.Interfaces;

/// <summary>
/// Universal model loading and format detection service coordinating parsing into Universal Scene Model (USM).
/// </summary>
public interface IModelLoadingService
{
    /// <summary>
    /// Detects the target format descriptor based on file extension, compound extensions (.geo.json, .voxel.json),
    /// and JSON content sniffing when ambiguous.
    /// </summary>
    IFormatDescriptor? DetectFormat(string filePath);

    /// <summary>
    /// Resolves the appropriate parser and loads the model into USM Scene3D.
    /// </summary>
    Task<OperationResult<Scene3D>> LoadModelAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a model directly from a stream given a filename or format hint.
    /// </summary>
    Task<OperationResult<Scene3D>> LoadModelFromStreamAsync(Stream stream, string fileNameOrExtension, CancellationToken cancellationToken = default);
}

