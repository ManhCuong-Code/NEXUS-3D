using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.BedrockGeometry.Conversion;
using NEXUS3D.Formats.BedrockGeometry.Models;

namespace NEXUS3D.Formats.BedrockGeometry.Exporting;

public interface IBedrockResourcePackExporter
{
    Task<string> ExportResourcePackAsync(
        Scene3D scene,
        string outputFilePathOrDirectory,
        bool createMcPackZip = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Packages Bedrock Geometry models and texture assets into a ready-to-import Minecraft Bedrock Resource Pack
/// (.mcpack archive or directory structure with manifest.json, models/entity, and textures/entity).
/// </summary>
public sealed class BedrockResourcePackExporter : IBedrockResourcePackExporter
{
    private readonly ISceneToBedrockGeometryConverter _geometryConverter;
    private readonly BedrockGeometryExporter _geometryExporter;

    public BedrockResourcePackExporter(
        ISceneToBedrockGeometryConverter? geometryConverter = null,
        BedrockGeometryExporter? geometryExporter = null)
    {
        _geometryConverter = geometryConverter ?? new SceneToBedrockGeometryConverter();
        _geometryExporter = geometryExporter ?? new BedrockGeometryExporter();
    }

    public async Task<string> ExportResourcePackAsync(
        Scene3D scene,
        string outputFilePathOrDirectory,
        bool createMcPackZip = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePathOrDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        string rawName = !string.IsNullOrWhiteSpace(scene.Name) ? scene.Name : "model";
        string sanitizedModelName = SanitizeFileName(rawName);

        // Convert Scene to Bedrock Geometry Document
        var (doc, _) = _geometryConverter.Convert(scene, new BedrockMappingOptions
        {
            TargetFormatVersion = "1.12.0",
            PreserveRawData = true
        });

        // Resolve or create Texture
        byte[]? textureBytes = null;
        string? existingTexturePath = FindExistingTexture(scene);
        if (existingTexturePath is not null && File.Exists(existingTexturePath))
        {
            textureBytes = await File.ReadAllBytesAsync(existingTexturePath, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Build Texture Atlas from Materials/Colors
            var colorMap = new Dictionary<string, System.Numerics.Vector4>(StringComparer.OrdinalIgnoreCase);
            foreach (var mat in scene.Materials.Materials)
            {
                colorMap[mat.Name] = new System.Numerics.Vector4(
                    (float)mat.BaseColor.R,
                    (float)mat.BaseColor.G,
                    (float)mat.BaseColor.B,
                    (float)(mat.BaseColor.A * mat.Opacity));
            }

            var (atlasBytes, _, width, height) = BedrockTextureAtlasGenerator.GenerateAtlas(colorMap, 64);
            textureBytes = atlasBytes;

            // Ensure geometry description matches texture dimensions
            foreach (var geo in doc.Geometries)
            {
                geo.Description.TextureWidth = width;
                geo.Description.TextureHeight = height;
            }
        }

        string tempPackDir = Path.Combine(Path.GetTempPath(), $"nexus3d_pack_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(tempPackDir);

            // 1. Build manifest.json (format_version: 2)
            string manifestJson = CreateManifest(sanitizedModelName, rawName);
            await File.WriteAllTextAsync(Path.Combine(tempPackDir, "manifest.json"), manifestJson, cancellationToken).ConfigureAwait(false);

            // 2. Build models/entity/<sanitizedModelName>.geo.json
            string modelsEntityDir = Path.Combine(tempPackDir, "models", "entity");
            Directory.CreateDirectory(modelsEntityDir);
            string geoPath = Path.Combine(modelsEntityDir, $"{sanitizedModelName}.geo.json");
            _geometryExporter.ExportToFile(doc, geoPath, BedrockGeometryWriteOptions.Default);

            // 3. Build textures/entity/<sanitizedModelName>.png
            string texturesEntityDir = Path.Combine(tempPackDir, "textures", "entity");
            Directory.CreateDirectory(texturesEntityDir);
            string texPath = Path.Combine(texturesEntityDir, $"{sanitizedModelName}.png");
            if (textureBytes is not null)
            {
                await File.WriteAllBytesAsync(texPath, textureBytes, cancellationToken).ConfigureAwait(false);
            }

            // 4. Build textures/item_texture.json definition
            string itemTextureJson = CreateItemTextureDefinition(sanitizedModelName);
            await File.WriteAllTextAsync(Path.Combine(tempPackDir, "textures", "item_texture.json"), itemTextureJson, cancellationToken).ConfigureAwait(false);

            // Determine final output path
            if (createMcPackZip)
            {
                string targetZip = outputFilePathOrDirectory.EndsWith(".mcpack", StringComparison.OrdinalIgnoreCase) ||
                                   outputFilePathOrDirectory.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    ? outputFilePathOrDirectory
                    : Path.Combine(outputFilePathOrDirectory, $"{sanitizedModelName}.mcpack");

                string targetDir = Path.GetDirectoryName(Path.GetFullPath(targetZip)) ?? Environment.CurrentDirectory;
                Directory.CreateDirectory(targetDir);

                if (File.Exists(targetZip))
                {
                    File.Delete(targetZip);
                }

                ZipFile.CreateFromDirectory(tempPackDir, targetZip, CompressionLevel.Optimal, false);
                return targetZip;
            }
            else
            {
                string targetDir = outputFilePathOrDirectory;
                CopyDirectory(tempPackDir, targetDir);
                return targetDir;
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempPackDir))
                {
                    Directory.Delete(tempPackDir, recursive: true);
                }
            }
            catch { }
        }
    }

    private static string CreateManifest(string modelName, string displayName)
    {
        var manifest = new JsonObject
        {
            ["format_version"] = 2,
            ["header"] = new JsonObject
            {
                ["description"] = $"NEXUS-3D Exported Resource Pack - {displayName}",
                ["name"] = displayName,
                ["uuid"] = Guid.NewGuid().ToString(),
                ["version"] = new JsonArray { 1, 0, 0 },
                ["min_engine_version"] = new JsonArray { 1, 20, 0 }
            },
            ["modules"] = new JsonArray
            {
                new JsonObject
                {
                    ["description"] = "NEXUS-3D Geometry & Texture Resources",
                    ["type"] = "resources",
                    ["uuid"] = Guid.NewGuid().ToString(),
                    ["version"] = new JsonArray { 1, 0, 0 }
                }
            }
        };

        return manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string CreateItemTextureDefinition(string modelName)
    {
        var root = new JsonObject
        {
            ["resource_pack_name"] = "nexus3d",
            ["texture_name"] = "atlas.items",
            ["texture_data"] = new JsonObject
            {
                [modelName] = new JsonObject
                {
                    ["textures"] = $"textures/entity/{modelName}"
                }
            }
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string? FindExistingTexture(Scene3D scene)
    {
        foreach (var mat in scene.Materials.Materials)
        {
            if (mat.TextureSlots.TryGetValue("Diffuse", out var slot) &&
                !string.IsNullOrWhiteSpace(slot.Channel) &&
                File.Exists(slot.Channel))
            {
                return slot.Channel;
            }

            if (mat.TextureSlots.TryGetValue("Albedo", out var albedoslot) &&
                !string.IsNullOrWhiteSpace(albedoslot.Channel) &&
                File.Exists(albedoslot.Channel))
            {
                return albedoslot.Channel;
            }
        }

        if (!string.IsNullOrWhiteSpace(scene.Metadata.Source?.OriginalFileName))
        {
            string orig = scene.Metadata.Source.OriginalFileName;
            string? dir = Path.GetDirectoryName(orig);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                string baseName = Path.GetFileNameWithoutExtension(orig);
                string candidate = Path.Combine(dir, $"{baseName}.png");
                if (File.Exists(candidate)) return candidate;

                candidate = Path.Combine(dir, "textures", "entity", $"{baseName}.png");
                if (File.Exists(candidate)) return candidate;

                candidate = Path.Combine(dir, "textures", $"{baseName}.png");
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Where(c => !invalid.Contains(c) && c != ' ').ToArray()).ToLowerInvariant();
        return string.IsNullOrWhiteSpace(cleaned) ? "model" : cleaned;
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            string dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            string destDir = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, destDir);
        }
    }
}
