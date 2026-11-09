using Microsoft.Extensions.Logging;
using NEXUS3D.Application.Interfaces;
using NEXUS3D.Application.Models;
using NEXUS3D.Application.Validation;
using NEXUS3D.Domain.Entities;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Repositories;
using NEXUS3D.Domain.Rules;
using NEXUS3D.Domain.ValueObjects;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Shared.Guards;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Application.UseCases;

/// <summary>
/// Implements Stage 01 conversion preparation: inspects model file metadata, validates conversion profiles,
/// and records prepared conversion jobs in SQLite without running Stage 02+ 3D mesh/voxel conversion algorithms.
/// </summary>
public sealed class ConverterPreparationService : IConverterPreparationService
{
    private readonly IConversionProfileRepository _profileRepository;
    private readonly IConversionHistoryRepository _historyRepository;
    private readonly IRecentFileRepository _recentFileRepository;
    private readonly IFormatRegistry _formatRegistry;
    private readonly ILogger<ConverterPreparationService> _logger;

    public ConverterPreparationService(
        IConversionProfileRepository profileRepository,
        IConversionHistoryRepository historyRepository,
        IRecentFileRepository recentFileRepository,
        IFormatRegistry formatRegistry,
        ILogger<ConverterPreparationService> logger)
    {
        _profileRepository = Guard.NotNull(profileRepository);
        _historyRepository = Guard.NotNull(historyRepository);
        _recentFileRepository = Guard.NotNull(recentFileRepository);
        _formatRegistry = Guard.NotNull(formatRegistry);
        _logger = Guard.NotNull(logger);
    }

    public async Task<OperationResult<IReadOnlyList<ConversionProfile>>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var profiles = await _profileRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            return OperationResult<IReadOnlyList<ConversionProfile>>.Success(profiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading conversion profiles.");
            return OperationResult<IReadOnlyList<ConversionProfile>>.Failure("PROFILE_LOAD_ERR", ex.Message);
        }
    }

    public async Task<OperationResult<ModelDocumentSummary>> InspectModelFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return OperationResult<ModelDocumentSummary>.Failure("FILE_PATH_EMPTY", "Model file path cannot be empty.");
        }

        string fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "UntitledModel";
        }

        ModelFormatType detectedFormat = ConversionDomainRules.DetectFormatFromPath(filePath);
        IFormatDescriptor? descriptor = _formatRegistry.ResolveByFilePath(filePath)
            ?? _formatRegistry.GetByFormatType(detectedFormat);

        GeometryKind geometryKind = descriptor?.NativeGeometryKind ?? GeometryKind.Unknown;
        long fileSize = 0;

        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            fileSize = fileInfo.Length;

            var recentEntry = new RecentFileEntry(filePath, fileName, detectedFormat, fileSize);
            await _recentFileRepository.UpsertAsync(recentEntry, cancellationToken).ConfigureAwait(false);
        }

        var summary = new ModelDocumentSummary(
            name: Path.GetFileNameWithoutExtension(fileName),
            filePath: filePath,
            format: detectedFormat,
            geometryKind: geometryKind,
            fileSizeBytes: fileSize,
            elementCount: 0,
            bounds: BoundingBox3D.Empty);

        _logger.LogInformation(
            "Inspected model file metadata: {FileName} ({Format}, {SizeBytes} bytes)",
            fileName,
            detectedFormat,
            fileSize);

        return OperationResult<ModelDocumentSummary>.Success(summary);
    }

    public async Task<OperationResult<PrepareConversionResult>> PrepareConversionJobAsync(
        PrepareConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        OperationResult validation = PrepareConversionRequestValidator.Validate(request);
        if (validation.IsFailure)
        {
            return OperationResult<PrepareConversionResult>.Failure(validation.ErrorCode, validation.ErrorMessage);
        }

        try
        {
            var inspectResult = await InspectModelFileAsync(request.SourceFilePath, cancellationToken).ConfigureAwait(false);
            if (inspectResult.IsFailure || inspectResult.Value is null)
            {
                return OperationResult<PrepareConversionResult>.Failure(inspectResult.ErrorCode, inspectResult.ErrorMessage);
            }

            IFormatDescriptor? targetDescriptor = _formatRegistry.GetByFormatType(request.TargetFormat);
            string targetExtension = targetDescriptor?.PrimaryExtension ?? ".obj";
            string baseFileName = Path.GetFileNameWithoutExtension(request.SourceFilePath);
            if (baseFileName.EndsWith(".geo", StringComparison.OrdinalIgnoreCase) ||
                baseFileName.EndsWith(".voxel", StringComparison.OrdinalIgnoreCase))
            {
                baseFileName = Path.GetFileNameWithoutExtension(baseFileName);
            }

            string targetFilePath = Path.Combine(request.OutputDirectory, $"{baseFileName}{targetExtension}");
            string jobName = $"{baseFileName} ({request.SourceFormat} -> {request.TargetFormat})";

            var historyRecord = new ConversionHistoryRecord(
                jobName: jobName,
                sourceFilePath: request.SourceFilePath,
                targetFilePath: targetFilePath,
                sourceFormat: request.SourceFormat,
                targetFormat: request.TargetFormat,
                sourceSizeBytes: inspectResult.Value.FileSizeBytes,
                profileId: request.ProfileId);

            await _historyRepository.AddAsync(historyRecord, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Prepared conversion job '{JobName}' [ID: {JobId}] from {SourceFormat} to {TargetFormat}.",
                jobName,
                historyRecord.Id,
                request.SourceFormat,
                request.TargetFormat);

            var result = new PrepareConversionResult(
                HistoryRecord: historyRecord,
                DocumentSummary: inspectResult.Value,
                TargetFilePath: targetFilePath,
                StatusNote: "Job registered in workspace queue (Foundation Stage 01).");

            return OperationResult<PrepareConversionResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare conversion job for {SourceFile}.", request.SourceFilePath);
            return OperationResult<PrepareConversionResult>.Failure("CONV_PREP_ERR", ex.Message);
        }
    }

    public async Task<OperationResult> SaveProfileAsync(
        ConversionProfile profile,
        CancellationToken cancellationToken = default)
    {
        OperationResult validation = ConversionDomainRules.ValidateProfile(profile);
        if (validation.IsFailure)
        {
            return validation;
        }

        try
        {
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _profileRepository.UpsertAsync(profile, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Saved conversion profile '{ProfileName}' ({ProfileId}).", profile.Name, profile.Id);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save conversion profile '{ProfileName}'.", profile.Name);
            return OperationResult.Failure("PROFILE_SAVE_ERR", ex.Message);
        }
    }
}

