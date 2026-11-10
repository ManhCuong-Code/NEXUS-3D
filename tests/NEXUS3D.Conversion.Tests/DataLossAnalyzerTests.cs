using Microsoft.Extensions.Logging.Abstractions;
using NEXUS3D.Conversion.Abstractions;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Conversion.Engine.Services;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using Xunit;

namespace NEXUS3D.Conversion.Tests;

public sealed class DataLossAnalyzerTests
{
    private readonly DataLossAnalyzer _analyzer;
    private readonly FormatCapabilityRegistry _capabilityRegistry;

    public DataLossAnalyzerTests()
    {
        _capabilityRegistry = new FormatCapabilityRegistry(NullLogger<FormatCapabilityRegistry>.Instance);
        _analyzer = new DataLossAnalyzer(_capabilityRegistry, NullLogger<DataLossAnalyzer>.Instance);
    }

    [Fact]
    public void AnalyzeLoss_BedrockWithBones_ToObj_DetectsUnmappedBones()
    {
        var scene = new Scene3D { Name = "RiggedCharacter" };
        var skeleton = new SkeletonData("Rig");
        skeleton.Bones.Add(new BoneData("Root"));
        scene.Skeletons.Add(skeleton);

        var report = _analyzer.AnalyzeLoss(scene, ModelFormatType.Obj);

        Assert.True(report.HasLoss);
        Assert.Contains(report.Items, item => item.Category == "Rigging" && item.Severity == LossSeverity.Unmapped);
    }

    [Fact]
    public void AnalyzeLoss_IdenticalFormat_ReportsSupported()
    {
        var report = _capabilityRegistry.EvaluateLoss(ModelFormatType.Obj, ModelFormatType.Obj);

        Assert.False(report.HasLoss);
        Assert.Equal(LossSeverity.Supported, report.OverallSeverity);
    }
}
