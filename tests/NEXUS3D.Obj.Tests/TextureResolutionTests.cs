using NEXUS3D.Domain.Textures;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Materials;
using NEXUS3D.Formats.Obj.Textures;

namespace NEXUS3D.Obj.Tests;

public sealed class TextureResolutionTests
{
    [Fact]
    public void ResolveReference_ExistingPngAndMissingTexture_ExtractsHeaderDimensionsAndEmitsWarning()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"nexus_obj_tex_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "sub folder"));

        try
        {
            string pngPath = Path.Combine(tempDir, "sub folder", "albedo map.png");
            byte[] png16x16 = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAFklEQVR42mNk+M9QzwAEjDAGNzYAAIoaB/12u9zKAAAAAElFTkSuQmCC");
            File.WriteAllBytes(pngPath, png16x16);

            var resolver = new TexturePathResolver();

            // Resolve with Windows backslash separator and space in path
            ObjTextureReference resolved = resolver.ResolveReference(
                "Brick",
                "Albedo",
                "map_Kd",
                @"sub folder\albedo map.png",
                tempDir);

            Assert.True(resolved.ExistsOnDisk);
            Assert.Equal("sub folder/albedo map.png", resolved.NormalizedRelativePath);
            Assert.Equal(TextureFormat.Png, resolved.Format);
            Assert.Equal(TextureColorSpace.Srgb, resolved.ColorSpace);
            Assert.Equal(16, resolved.Width);
            Assert.Equal(16, resolved.Height);

            TextureData usmTex = resolved.ToUsmTextureData();
            Assert.Equal(TextureLoadState.Unloaded, usmTex.Reference.LoadState);
            Assert.Equal(16, usmTex.Width);
            Assert.Equal(16, usmTex.Height);

            // Validate missing texture in non-strict vs strict mode
            var mtlDoc = new MtlDocument { SourcePath = Path.Combine(tempDir, "scene.mtl") };
            var mat = new MtlMaterial("TestMat")
            {
                MapKd = new MtlTextureMapStatement("map_Kd", "sub folder/albedo map.png"),
                Norm = new MtlTextureMapStatement("norm", "sub folder/missing_normal.png")
            };
            mtlDoc.Materials.Add(mat);

            var validator = new TextureValidationService(resolver);
            var (refs, issues) = validator.ResolveAndValidate(mtlDoc, tempDir, strictMode: false);

            Assert.Equal(2, refs.Count);
            Assert.Single(issues);
            Assert.Equal(ObjErrorCodes.MissingTextureFile, issues[0].Code);
            Assert.Equal(ValidationSeverity.Warning, issues[0].Severity);

            Assert.Throws<ObjException>(() => validator.ResolveAndValidate(mtlDoc, tempDir, strictMode: true));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CopyTextureForExportAsync_PreservesStructureOrDisambiguatesCollisionsSafely()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"nexus_obj_copy_{Guid.NewGuid():N}");
        string srcDirA = Path.Combine(tempDir, "srcA");
        string srcDirB = Path.Combine(tempDir, "srcB");
        string exportDir = Path.Combine(tempDir, "export");
        Directory.CreateDirectory(srcDirA);
        Directory.CreateDirectory(srcDirB);
        Directory.CreateDirectory(exportDir);

        try
        {
            string fileA = Path.Combine(srcDirA, "tile.png");
            string fileB = Path.Combine(srcDirB, "tile.png");
            await File.WriteAllBytesAsync(fileA, [1, 2, 3, 4]);
            await File.WriteAllBytesAsync(fileB, [5, 6, 7, 8]);

            var resolver = new TexturePathResolver();
            var allocated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string rel1 = await resolver.CopyTextureForExportAsync(
                fileA,
                "textures/tile.png",
                exportDir,
                preserveDirectoryStructure: false,
                allowOverwrite: true,
                allocatedRelativePaths: allocated);

            string rel2 = await resolver.CopyTextureForExportAsync(
                fileB,
                "other/tile.png",
                exportDir,
                preserveDirectoryStructure: false,
                allowOverwrite: true,
                allocatedRelativePaths: allocated);

            Assert.Equal("tile.png", rel1);
            Assert.Equal("tile_2.png", rel2);
            Assert.True(File.Exists(Path.Combine(exportDir, "tile.png")));
            Assert.True(File.Exists(Path.Combine(exportDir, "tile_2.png")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

