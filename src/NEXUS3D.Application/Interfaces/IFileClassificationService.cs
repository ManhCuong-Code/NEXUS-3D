using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;

namespace NEXUS3D.Application.Interfaces;

/// <summary>
/// Service responsible for deep content inspection, binary signature analysis, and smart format categorization
/// prior to opening a 3D model into its specialized workspace.
/// </summary>
public interface IFileClassificationService
{
    /// <summary>
    /// Performs asynchronous header inspection, schema verification, and capability mapping.
    /// </summary>
    Task<FileClassificationReport> ClassifyFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs fast synchronous classification using filename patterns and cached headers.
    /// </summary>
    FileClassificationReport ClassifyFile(string filePath);

    /// <summary>
    /// Resolves the workspace category for a specific format type.
    /// </summary>
    FormatCategory GetCategoryForFormat(ModelFormatType formatType);

    /// <summary>
    /// Returns all registered format descriptors matching a given category.
    /// </summary>
    IReadOnlyList<IFormatDescriptor> GetFormatsByCategory(FormatCategory category);

    /// <summary>
    /// Returns all supported workspace categories.
    /// </summary>
    IReadOnlyList<FormatCategory> GetAllCategories();
}
