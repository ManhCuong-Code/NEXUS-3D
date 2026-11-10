using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.Plugin.SDK.Validation;
using Xunit;

namespace NEXUS3D.Plugin.Tests;

public sealed class PluginManifestValidationTests
{
    [Fact]
    public void Validate_ValidManifest_Succeeds()
    {
        var manifest = new PluginManifest
        {
            Id = "com.nexus3d.formats.rawvox",
            Name = "RawVox Format Support",
            Version = "1.0.0",
            Author = "Nexus Core Team",
            Description = "Supports RawVox format.",
            Assembly = "NEXUS3D.Sample.Plugin.dll",
            EntryPoint = "NEXUS3D.Sample.Plugin.RawVoxPlugin",
            PluginApiVersion = "1.0.0",
            MinimumHostVersion = "1.0.0",
            SupportedFormats = ["RAWVOX"]
        };

        var result = PluginManifestValidator.Validate(manifest);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid id with spaces")]
    [InlineData("com/slash/id")]
    public void Validate_InvalidId_ReturnsFailure(string invalidId)
    {
        var manifest = new PluginManifest
        {
            Id = invalidId,
            Name = "Test Plugin",
            Version = "1.0.0",
            Assembly = "plugin.dll",
            EntryPoint = "plugin.Main"
        };

        var result = PluginManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, e => e.PropertyPath == "Id");
    }

    [Theory]
    [InlineData("invalid-version")]
    [InlineData("v1.0.0")]
    public void Validate_InvalidSemVer_ReturnsFailure(string invalidVersion)
    {
        var manifest = new PluginManifest
        {
            Id = "com.vendor.plugin",
            Name = "Test Plugin",
            Version = invalidVersion,
            Assembly = "plugin.dll",
            EntryPoint = "plugin.Main"
        };

        var result = PluginManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, e => e.PropertyPath == "Version");
    }

    [Theory]
    [InlineData("../escape.dll")]
    [InlineData("sub/../../escape.dll")]
    [InlineData("/root/escape.dll")]
    [InlineData("C:\\windows\\system32\\calc.dll")]
    public void Validate_AssemblyPathTraversal_ReturnsFailure(string dangerousPath)
    {
        var manifest = new PluginManifest
        {
            Id = "com.vendor.plugin",
            Name = "Malicious Plugin",
            Version = "1.0.0",
            Assembly = dangerousPath,
            EntryPoint = "Malicious.EntryPoint"
        };

        var result = PluginManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, e => e.PropertyPath == "Assembly");
    }

    [Fact]
    public void Validate_MissingRequiredFields_ReportsAllErrors()
    {
        var manifest = new PluginManifest
        {
            Id = "",
            Name = "",
            Version = "",
            Assembly = "",
            EntryPoint = ""
        };

        var result = PluginManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.True(result.Issues.Count >= 4);
    }
}
