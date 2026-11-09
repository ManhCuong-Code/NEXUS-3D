using System.Text;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Conversion;
using NEXUS3D.Formats.Obj.Parsing;
using NEXUS3D.Formats.Obj.Services;

namespace NEXUS3D.Obj.Tests;

public sealed class ObjMalformedAndEdgeCaseTests
{
    private static async Task<ObjException> AssertThrowsObjExceptionAsync(string objText, ObjParseOptions? options = null)
    {
        var parser = new ObjParser();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(objText));
        return await Assert.ThrowsAsync<ObjException>(() =>
            parser.ParseDocumentAsync(stream, options ?? ObjParseOptions.Default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n  \n  ")]
    [InlineData("# Only comments\n# No vertices or faces\n")]
    public async Task Parse_EmptyOrCommentOnlyStream_ThrowsObj001(string content)
    {
        ObjException ex = await AssertThrowsObjExceptionAsync(content);
        Assert.Equal(ObjErrorCodes.EmptyOrUnrecognizedFile, ex.ErrorCode);
    }

    [Theory]
    [InlineData("v 1.0 2.0\nf 1 1 1")]
    [InlineData("v 1.0 NaN 3.0\nf 1 1 1")]
    [InlineData("v 1.0 Infinity 3.0\nf 1 1 1")]
    [InlineData("v 1.0 2.0 3.0 0.0\nf 1 1 1")]
    [InlineData("v abc 2.0 3.0\nf 1 1 1")]
    public async Task Parse_InvalidVertexCoordinates_ThrowsObj003(string content)
    {
        ObjException ex = await AssertThrowsObjExceptionAsync(content);
        Assert.Equal(ObjErrorCodes.InvalidVertex, ex.ErrorCode);
    }

    [Theory]
    [InlineData("v 0 0 0\nv 1 0 0\nf 1 2")]
    [InlineData("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1/1/1/1 2 3")]
    [InlineData("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1/abc/1 2 3")]
    public async Task Parse_MalformedFaceOrTooFewVertices_ThrowsObj006(string content)
    {
        ObjException ex = await AssertThrowsObjExceptionAsync(content);
        Assert.Equal(ObjErrorCodes.InvalidFace, ex.ErrorCode);
    }

    [Theory]
    [InlineData("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 0 1 2")]
    [InlineData("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 99")]
    [InlineData("v 0 0 0\nv 1 0 0\nv 0 1 0\nf -1 -2 -99")]
    [InlineData("v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nf 1/5 2/1 3/1")]
    [InlineData("v 0 0 0\nv 1 0 0\nv 0 1 0\nvn 0 0 1\nf 1//5 2//1 3//1")]
    public async Task Parse_OutOfRangePositiveZeroOrNegativeIndices_ThrowsObj007(string content)
    {
        ObjException ex = await AssertThrowsObjExceptionAsync(content);
        Assert.Equal(ObjErrorCodes.IndexOutOfRange, ex.ErrorCode);
    }

    [Fact]
    public async Task Parse_OversizedLine_ThrowsObj009LimitExceeded()
    {
        string longComment = "# " + new string('A', 512);
        ObjException ex = await AssertThrowsObjExceptionAsync(
            longComment,
            new ObjParseOptions { MaxLineLength = 128 });
        Assert.Equal(ObjErrorCodes.LimitExceeded, ex.ErrorCode);
    }

    [Fact]
    public async Task ReadFile_MissingMtlAndMissingMaterial_WarnsInPermissiveModeAndFailsInStrictMode()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"nexus_obj_malformed_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string objPath = Path.Combine(tempDir, "model.obj");
            const string objText = """
                mtllib nonexistent_library.mtl
                v 0 0 0
                v 1 0 0
                v 0 1 0
                usemtl GhostMaterial
                f 1 2 3
                """;
            await File.WriteAllTextAsync(objPath, objText);

            var reader = new ObjFormatParser();

            // 1. Permissive mode: succeeds with fallback material and OBJ011 / OBJ012 warnings
            ObjReadResult permissive = await reader.ReadFileAsync(
                objPath,
                new ObjMappingOptions { StrictMode = false, LoadCompanionMtl = true });

            Assert.True(permissive.IsSuccess);
            Assert.NotNull(permissive.Scene);
            Assert.Contains(permissive.Issues, i => i.Code == ObjErrorCodes.MissingMtlFile && i.Severity == ValidationSeverity.Warning);
            Assert.Contains(permissive.Issues, i => i.Code == ObjErrorCodes.MissingMaterial && i.Severity == ValidationSeverity.Warning);

            // 2. Strict mode: fails with OBJ011
            ObjReadResult strict = await reader.ReadFileAsync(
                objPath,
                new ObjMappingOptions { StrictMode = true, LoadCompanionMtl = true });

            Assert.False(strict.IsSuccess);
            Assert.NotNull(strict.Error);
            Assert.Equal(ObjErrorCodes.MissingMtlFile, strict.Error.Value.Code);
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
