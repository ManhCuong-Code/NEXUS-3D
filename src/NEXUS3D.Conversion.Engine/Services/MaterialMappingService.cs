using System.Text.Json;
using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Textures;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Service adapting surface materials, PBR/Phong parameters, and external texture file paths across 3D formats.
/// </summary>
public sealed class MaterialMappingService : IMaterialMappingService
{
    private readonly ILogger<MaterialMappingService> _logger;

    public MaterialMappingService(ILogger<MaterialMappingService> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public Task<OperationResult> MapMaterialsAsync(
        Scene3D scene,
        ConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guard.NotNull(scene);
        Guard.NotNull(request);

        var options = request.Options ?? ConversionOptions.Default;
        var targetFormat = request.TargetFormat;

        string targetDir = Path.GetDirectoryName(Path.GetFullPath(request.TargetFilePath)) ?? Environment.CurrentDirectory;
        string sourceDir = Path.GetDirectoryName(Path.GetFullPath(request.SourceFilePath)) ?? Environment.CurrentDirectory;

        _logger.LogInformation(
            "Mapping materials for {SceneName} ({MaterialCount} materials, {TextureCount} textures) to {TargetFormat}.",
            scene.Name,
            scene.Materials.Count,
            scene.Textures.Count,
            targetFormat);

        // 1. Texture resolution and optional file copying
        if (options.CopyExternalTextures && scene.Textures.Count > 0)
        {
            string textureSubDirName = options.TextureOutputSubdirectory ?? "textures";
            string destTextureFolder = Path.Combine(targetDir, textureSubDirName);

            foreach (var texture in scene.Textures)
            {
                if (texture.Reference.StorageKind != TextureStorageKind.External ||
                    string.IsNullOrWhiteSpace(texture.Reference.RelativePath))
                {
                    continue;
                }

                string relativePath = texture.Reference.RelativePath;
                string sourceImgPath = Path.IsPathRooted(relativePath)
                    ? relativePath
                    : Path.GetFullPath(Path.Combine(sourceDir, relativePath));

                if (File.Exists(sourceImgPath))
                {
                    try
                    {
                        Directory.CreateDirectory(destTextureFolder);
                        string fileName = Path.GetFileName(sourceImgPath);
                        string destImgPath = Path.Combine(destTextureFolder, fileName);

                        if (!string.Equals(sourceImgPath, destImgPath, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(sourceImgPath, destImgPath, overwrite: true);
                        }

                        // Update relative path in texture reference so target file links cleanly
                        texture.Reference.RelativePath = Path.Combine(textureSubDirName, fileName).Replace('\\', '/');
                        texture.Reference.LoadState = TextureLoadState.Loaded;

                        _logger.LogInformation("Copied texture asset '{FileName}' to '{DestFolder}'.", fileName, destTextureFolder);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to copy texture file '{SourceImgPath}'. Keeping original path.", sourceImgPath);
                    }
                }
                else
                {
                    texture.Reference.LoadState = TextureLoadState.Missing;
                    _logger.LogWarning("Referenced texture file was not found on disk: '{SourceImgPath}'.", sourceImgPath);
                }
            }
        }

        // 2. Material shader and parameter bridging
        foreach (var material in scene.Materials.Materials)
        {
            if (targetFormat == ModelFormatType.Obj)
            {
                // Ensure classic MTL Phong parameters are populated from PBR
                BridgeToPhong(material);
            }
            else if (targetFormat is ModelFormatType.Schematic or ModelFormatType.VoxelJson)
            {
                // Ensure base color or block name tags are populated
                BridgeToVoxel(material);
            }
        }

        return Task.FromResult(OperationResult.Success());
    }

    private static void BridgeToPhong(MaterialData material)
    {
        // Diffuse reflectance Kd (RGB)
        material.CustomProperties["mtl.Kd"] = JsonSerializer.SerializeToElement(
            new double[] { material.BaseColor.R, material.BaseColor.G, material.BaseColor.B });

        // Specular reflectance Ks: scaled by metallic
        double spec = Math.Clamp(material.Metallic * 0.9 + 0.05, 0.0, 1.0);
        material.CustomProperties["mtl.Ks"] = JsonSerializer.SerializeToElement(new double[] { spec, spec, spec });

        // Specular exponent Ns: inverted from roughness
        double exponent = Math.Clamp(Math.Pow(1.0 - material.Roughness, 2.0) * 1000.0, 0.0, 1000.0);
        material.CustomProperties["mtl.Ns"] = JsonSerializer.SerializeToElement(exponent);

        // Opacity / Dissolve d
        material.CustomProperties["mtl.d"] = JsonSerializer.SerializeToElement(material.Opacity);
    }

    private static void BridgeToVoxel(MaterialData material)
    {
        // Store hex color representation
        int r = (int)Math.Clamp(material.BaseColor.R * 255.0, 0.0, 255.0);
        int g = (int)Math.Clamp(material.BaseColor.G * 255.0, 0.0, 255.0);
        int b = (int)Math.Clamp(material.BaseColor.B * 255.0, 0.0, 255.0);
        string hex = $"#{r:X2}{g:X2}{b:X2}";

        material.CustomProperties["voxel.hexColor"] = JsonSerializer.SerializeToElement(hex);
    }
}
