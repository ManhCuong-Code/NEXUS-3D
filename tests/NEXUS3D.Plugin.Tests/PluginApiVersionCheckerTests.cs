using NEXUS3D.Plugin.SDK.Manifest;
using NEXUS3D.PluginSystem.Versioning;
using Xunit;

namespace NEXUS3D.Plugin.Tests;

public sealed class PluginApiVersionCheckerTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("2.0.0", "1.0.0", false)] // plugin requires API v2, host is v1
    [InlineData("0.9.0", "1.0.0", false)] // host major is 1, plugin requires major 0
    [InlineData("1.0.0", "2.0.0", false)] // plugin requires minimum host v2, host is v1
    public void IsCompatible_Evaluation_MatchesExpected(string pluginApiVersion, string minHostVersion, bool expected)
    {
        bool isCompatible = PluginApiVersionChecker.IsCompatible(pluginApiVersion, minHostVersion, out string? failureReason);

        Assert.Equal(expected, isCompatible);
        if (!expected)
        {
            Assert.NotNull(failureReason);
        }
    }

    [Fact]
    public void IsCompatible_InvalidVersionString_ReturnsFailure()
    {
        bool isCompatible = PluginApiVersionChecker.IsCompatible("invalid-api-version", "1.0.0", out string? failureReason);

        Assert.False(isCompatible);
        Assert.NotNull(failureReason);
        Assert.Contains("invalid", failureReason, StringComparison.OrdinalIgnoreCase);
    }
}
