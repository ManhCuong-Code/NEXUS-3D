using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.UseCases;

/// <summary>
/// Universal model loading service providing format identification, content sniffing, and parser orchestration.
/// </summary>
public sealed class ModelLoadingService : IModelLoadingService
{
    private readonly IFormatRegistry _formatRegistry;
    private readonly IEnumerable<IFormatParser> _parsers;
    private readonly ILogger<ModelLoadingService> _logger;

    public ModelLoadingService(
        IFormatRegistry formatRegistry,
        IEnumerable<IFormatParser> parsers,
        ILogger<ModelLoadingService> logger)
    {
        _formatRegistry = Guard.NotNull(formatRegistry);
        _parsers = Guard.NotNull(parsers);
        _logger = Guard.NotNull(logger);
    }

    public IFormatDescriptor? DetectFormat(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        string fileName = Path.GetFileName(filePath).ToLowerInvariant();

        // 1. Check compound extensions first
        if (fileName.EndsWith(".geo.json", StringComparison.OrdinalIgnoreCase))
        {
            return _formatRegistry.GetByFormatType(ModelFormatType.BedrockGeometry)
                ?? _formatRegistry.GetAllFormats().FirstOrDefault(d => d.ShortCode.Equals("GEO", StringComparison.OrdinalIgnoreCase));
        }

        if (fileName.EndsWith(".voxel.json", StringComparison.OrdinalIgnoreCase))
        {
            return _formatRegistry.GetByFormatType(ModelFormatType.VoxelJson)
                ?? _formatRegistry.GetAllFormats().FirstOrDefault(d => d.ShortCode.Equals("JSON", StringComparison.OrdinalIgnoreCase));
        }

        // 2. Generic .json - sniff content to distinguish Bedrock Geometry from Voxel JSON
        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(filePath))
            {
                try
                {
                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
                    char[] buffer = new char[4096];
                    int read = sr.Read(buffer, 0, buffer.Length);
                    string snippet = new string(buffer, 0, read);

                    if (snippet.Contains("minecraft:geometry", StringComparison.OrdinalIgnoreCase) ||
                        snippet.Contains("geometry.", StringComparison.OrdinalIgnoreCase) ||
                        (snippet.Contains("format_version", StringComparison.OrdinalIgnoreCase) && snippet.Contains("bones", StringComparison.OrdinalIgnoreCase)))
                    {
                        return _formatRegistry.GetByFormatType(ModelFormatType.BedrockGeometry);
                    }

                    if (snippet.Contains("elements", StringComparison.OrdinalIgnoreCase) ||
                        snippet.Contains("voxels", StringComparison.OrdinalIgnoreCase) ||
                        snippet.Contains("palette", StringComparison.OrdinalIgnoreCase) ||
                        snippet.Contains("voxel_size", StringComparison.OrdinalIgnoreCase) ||
                        snippet.Contains("ambientocclusion", StringComparison.OrdinalIgnoreCase) ||
                        (snippet.Contains("textures", StringComparison.OrdinalIgnoreCase) && snippet.Contains("parent", StringComparison.OrdinalIgnoreCase)))
                    {
                        return _formatRegistry.GetByFormatType(ModelFormatType.VoxelJson);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Content sniffing on {Path} failed, falling back to registry resolution.", filePath);
                }
            }

            // Fallback for .json: check filename hints, default to VoxelJson
            return fileName.Contains("bedrock", StringComparison.OrdinalIgnoreCase) || fileName.Contains("geo", StringComparison.OrdinalIgnoreCase)
                ? (_formatRegistry.GetByFormatType(ModelFormatType.BedrockGeometry) ?? _formatRegistry.GetByFormatType(ModelFormatType.VoxelJson))
                : (_formatRegistry.GetByFormatType(ModelFormatType.VoxelJson) ?? _formatRegistry.GetByFormatType(ModelFormatType.BedrockGeometry));
        }

        // 3. Known single extensions
        if (fileName.EndsWith(".schem", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".schematic", StringComparison.OrdinalIgnoreCase))
        {
            return _formatRegistry.GetByFormatType(ModelFormatType.Schematic);
        }

