using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Engine.Services;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Services;

namespace NEXUS3D.Engine.Tests;

public sealed class EngineAndViewportFoundationTests
{
    [Fact]
    public async Task FoundationConversionPipeline_ShouldReportReadyAndValidateProfiles()
    {
        var pipeline = new FoundationConversionPipeline();
        var status = pipeline.GetStatusSnapshot();

        Assert.True(status.IsReady);
        Assert.Equal(4, status.SupportedPipelines.Count);

        var validProfile = new ConversionProfile(
            "Test Profile",
            "Description",
            ModelFormatType.Schematic,
            ModelFormatType.Obj,
            scaleFactor: 1.0);

        var validation = await pipeline.ValidateCompatibilityAsync(validProfile);
        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void DefaultViewportController_ShouldUpdateShadingAndGridState()
    {
        var controller = new DefaultViewportController();

        Assert.Equal(ViewportShadingMode.Textured, controller.CurrentState.ShadingMode);
        Assert.True(controller.CurrentState.ShowGrid);

        controller.SetShadingMode(ViewportShadingMode.Wireframe);
        controller.ToggleGrid(false);

        Assert.Equal(ViewportShadingMode.Wireframe, controller.CurrentState.ShadingMode);
        Assert.False(controller.CurrentState.ShowGrid);

        controller.ResetCamera();
        Assert.Equal(ViewportShadingMode.Wireframe, controller.CurrentState.ShadingMode);
    }
}

