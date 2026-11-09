using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Formats.Abstractions;

/// <summary>
/// Describes a 3D model file format supported by the NEXUS 3D ecosystem.
/// </summary>
public interface IFormatDescriptor
{
    ModelFormatType FormatType { get; }
    string DisplayName { get; }
    string ShortCode { get; }
    string PrimaryExtension { get; }
    IReadOnlyList<string> SupportedExtensions { get; }
    GeometryKind NativeGeometryKind { get; }
    bool SupportsImport { get; }
    bool SupportsExport { get; }
    string Description { get; }

    bool CanHandleExtension(string filePathOrExtension);
}

/// <summary>
/// Foundational parser contract for inspecting and reading a 3D format.
/// Actual parsing algorithms will be implemented in subsequent stages.
/// </summary>
public interface IModelFormatParser
{
    ModelFormatType SupportedFormat { get; }
    Task<OperationResult<ModelDocumentSummary>> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Foundational exporter contract for serializing internal 3D representations to a target file format.
/// </summary>
public interface IModelFormatExporter
{
    ModelFormatType TargetFormat { get; }
    Task<OperationResult> ValidateTargetAsync(
        string outputFilePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Central registry of all discovered 3D format descriptors.
/// </summary>
public interface IFormatRegistry
{
    IReadOnlyList<IFormatDescriptor> GetAllFormats();
    IReadOnlyList<IFormatDescriptor> GetImportableFormats();
    IReadOnlyList<IFormatDescriptor> GetExportableFormats();
    IFormatDescriptor? GetByFormatType(ModelFormatType formatType);
    IFormatDescriptor? ResolveByFilePath(string filePath);
    string BuildFileDialogFilter(bool includeAllSupported = true);
}

