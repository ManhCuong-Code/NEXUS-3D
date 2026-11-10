using System.Text.Json;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Enums;
using Nexus3D.CLI.Formatting;
using Xunit;

namespace NEXUS3D.Conversion.Tests.Batch;

public sealed class JsonReportGeneratorTests
{
    [Fact]
    public void GenerateJson_ValidBatchJobResult_ShouldProduceValidStructuredJson()
    {
        var jobId = Guid.NewGuid();
        var item1 = new BatchJobItem(jobId, "input1.obj", "output1.schem", ModelFormatType.Obj, ModelFormatType.Schematic, 1200);
        item1.MarkSucceeded(3400, 45);

        var item2 = new BatchJobItem(jobId, "input2.obj", "output2.schem", ModelFormatType.Obj, ModelFormatType.Schematic, 1500);
        item2.MarkFailed("PARSE_ERROR", "Unexpected EOF", 20);

        var result = new BatchJobResult(
            JobId: jobId,
            JobName: "TestReportJob",
            Status: BatchJobStatus.CompletedWithErrors,
            TotalFiles: 2,
            SucceededFiles: 1,
            FailedFiles: 1,
            SkippedFiles: 0,
            DurationMs: 65,
            Items: [item1, item2],
            ErrorMessage: "One or more files failed.");

        string json = JsonReportGenerator.GenerateJson(result);

        Assert.NotNull(json);
        Assert.NotEmpty(json);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(jobId.ToString(), root.GetProperty("JobId").GetString());
        Assert.Equal("TestReportJob", root.GetProperty("JobName").GetString());
        Assert.Equal("CompletedWithErrors", root.GetProperty("Status").GetString());
        Assert.Equal(2, root.GetProperty("TotalFiles").GetInt32());
        Assert.Equal(1, root.GetProperty("SucceededFiles").GetInt32());
        Assert.Equal(1, root.GetProperty("FailedFiles").GetInt32());

        var items = root.GetProperty("Items");
        Assert.Equal(2, items.GetArrayLength());

        var first = items[0];
        Assert.Equal("Succeeded", first.GetProperty("Status").GetString());
        Assert.Equal(3400, first.GetProperty("TargetSizeBytes").GetInt64());

        var second = items[1];
        Assert.Equal("Failed", second.GetProperty("Status").GetString());
        Assert.Equal("PARSE_ERROR", second.GetProperty("ErrorCode").GetString());
    }
}

