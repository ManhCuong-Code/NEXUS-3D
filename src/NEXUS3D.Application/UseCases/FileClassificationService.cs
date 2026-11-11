using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Application.UseCases;

/// <summary>
/// Smart file classifier inspecting extension, magic bytes, JSON structure, and schema
/// to accurately route models to their specialized 3D workspace.
/// </summary>
public sealed class FileClassificationService : IFileClassificationService
{
    private readonly IFormatRegistry _formatRegistry;
    private readonly ILogger<FileClassificationService> _logger;

    public FileClassificationService(
        IFormatRegistry formatRegistry,
        ILogger<FileClassificationService> logger)
    {
        _formatRegistry = Guard.NotNull(formatRegistry);
        _logger = Guard.NotNull(logger);
    }

    public async Task<FileClassificationReport> ClassifyFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ClassifyFile(filePath), cancellationToken);
    }

    public FileClassificationReport ClassifyFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new FileClassificationReport
            {
                FilePath = filePath ?? string.Empty,
                FileName = Path.GetFileName(filePath) ?? "Unknown",
                FileSizeBytes = 0,
                FormattedFileSize = "0 KB",
                FormatType = ModelFormatType.Unknown,
                Category = FormatCategory.Unknown,
                FormatDisplayName = "File Not Found",
                ShortCode = "N/A",
                Confidence = ConfidenceLevel.Unsupported,
                Details = "Specified file does not exist on disk."
            };
        }

        var fileInfo = new FileInfo(filePath);
        long size = fileInfo.Length;
        string formattedSize = FormatBytes(size);
        string fileName = fileInfo.Name;
        string ext = fileInfo.Extension.ToLowerInvariant();

        // 1. Check compound extension for Bedrock (.geo.json)
        if (fileName.EndsWith(".geo.json", StringComparison.OrdinalIgnoreCase))
        {
            return BuildBedrockReport(filePath, fileName, size, formattedSize, ConfidenceLevel.Definite, "Bedrock compound extension (.geo.json)");
        }

        // 2. Check compound extension for Voxel (.voxel.json)
        if (fileName.EndsWith(".voxel.json", StringComparison.OrdinalIgnoreCase))
        {
            return BuildVoxelReport(filePath, fileName, size, formattedSize, ConfidenceLevel.Definite, "Voxel JSON compound extension (.voxel.json)");
        }

        // 3. Schematic (.schem, .schematic)
        if (ext is ".schem" or ".schematic")
        {
            return InspectSchematicFile(filePath, fileName, size, formattedSize);
        }

        // 4. Wavefront OBJ (.obj)
        if (ext == ".obj")
        {
            return InspectObjFile(filePath, fileName, size, formattedSize);
        }

        // 5. Generic JSON (.json)
        if (ext == ".json")
        {
            return InspectJsonFile(filePath, fileName, size, formattedSize);
        }

        // 6. Check generic format registry for extensions (Plugins or third-party formats)
        var descriptor = _formatRegistry.ResolveByFilePath(filePath);
        if (descriptor is not null)
        {
            var cat = GetCategoryForFormat(descriptor.FormatType);
            return new FileClassificationReport
            {
                FilePath = filePath,
                FileName = fileName,
                FileSizeBytes = size,
                FormattedFileSize = formattedSize,
                FormatType = descriptor.FormatType,
                Category = cat,
                FormatDisplayName = descriptor.DisplayName,
                ShortCode = descriptor.ShortCode,
                Confidence = ConfidenceLevel.Probable,
                Details = $"Matched registered descriptor for '{descriptor.DisplayName}'",
                ParserName = $"{descriptor.DisplayName} Parser",
                Capabilities = GetCapabilitiesForCategory(cat),
                Descriptor = descriptor,
                ReadableComponents = ["Registered Format Stream", "Metadata Entities"]
            };
        }

        return new FileClassificationReport
        {
            FilePath = filePath,
            FileName = fileName,
            FileSizeBytes = size,
            FormattedFileSize = formattedSize,
            FormatType = ModelFormatType.Unknown,
            Category = FormatCategory.Unknown,
            FormatDisplayName = "Unrecognized Format",
            ShortCode = "UNKNOWN",
            Confidence = ConfidenceLevel.Unsupported,
            Details = $"No known parser or detector matches extension '{ext}'.",
            PotentialWarnings = ["File format is not natively supported by NEXUS-3D."]
        };
    }

    public FormatCategory GetCategoryForFormat(ModelFormatType formatType) => formatType switch
    {
        ModelFormatType.Obj => FormatCategory.PolygonMesh,
        ModelFormatType.Schematic => FormatCategory.MinecraftSchematic,
        ModelFormatType.VoxelJson => FormatCategory.VoxelModel,
        ModelFormatType.BedrockGeometry => FormatCategory.BedrockGeometry,
        ModelFormatType.Custom => FormatCategory.PluginExtension,
        _ => FormatCategory.Unknown
    };

    public IReadOnlyList<IFormatDescriptor> GetFormatsByCategory(FormatCategory category)
    {
        var all = _formatRegistry.GetAllFormats();
        return all.Where(f => GetCategoryForFormat(f.FormatType) == category).ToList();
    }

    public IReadOnlyList<FormatCategory> GetAllCategories() =>
    [
        FormatCategory.PolygonMesh,
        FormatCategory.MinecraftSchematic,
        FormatCategory.VoxelModel,
        FormatCategory.BedrockGeometry,
        FormatCategory.PluginExtension
    ];

    private FileClassificationReport InspectSchematicFile(string filePath, string fileName, long size, string formattedSize)
    {
        var desc = _formatRegistry.GetByFormatType(ModelFormatType.Schematic);
        var report = new FileClassificationReport
        {
            FilePath = filePath,
            FileName = fileName,
            FileSizeBytes = size,
            FormattedFileSize = formattedSize,
            FormatType = ModelFormatType.Schematic,
            Category = FormatCategory.MinecraftSchematic,
            FormatDisplayName = "Minecraft Schematic",
            ShortCode = "SCHEM",
            Confidence = ConfidenceLevel.Definite,
            ParserName = "Sponge Schematic NBT Parser",
            Capabilities = FormatCapabilities.SchematicDefaults,
            Descriptor = desc
        };

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] header = new byte[4];
            int read = fs.Read(header, 0, 4);

            if (read >= 2 && header[0] == 0x1F && header[1] == 0x8B)
            {
                report.Details = "GZip-compressed NBT structure confirmed.";
                report.ReadableComponents.Add("GZip Compression Layer");
                report.ReadableComponents.Add("Block Palette & 3D Voxel Coordinate Array");
                report.ReadableComponents.Add("Tile / Block Entities & Preserved NBT Data");
                report.VersionString = fileName.EndsWith(".schem", StringComparison.OrdinalIgnoreCase) ? "Sponge V2/V3" : "Classic Schematic";
            }
            else if (read >= 1 && header[0] == 0x0A)
            {
                report.Details = "Uncompressed NBT Compound structure confirmed.";
                report.ReadableComponents.Add("Raw NBT Compound Structure");
                report.ReadableComponents.Add("Block Palette & Voxel Data");
                report.VersionString = "Uncompressed NBT";
            }
            else
            {
                report.Confidence = ConfidenceLevel.Probable;
                report.Details = "Extension indicates schematic, but standard NBT magic bytes not found at header (may use custom compression).";
                report.PotentialWarnings.Add("Non-standard NBT header encountered.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read header of schematic {Path}", filePath);
        }

        return report;
    }

    private FileClassificationReport InspectObjFile(string filePath, string fileName, long size, string formattedSize)
    {
        var desc = _formatRegistry.GetByFormatType(ModelFormatType.Obj);
        var report = new FileClassificationReport
        {
            FilePath = filePath,
            FileName = fileName,
            FileSizeBytes = size,
            FormattedFileSize = formattedSize,
            FormatType = ModelFormatType.Obj,
            Category = FormatCategory.PolygonMesh,
            FormatDisplayName = "Wavefront Polygon Mesh",
            ShortCode = "OBJ",
            Confidence = ConfidenceLevel.Definite,
            ParserName = "Wavefront OBJ Parser",
            Capabilities = FormatCapabilities.MeshDefaults,
            Descriptor = desc,
            VersionString = "Wavefront 3.0"
        };

        try
        {
            string? mtlLib = null;
            bool hasVertexColors = false;
            bool hasNormals = false;
            bool hasUVs = false;

            using (var reader = new StreamReader(filePath, Encoding.UTF8))
            {
                int lineCount = 0;
                string? line;
                while ((line = reader.ReadLine()) is not null && lineCount < 200)
                {
                    lineCount++;
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
                    {
                        mtlLib = trimmed[7..].Trim();
                    }
                    else if (trimmed.StartsWith("vn ", StringComparison.OrdinalIgnoreCase))
                    {
                        hasNormals = true;
                    }
                    else if (trimmed.StartsWith("vt ", StringComparison.OrdinalIgnoreCase))
                    {
                        hasUVs = true;
                    }
                    else if (trimmed.StartsWith("v ", StringComparison.OrdinalIgnoreCase))
                    {
                        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length >= 7) // v x y z r g b
                        {
                            hasVertexColors = true;
                        }
                    }
                }
            }

            report.ReadableComponents.Add("3D Vertex Coordinates & Polygonal Faces");
            if (hasNormals) report.ReadableComponents.Add("Surface Normals (vn)");
            if (hasUVs) report.ReadableComponents.Add("Texture UV Coordinates (vt)");
            if (hasVertexColors) report.ReadableComponents.Add("Per-Vertex Color Data (v x y z r g b)");

            if (!string.IsNullOrEmpty(mtlLib))
            {
                string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
                string mtlPath = Path.Combine(dir, mtlLib);
                if (File.Exists(mtlPath))
                {
                    report.ReadableComponents.Add($"Material Library: {mtlLib} (Found)");
                }
                else
                {
                    report.PotentialWarnings.Add($"Referenced material library '{mtlLib}' not found in model directory.");
                }
            }
            else
            {
                report.PotentialWarnings.Add("No companion .mtl material library referenced. Model will use default shader.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed reading OBJ file {Path}", filePath);
        }

        return report;
    }

    private FileClassificationReport InspectJsonFile(string filePath, string fileName, long size, string formattedSize)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs, Encoding.UTF8, true, 4096);
            char[] buf = new char[4096];
            int read = sr.Read(buf, 0, buf.Length);
            string snippet = new string(buf, 0, read);

            if (snippet.Contains("minecraft:geometry", StringComparison.OrdinalIgnoreCase) ||
                snippet.Contains("geometry.", StringComparison.OrdinalIgnoreCase) ||
                (snippet.Contains("format_version", StringComparison.OrdinalIgnoreCase) && snippet.Contains("bones", StringComparison.OrdinalIgnoreCase)))
            {
                return BuildBedrockReport(filePath, fileName, size, formattedSize, ConfidenceLevel.Definite, "Bedrock Geometry JSON schema detected in header");
            }

            if (snippet.Contains("elements", StringComparison.OrdinalIgnoreCase) ||
                snippet.Contains("voxels", StringComparison.OrdinalIgnoreCase) ||
                snippet.Contains("palette", StringComparison.OrdinalIgnoreCase) ||
                snippet.Contains("voxel_size", StringComparison.OrdinalIgnoreCase))
            {
                return BuildVoxelReport(filePath, fileName, size, formattedSize, ConfidenceLevel.Definite, "Voxel JSON schema detected in header");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed inspecting JSON {Path}", filePath);
        }

        // Ambiguous JSON
        return new FileClassificationReport
        {
            FilePath = filePath,
            FileName = fileName,
            FileSizeBytes = size,
            FormattedFileSize = formattedSize,
            FormatType = ModelFormatType.VoxelJson,
            Category = FormatCategory.VoxelModel,
            FormatDisplayName = "Ambiguous JSON (Voxel/Bedrock Candidate)",
            ShortCode = "JSON",
            Confidence = ConfidenceLevel.Ambiguous,
            Details = "JSON content structure is ambiguous. User manual selection recommended.",
            ParserName = "Voxel JSON Parser (Fallback)",
            Capabilities = FormatCapabilities.VoxelDefaults,
            PotentialWarnings = ["JSON content did not match unambiguous Bedrock Geometry or Voxel signatures."]
        };
    }

    private FileClassificationReport BuildBedrockReport(string filePath, string fileName, long size, string formattedSize, ConfidenceLevel confidence, string details)
    {
        var desc = _formatRegistry.GetByFormatType(ModelFormatType.BedrockGeometry);
        return new FileClassificationReport
        {
            FilePath = filePath,
            FileName = fileName,
            FileSizeBytes = size,
            FormattedFileSize = formattedSize,
            FormatType = ModelFormatType.BedrockGeometry,
            Category = FormatCategory.BedrockGeometry,
            FormatDisplayName = "Minecraft Bedrock Geometry",
            ShortCode = "GEO",
            Confidence = confidence,
            Details = details,
            ParserName = "Bedrock Geometry 1.12.0/1.16.0 Parser",
            Capabilities = FormatCapabilities.BedrockDefaults,
            Descriptor = desc,
            VersionString = "Bedrock 1.16.0",
            ReadableComponents =
            [
                "Bone Hierarchy & Kinematic Tree",
                "Cuboid Elements (Origin, Size, Rotation)",
                "Pivot Points & Locators",
                "Box & Per-Face Texture UV Mapping"
            ]
        };
    }

    private FileClassificationReport BuildVoxelReport(string filePath, string fileName, long size, string formattedSize, ConfidenceLevel confidence, string details)
    {
        var desc = _formatRegistry.GetByFormatType(ModelFormatType.VoxelJson);
        return new FileClassificationReport
        {
            FilePath = filePath,
            FileName = fileName,
            FileSizeBytes = size,
            FormattedFileSize = formattedSize,
            FormatType = ModelFormatType.VoxelJson,
            Category = FormatCategory.VoxelModel,
            FormatDisplayName = "Universal Voxel JSON",
            ShortCode = "VOXEL",
            Confidence = confidence,
            Details = details,
            ParserName = "Universal Voxel JSON Parser",
            Capabilities = FormatCapabilities.VoxelDefaults,
            Descriptor = desc,
            VersionString = "1.0",
            ReadableComponents =
            [
                "Voxel Coordinate Grid & Chunks",
                "Color / Material Palette Swatches",
                "Bounding Dimensions"
            ]
        };
    }

    private static FormatCapabilities GetCapabilitiesForCategory(FormatCategory category) => category switch
    {
        FormatCategory.PolygonMesh => FormatCapabilities.MeshDefaults,
        FormatCategory.MinecraftSchematic => FormatCapabilities.SchematicDefaults,
        FormatCategory.VoxelModel => FormatCapabilities.VoxelDefaults,
        FormatCategory.BedrockGeometry => FormatCapabilities.BedrockDefaults,
        _ => FormatCapabilities.GenericDefaults
    };

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
