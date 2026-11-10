using NEXUS3D.Domain.Enums;
using Nexus3D.CLI;
using Nexus3D.CLI.Arguments;
using Xunit;

namespace NEXUS3D.Conversion.Tests.Batch;

public sealed class CliArgumentParserTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void Parse_HelpCommand_ShouldReturnHelp(string arg)
    {
        var result = CliArgumentParser.Parse([arg]);
        Assert.Equal(CliCommandType.Help, result.Command);
        Assert.Null(result.ParseError);
    }

    [Fact]
    public void Parse_EmptyArgs_ShouldReturnHelp()
    {
        var result = CliArgumentParser.Parse([]);
        Assert.Equal(CliCommandType.Help, result.Command);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    [InlineData("version")]
    public void Parse_VersionCommand_ShouldReturnVersion(string arg)
    {
        var result = CliArgumentParser.Parse([arg]);
        Assert.Equal(CliCommandType.Version, result.Command);
        Assert.Null(result.ParseError);
    }

    [Fact]
    public void Parse_ConvertCommand_ValidOptions_ShouldPopulateFields()
    {
        string[] args = ["convert", "-i", "cube.obj", "-o", "cube.schem", "--scale", "2.5", "--voxelize", "--overwrite"];
        var result = CliArgumentParser.Parse(args);

        Assert.Equal(CliCommandType.Convert, result.Command);
        Assert.Null(result.ParseError);
        Assert.Equal("cube.obj", result.InputPath);
        Assert.Equal("cube.schem", result.OutputPath);
        Assert.Equal(2.5, result.ScaleFactor);
        Assert.True(result.Voxelize);
        Assert.True(result.Overwrite);
    }

    [Fact]
    public void Parse_ConvertCommand_MissingInput_ShouldSetParseError()
    {
        string[] args = ["convert", "-o", "cube.schem"];
        var result = CliArgumentParser.Parse(args);

        Assert.NotNull(result.ParseError);
        Assert.Contains("--input", result.ParseError);
    }

    [Fact]
    public void Parse_BatchCommand_ValidOptions_ShouldPopulateFields()
    {
        string[] args =
        [
            "batch",
            "-i", "./models",
            "-o", "./exports",
            "-f", "schem",
            "-p", "8",
            "--priority", "high",
            "--extensions", ".obj,.json",
            "--retry", "3",
            "--json-report", "report.json",
            "--overwrite",
            "--stop-on-error"
        ];

        var result = CliArgumentParser.Parse(args);

        Assert.Equal(CliCommandType.Batch, result.Command);
        Assert.Null(result.ParseError);
        Assert.Equal("./models", result.InputPath);
        Assert.Equal("./exports", result.OutputPath);
        Assert.Equal(ModelFormatType.Schematic, result.TargetFormat);
        Assert.Equal(8, result.Parallelism);
        Assert.Equal(BatchJobPriority.High, result.Priority);
        Assert.Equal(2, result.Extensions.Count);
        Assert.Contains(".obj", result.Extensions);
        Assert.Contains(".json", result.Extensions);
        Assert.Equal(3, result.RetryLimit);
        Assert.Equal("report.json", result.JsonReportPath);
        Assert.True(result.Overwrite);
        Assert.False(result.ContinueOnError);
    }

    [Fact]
    public void Parse_BatchCommand_MissingFormat_ShouldSetParseError()
    {
        string[] args = ["batch", "-i", "./models", "-o", "./exports"];
        var result = CliArgumentParser.Parse(args);

        Assert.NotNull(result.ParseError);
        Assert.Contains("--format", result.ParseError);
    }

    [Fact]
    public void Parse_JobsList_ShouldReturnJobsList()
    {
        var result = CliArgumentParser.Parse(["jobs", "list", "--limit", "50"]);
        Assert.Equal(CliCommandType.JobsList, result.Command);
        Assert.Equal(50, result.Limit);
        Assert.Null(result.ParseError);
    }

    [Fact]
    public void Parse_JobsStatus_ValidGuid_ShouldPopulateJobId()
    {
        var guid = Guid.NewGuid();
        var result = CliArgumentParser.Parse(["jobs", "status", guid.ToString()]);
        Assert.Equal(CliCommandType.JobsStatus, result.Command);
        Assert.Equal(guid, result.JobId);
        Assert.Null(result.ParseError);
    }

    [Fact]
    public void Parse_JobsCancel_ValidGuid_ShouldPopulateJobId()
    {
        var guid = Guid.NewGuid();
        var result = CliArgumentParser.Parse(["jobs", "cancel", guid.ToString()]);
        Assert.Equal(CliCommandType.JobsCancel, result.Command);
        Assert.Equal(guid, result.JobId);
        Assert.Null(result.ParseError);
    }

    [Fact]
    public void ExitCodes_Constants_ShouldMatchSpecification()
    {
        Assert.Equal(0, CliExitCodes.Success);
        Assert.Equal(1, CliExitCodes.UnknownError);
        Assert.Equal(2, CliExitCodes.InvalidArguments);
        Assert.Equal(3, CliExitCodes.InvalidInputFile);
        Assert.Equal(4, CliExitCodes.ConversionFailure);
        Assert.Equal(5, CliExitCodes.PartialBatchFailure);
        Assert.Equal(6, CliExitCodes.CancelledByUser);
    }
}