        if (fileName.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
        {
            return _formatRegistry.GetByFormatType(ModelFormatType.Obj);
        }

        // 4. Fallback to format registry file resolution (supports plugins and custom formats)
        var registryMatch = _formatRegistry.ResolveByFilePath(filePath);
        if (registryMatch is not null)
        {
            return registryMatch;
        }

        // 5. Broad scan across all registered formats
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        return _formatRegistry.GetAllFormats().FirstOrDefault(d =>
            d.SupportedExtensions.Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<OperationResult<Scene3D>> LoadModelAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return OperationResult<Scene3D>.Failure("FILE_NOT_FOUND", $"The specified 3D model file does not exist: '{filePath}'.");
        }

        try
        {
            var descriptor = DetectFormat(filePath);
            if (descriptor is null)
            {
                return OperationResult<Scene3D>.Failure("UNSUPPORTED_FORMAT", $"Unsupported or unrecognized 3D format: '{Path.GetExtension(filePath)}'.");
            }

            var parser = ResolveParser(descriptor);
            if (parser is null)
            {
                return OperationResult<Scene3D>.Failure("PARSER_NOT_FOUND", $"No parser available for format '{descriptor.DisplayName}' ({descriptor.ShortCode}).");
            }

            _logger.LogInformation("Loading 3D model '{Path}' using parser '{ParserId}' ({Format})",
                Path.GetFileName(filePath), parser.FormatIdentifier, descriptor.DisplayName);

            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 65536, useAsync: true);
            var parseOptions = new ParseOptions
            {
                StrictValidation = false,
                PreserveUnknownData = true,
                SourceFileName = Path.GetFileName(filePath)
            };

            var parseResult = await parser.ParseToSceneAsync(stream, parseOptions, cancellationToken);
            if ((parseResult.IsFailure || parseResult.Value is null) && Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                // If initial JSON parser failed, attempt alternate JSON parser (Bedrock <-> VoxelJson)
                var alternateType = descriptor.FormatType == ModelFormatType.BedrockGeometry
                    ? ModelFormatType.VoxelJson
                    : ModelFormatType.BedrockGeometry;
                var alternateDesc = _formatRegistry.GetByFormatType(alternateType);
                if (alternateDesc is not null)
                {
                    var altParser = ResolveParser(alternateDesc);
                    if (altParser is not null && !ReferenceEquals(altParser, parser))
                    {
                        stream.Position = 0;
                        parseResult = await altParser.ParseToSceneAsync(stream, parseOptions, cancellationToken);
                        if (parseResult.IsSuccess && parseResult.Value is not null)
                        {
                            descriptor = alternateDesc;
                            parser = altParser;
                        }
                    }
                }
            }

            if (parseResult.IsFailure || parseResult.Value is null)
            {
                string errMsg = !string.IsNullOrWhiteSpace(parseResult.Error.Message) ? parseResult.Error.Message : "Parsing produced no scene data.";
                _logger.LogWarning("Failed to parse 3D model '{Path}': {Error}", filePath, errMsg);
                return OperationResult<Scene3D>.Failure("PARSE_FAILED", errMsg);
            }

            var scene = parseResult.Value;
            if (string.IsNullOrWhiteSpace(scene.Name))
            {
                scene.Name = Path.GetFileNameWithoutExtension(filePath);
            }

            _logger.LogInformation("Successfully loaded 3D model '{Name}' ({Nodes} nodes, {Materials} materials).",
                scene.Name, scene.Graph.Root.Children.Count, scene.Materials.Count);

            return OperationResult<Scene3D>.Success(scene);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<Scene3D>.Failure("CANCELLED", "Model loading operation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading model file '{Path}'", filePath);
            return OperationResult<Scene3D>.Failure("LOAD_ERROR", $"Failed to open model: {ex.Message}");
        }
    }

    public async Task<OperationResult<Scene3D>> LoadModelFromStreamAsync(
        Stream stream,
        string fileNameOrExtension,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            var descriptor = DetectFormat(fileNameOrExtension);
            if (descriptor is null)
            {
                return OperationResult<Scene3D>.Failure("UNSUPPORTED_FORMAT", $"Unsupported or unrecognized format identifier: '{fileNameOrExtension}'.");
            }

            var parser = ResolveParser(descriptor);
            if (parser is null)
            {
                return OperationResult<Scene3D>.Failure("PARSER_NOT_FOUND", $"No parser available for format '{descriptor.DisplayName}'.");
            }

            var parseOptions = new ParseOptions
            {
                StrictValidation = false,
                PreserveUnknownData = true,
                SourceFileName = fileNameOrExtension
            };

            var parseResult = await parser.ParseToSceneAsync(stream, parseOptions, cancellationToken);
            if (parseResult.IsFailure || parseResult.Value is null)
            {
                string errMsg = !string.IsNullOrWhiteSpace(parseResult.Error.Message) ? parseResult.Error.Message : "Stream parsing failed.";
                return OperationResult<Scene3D>.Failure("PARSE_FAILED", errMsg);
            }

            return OperationResult<Scene3D>.Success(parseResult.Value);
        }
        catch (Exception ex)
        {
            return OperationResult<Scene3D>.Failure("STREAM_ERROR", ex.Message);
        }
    }

    private IFormatParser? ResolveParser(IFormatDescriptor descriptor)
    {
        // 1. Direct type match via IModelFormatParser
        var modelParser = _parsers.OfType<IModelFormatParser>().FirstOrDefault(p => p.SupportedFormat == descriptor.FormatType);
        if (modelParser is IFormatParser fp) return fp;

        // 2. ShortCode, format type name, or primary extension match
        string formatName = descriptor.FormatType.ToString();
        var parser = _parsers.FirstOrDefault(p =>
            p.FormatIdentifier.Contains(descriptor.ShortCode, StringComparison.OrdinalIgnoreCase) ||
            p.FormatIdentifier.Contains(formatName, StringComparison.OrdinalIgnoreCase) ||
            p.FormatIdentifier.Contains(descriptor.PrimaryExtension.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
        if (parser is not null) return parser;

        // 3. Fallback to any supported extension
        foreach (var ext in descriptor.SupportedExtensions)
        {
            string clean = ext.TrimStart('.');
            parser = _parsers.FirstOrDefault(p => p.FormatIdentifier.Contains(clean, StringComparison.OrdinalIgnoreCase));
            if (parser is not null) return parser;
        }

        return null;
    }
}
