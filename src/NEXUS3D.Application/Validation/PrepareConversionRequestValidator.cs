using NEXUS3D.Application.Models;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Rules;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.Validation;

/// <summary>
/// Validates <see cref="PrepareConversionRequest"/> before creating a workspace job record.
/// </summary>
public static class PrepareConversionRequestValidator
{
    public static OperationResult Validate(PrepareConversionRequest? request)
    {
        if (request is null)
        {
            return OperationResult.Failure("VAL_REQ_NULL", "Conversion request cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(request.SourceFilePath))
        {
            return OperationResult.Failure("VAL_SOURCE_EMPTY", "Please select a source 3D model file.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return OperationResult.Failure("VAL_OUTPUT_DIR_EMPTY", "Please specify an output directory.");
        }

        if (request.SourceFormat == ModelFormatType.Unknown)
        {
            return OperationResult.Failure("VAL_SOURCE_FORMAT", "Unsupported or unrecognized source model format.");
        }

        if (request.TargetFormat == ModelFormatType.Unknown)
        {
            return OperationResult.Failure("VAL_TARGET_FORMAT", "Please select a valid target export format.");
        }

        if (double.IsNaN(request.ScaleFactor) ||
            request.ScaleFactor < ConversionDomainRules.MinScaleFactor ||
            request.ScaleFactor > ConversionDomainRules.MaxScaleFactor)
        {
            return OperationResult.Failure(
                "VAL_SCALE_FACTOR",
                $"Scale factor must be between {ConversionDomainRules.MinScaleFactor} and {ConversionDomainRules.MaxScaleFactor}.");
        }

        return OperationResult.Success();
    }
}

