using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using Xunit;

namespace NEXUS3D.Conversion.Tests.Batch;

public sealed class BatchJobModelTests
{
    [Fact]
    public void BatchJob_CalculatedProperties_ShouldReflectItemsState()
    {
        var job = new BatchJob("TestJob", "C:/exports", ModelFormatType.Schematic);

        Assert.Equal(0, job.TotalFiles);
        Assert.Equal(100.0, job.PercentComplete);

        var item1 = new BatchJobItem(job.JobId, "model1.obj", "model1.schem", ModelFormatType.Obj, ModelFormatType.Schematic);
        var item2 = new BatchJobItem(job.JobId, "model2.obj", "model2.schem", ModelFormatType.Obj, ModelFormatType.Schematic);
        var item3 = new BatchJobItem(job.JobId, "model3.obj", "model3.schem", ModelFormatType.Obj, ModelFormatType.Schematic);
        var item4 = new BatchJobItem(job.JobId, "model4.obj", "model4.schem", ModelFormatType.Obj, ModelFormatType.Schematic);

        job.Items.AddRange([item1, item2, item3, item4]);

        Assert.Equal(4, job.TotalFiles);
        Assert.Equal(0, job.CompletedFiles);
        Assert.Equal(0.0, job.PercentComplete);

        // Mark item1 succeeded
        item1.MarkSucceeded(1024, 50);
        Assert.Equal(1, job.SucceededFiles);
        Assert.Equal(1, job.CompletedFiles);
        Assert.Equal(25.0, job.PercentComplete);

        // Mark item2 failed
        item2.MarkFailed("PARSE_ERROR", "Syntax error", 30);
        Assert.Equal(1, job.FailedFiles);
        Assert.Equal(2, job.CompletedFiles);
        Assert.Equal(50.0, job.PercentComplete);

        // Mark item3 skipped
        item3.MarkSkipped("File already exists");
        Assert.Equal(1, job.SkippedFiles);
        Assert.Equal(3, job.CompletedFiles);
        Assert.Equal(75.0, job.PercentComplete);

        // Mark item4 succeeded
        item4.MarkSucceeded(2048, 80);
        Assert.Equal(2, job.SucceededFiles);
        Assert.Equal(4, job.CompletedFiles);
        Assert.Equal(100.0, job.PercentComplete);
    }

    [Fact]
    public void BatchJobItem_StateTransitions_ShouldSetTimestampsAndErrors()
    {
        var item = new BatchJobItem(Guid.NewGuid(), "input.obj", "output.schem", ModelFormatType.Obj, ModelFormatType.Schematic, 5000);

        Assert.Equal(BatchItemStatus.Pending, item.Status);
        Assert.Null(item.CompletedAtUtc);
        Assert.Equal(5000, item.SourceSizeBytes);

        item.MarkProcessing();
        Assert.Equal(BatchItemStatus.Processing, item.Status);

        item.MarkFailed("ERR01", "Invalid vertex", 120);
        Assert.Equal(BatchItemStatus.Failed, item.Status);
        Assert.Equal("ERR01", item.ErrorCode);
        Assert.Equal("Invalid vertex", item.ErrorMessage);
        Assert.Equal(120, item.DurationMs);
        Assert.NotNull(item.CompletedAtUtc);

        item.MarkSucceeded(4200, 95);
        Assert.Equal(BatchItemStatus.Succeeded, item.Status);
        Assert.Null(item.ErrorMessage);
        Assert.Null(item.ErrorCode);
        Assert.Equal(4200, item.TargetSizeBytes);
        Assert.Equal(95, item.DurationMs);
    }
}

