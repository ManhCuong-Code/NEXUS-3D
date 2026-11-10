using System.IO.Compression;
using System.Security;
using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.PluginSystem.Packages;
using Xunit;

namespace NEXUS3D.Plugin.Tests;

public sealed class PluginPackageSecurityTests : IDisposable
{
    private readonly string _tempDirectory;

    public PluginPackageSecurityTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "Nexus3D_PkgSecTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task PackAndUnpack_ValidPlugin_Succeeds()
    {
        string sourceDir = Path.Combine(_tempDirectory, "SourcePlugin");
        Directory.CreateDirectory(sourceDir);

        string manifestJson = """
        {
            "id": "com.test.valid",
            "name": "Valid Test Plugin",
            "version": "1.0.0",
            "assembly": "TestPlugin.dll",
            "entryPoint": "TestPlugin.Plugin",
            "pluginApiVersion": "1.0.0"
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "manifest.json"), manifestJson);
        await File.WriteAllTextAsync(Path.Combine(sourceDir, "TestPlugin.dll"), "DUMMY_DLL_BYTES");

        string packageFile = Path.Combine(_tempDirectory, "ValidPlugin.nexusplugin");
        string unpackDir = Path.Combine(_tempDirectory, "UnpackedPlugin");

        // 1. Pack
        var packResult = PluginPackageManager.PackPlugin(sourceDir, packageFile);
        Assert.True(packResult.IsSuccess);
        Assert.True(File.Exists(packageFile));
        Assert.NotEmpty(packResult.Sha256Hash);

        // 2. Unpack
        var unpackResult = PluginPackageManager.UnpackPlugin(packageFile, unpackDir);
        Assert.True(unpackResult.IsSuccess);
        Assert.NotNull(unpackResult.Value);
        Assert.Equal("com.test.valid", unpackResult.Value.Id);
        Assert.True(File.Exists(Path.Combine(unpackDir, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(unpackDir, "TestPlugin.dll")));
    }

    [Fact]
    public void Unpack_ZipSlipPathTraversal_ThrowsSecurityExceptionOrFails()
    {
        string packageFile = Path.Combine(_tempDirectory, "MaliciousZipSlip.nexusplugin");
        string unpackDir = Path.Combine(_tempDirectory, "TargetUnpackDir");
        Directory.CreateDirectory(unpackDir);

        // Create a malicious zip file containing path traversal entry
        using (var archive = ZipFile.Open(packageFile, ZipArchiveMode.Create))
        {
            // Valid manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write("""{"id": "com.evil.slip", "name": "Evil", "version": "1.0.0", "assembly": "evil.dll", "entryPoint": "evil"}""");
            }

            // Malicious entry escaping root
            var evilEntry = archive.CreateEntry("../../escaped_payload.txt");
            using (var writer = new StreamWriter(evilEntry.Open()))
            {
                writer.Write("PWNED_FILE");
            }
        }

        // Attempt unpack - must fail and protect filesystem
        var result = PluginPackageManager.UnpackPlugin(packageFile, unpackDir);

        Assert.False(result.IsSuccess);
        Assert.Contains("Zip Slip", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Verify escaped file was NOT created outside target unpack dir
        string escapedPath = Path.Combine(_tempDirectory, "escaped_payload.txt");
        Assert.False(File.Exists(escapedPath));
    }

    [Fact]
    public void Unpack_MissingManifest_ReturnsFailure()
    {
        string packageFile = Path.Combine(_tempDirectory, "NoManifest.nexusplugin");
        string unpackDir = Path.Combine(_tempDirectory, "TargetUnpackNoManifest");

        using (var archive = ZipFile.Open(packageFile, ZipArchiveMode.Create))
        {
            var dummyEntry = archive.CreateEntry("readme.txt");
            using var writer = new StreamWriter(dummyEntry.Open());
            writer.Write("No manifest here!");
        }

        var result = PluginPackageManager.UnpackPlugin(packageFile, unpackDir);

        Assert.False(result.IsSuccess);
        Assert.Contains("manifest.json", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
