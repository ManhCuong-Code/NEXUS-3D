using System.Diagnostics;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Enums;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Abstractions;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Conversion;
using NEXUS3D.Formats.Obj.Exporting;
using NEXUS3D.Formats.Obj.Materials;
using NEXUS3D.Formats.Obj.Parsing;
using NEXUS3D.Formats.Obj.Textures;
using NEXUS3D.Shared.Results;

namespace NEXUS3D.Formats.Obj.Services;

/// <summary>
/// Unified contract for exporting USM <see cref="Scene3D"/> or <see cref="ObjDocument"/> + <see cref="MtlDocument"/>
/// to Wavefront OBJ/MTL streams and multi-file outputs with atomic verification.
/// </summary>
public interface IObjWriter : IFormatExporter, IObjExporter
{
    Task<ObjExportResult> ExportSceneToFileAsync(
        Scene3D scene,
        string objFilePath,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<ObjExportResult> ExportDocumentsToFileAsync(
        ObjDocument objDocument,
        MtlDocument? mtlDocument,
        string objFilePath,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// High-level Wavefront OBJ &amp; MTL exporter implementing atomic multi-file staging (`.obj` + `.mtl` + copied textures)
/// and read-back verification before committing target files.
/// </summary>
public sealed class ObjFormatExporter : IObjWriter
{
    private readonly ISceneToObjConverter _sceneToObjConverter;
    private readonly IObjDocumentExporter _objExporter;
    private readonly IMtlExporter _mtlExporter;
    private readonly IObjDocumentParser _objParserForVerification;
    private readonly IMtlParser _mtlParserForVerification;
    private readonly TexturePathResolver _texturePathResolver;

    public string FormatIdentifier => "wavefront:obj";
    public ModelFormatType TargetFormat => ModelFormatType.Obj;
    public bool EmitNormals => true;
    public bool EmitTexCoords => true;

    public ObjFormatExporter(
        ISceneToObjConverter? sceneToObjConverter = null,
        IObjDocumentExporter? objExporter = null,
        IMtlExporter? mtlExporter = null,
        IObjDocumentParser? objParserForVerification = null,
        IMtlParser? mtlParserForVerification = null,
        TexturePathResolver? texturePathResolver = null)
    {
        _sceneToObjConverter = sceneToObjConverter ?? new SceneToObjConverter();
        _objExporter = objExporter ?? new ObjExporter();
        _mtlExporter = mtlExporter ?? new MtlExporter();
        _objParserForVerification = objParserForVerification ?? new ObjParser();
        _mtlParserForVerification = mtlParserForVerification ?? new MtlParser();
        _texturePathResolver = texturePathResolver ?? new TexturePathResolver();
    }

    public async Task<ObjExportResult> ExportSceneToFileAsync(
        Scene3D scene,
        string objFilePath,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(objFilePath);
        options ??= ObjExportOptions.Default;

        var mappingOptions = new ObjMappingOptions
        {
            PreserveRawData = true,
            ExportOptions = options
        };

        var (objDoc, mtlDoc, _) = _sceneToObjConverter.Convert(scene, mappingOptions);

        // Determine source base directory for texture copying from scene textures if not explicitly set
        string? inferredBaseDir = options.SourceBaseDirectory;
        if (string.IsNullOrWhiteSpace(inferredBaseDir))
        {
            foreach (var tex in scene.Textures)
            {
                if (tex.Metadata.Tags.TryGetValue("obj.resolvedFullPath", out string? fullTexPath) &&
                    !string.IsNullOrWhiteSpace(fullTexPath) &&
                    !string.IsNullOrWhiteSpace(tex.Reference.RelativePath))
                {
                    // Infer base directory from fullTexPath and relativePath
                    string normalizedRel = tex.Reference.RelativePath
                        .Replace('/', Path.DirectorySeparatorChar)
                        .Replace('\\', Path.DirectorySeparatorChar);
                    if (fullTexPath.EndsWith(normalizedRel, StringComparison.OrdinalIgnoreCase))
                    {
                        inferredBaseDir = fullTexPath[..^normalizedRel.Length].TrimEnd(Path.DirectorySeparatorChar);
                        break;
                    }
                }
            }
        }

        ObjExportOptions effectiveOptions = string.IsNullOrWhiteSpace(options.SourceBaseDirectory) && !string.IsNullOrWhiteSpace(inferredBaseDir)
            ? new ObjExportOptions
            {
                FloatPrecision = options.FloatPrecision,
                TriangulatePolygons = options.TriangulatePolygons,
                WriteNormals = options.WriteNormals,
                WriteTexCoords = options.WriteTexCoords,
                WriteComments = options.WriteComments,
                ExportCompanionMtl = options.ExportCompanionMtl,
                CopyTextures = options.CopyTextures,
                PreserveTextureDirectoryStructure = options.PreserveTextureDirectoryStructure,
                AllowOverwrite = options.AllowOverwrite,
                VerifyAfterExport = options.VerifyAfterExport,
                WriteUnrecognizedStatements = options.WriteUnrecognizedStatements,
                MtlFileName = options.MtlFileName,
                SourceBaseDirectory = inferredBaseDir
            }
            : options;

        return await ExportDocumentsToFileAsync(
            objDoc,
            mtlDoc.Materials.Count > 0 ? mtlDoc : null,
            objFilePath,
            effectiveOptions,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ObjExportResult> ExportDocumentsToFileAsync(
        ObjDocument objDocument,
        MtlDocument? mtlDocument,
        string objFilePath,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(objFilePath);
        options ??= ObjExportOptions.Default;

        var sw = Stopwatch.StartNew();
        var issues = new List<ValidationIssue>();

        string fullObjPath = Path.GetFullPath(objFilePath);
        string targetDir = Path.GetDirectoryName(fullObjPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(targetDir);

        if (File.Exists(fullObjPath) && !options.AllowOverwrite)
        {
            throw new ObjException(
                ObjErrorCodes.ExportFailed,
                $"Target OBJ file '{fullObjPath}' already exists and AllowOverwrite is false.");
        }

        bool shouldWriteMtl = options.ExportCompanionMtl && mtlDocument is { Materials.Count: > 0 };
        string? mtlFileName = null;
        string? fullMtlPath = null;

        if (shouldWriteMtl)
        {
            mtlFileName = !string.IsNullOrWhiteSpace(options.MtlFileName)
                ? options.MtlFileName
                : (objDocument.MaterialLibraries.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
                    ?? $"{Path.GetFileNameWithoutExtension(fullObjPath)}.mtl");

            fullMtlPath = Path.GetFullPath(Path.Combine(targetDir, mtlFileName.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(fullMtlPath) && !options.AllowOverwrite)
            {
                throw new ObjException(
                    ObjErrorCodes.ExportFailed,
                    $"Target MTL file '{fullMtlPath}' already exists and AllowOverwrite is false.");
            }
        }

        // Atomic staging directory inside targetDir
        string stagingDir = Path.Combine(targetDir, $".nexus_obj_stage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);

        try
        {
            string stagedObjPath = Path.Combine(stagingDir, Path.GetFileName(fullObjPath));
            string? stagedMtlPath = shouldWriteMtl && mtlFileName is not null
                ? Path.Combine(stagingDir, Path.GetFileName(mtlFileName))
                : null;

            MtlDocument? mtlToExport = mtlDocument?.DeepClone();
            var copiedTextureDestinations = new List<string>();

            // Copy textures into staging directory if requested
            if (shouldWriteMtl && mtlToExport is not null && options.CopyTextures)
            {
                string? sourceBaseDir = options.SourceBaseDirectory;
                if (string.IsNullOrWhiteSpace(sourceBaseDir) && !string.IsNullOrWhiteSpace(mtlToExport.SourcePath))
                {
                    sourceBaseDir = Path.GetDirectoryName(mtlToExport.SourcePath);
                }

                var allocatedRelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (MtlMaterial mat in mtlToExport.Materials)
                {
                    foreach (var (_, statement) in mat.GetTextureMaps())
                    {
                        string? sourceFullTexPath = _texturePathResolver.ResolveFullPath(statement.TexturePath, sourceBaseDir);
                        if (string.IsNullOrWhiteSpace(sourceFullTexPath) || !File.Exists(sourceFullTexPath))
                        {
                            issues.Add(new ValidationIssue(
                                ObjErrorCodes.MissingTextureFile,
                                $"Cannot copy missing texture '{statement.TexturePath}' referenced by material '{mat.Name}'.",
                                ValidationSeverity.Warning,
                                $"Material:{mat.Name}"));
                            continue;
                        }

                        string exportedRelPath = await _texturePathResolver.CopyTextureForExportAsync(
                            sourceFullTexPath,
                            statement.TexturePath,
                            stagingDir,
                            options.PreserveTextureDirectoryStructure,
                            allowOverwrite: true,
                            allocatedRelPaths,
                            cancellationToken).ConfigureAwait(false);

                        statement.TexturePath = exportedRelPath;
                        copiedTextureDestinations.Add(exportedRelPath);
                    }
                }
            }

            ObjExportOptions stagedOptions = shouldWriteMtl && mtlFileName is not null
                ? new ObjExportOptions
                {
                    FloatPrecision = options.FloatPrecision,
                    TriangulatePolygons = options.TriangulatePolygons,
                    WriteNormals = options.WriteNormals,
                    WriteTexCoords = options.WriteTexCoords,
                    WriteComments = options.WriteComments,
                    ExportCompanionMtl = options.ExportCompanionMtl,
                    CopyTextures = options.CopyTextures,
                    PreserveTextureDirectoryStructure = options.PreserveTextureDirectoryStructure,
                    AllowOverwrite = true,
                    VerifyAfterExport = false,
                    WriteUnrecognizedStatements = options.WriteUnrecognizedStatements,
                    MtlFileName = Path.GetFileName(mtlFileName)
                }
                : options;

            ObjExportResult objWriteResult = await _objExporter.ExportFileAsync(
                objDocument,
                stagedObjPath,
                stagedOptions,
                cancellationToken).ConfigureAwait(false);

            issues.AddRange(objWriteResult.Issues);

            if (shouldWriteMtl && mtlToExport is not null && stagedMtlPath is not null)
            {
                IReadOnlyList<ValidationIssue> mtlIssues = await _mtlExporter.ExportFileAsync(
                    mtlToExport,
                    stagedMtlPath,
                    stagedOptions,
                    cancellationToken).ConfigureAwait(false);
                issues.AddRange(mtlIssues);
            }

            // Read-back verification before committing staged files
            if (options.VerifyAfterExport)
            {
                await using var verifyObjStream = new FileStream(
                    stagedObjPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 65536,
                    useAsync: true);

                ObjDocument verifiedObj = await _objParserForVerification.ParseDocumentAsync(
                    verifyObjStream,
                    new ObjParseOptions { StrictValidation = false, SourceFilePath = stagedObjPath },
                    cancellationToken).ConfigureAwait(false);

                if (verifiedObj.Positions.Count != objWriteResult.VertexCount ||
                    verifiedObj.Faces.Count != objWriteResult.FaceCount)
                {
                    throw new ObjException(
                        ObjErrorCodes.ExportFailed,
                        "Post-export OBJ verification failed: read-back vertex or face count did not match exported document.");
                }

                if (shouldWriteMtl && mtlToExport is not null && stagedMtlPath is not null)
                {
                    MtlParseResult verifiedMtl = await _mtlParserForVerification.ParseFileAsync(
                        stagedMtlPath,
                        MtlParseOptions.Default,
                        cancellationToken).ConfigureAwait(false);

                    if (verifiedMtl.HasErrors || verifiedMtl.Document.Materials.Count != mtlToExport.Materials.Count)
                    {
                        throw new ObjException(
                            ObjErrorCodes.ExportFailed,
                            "Post-export MTL verification failed: read-back material count did not match exported MTL library.");
                    }
                }
            }

            // Commit staged files into targetDir
            File.Move(stagedObjPath, fullObjPath, overwrite: options.AllowOverwrite);

            if (shouldWriteMtl && stagedMtlPath is not null && fullMtlPath is not null)
            {
                string? mtlSubDir = Path.GetDirectoryName(fullMtlPath);
                if (!string.IsNullOrWhiteSpace(mtlSubDir))
                {
                    Directory.CreateDirectory(mtlSubDir);
                }

                File.Move(stagedMtlPath, fullMtlPath, overwrite: options.AllowOverwrite);
            }

            var finalCopiedTexturePaths = new List<string>(copiedTextureDestinations.Count);
            foreach (string relTex in copiedTextureDestinations)
            {
                string stagedTexFull = Path.GetFullPath(Path.Combine(stagingDir, relTex.Replace('/', Path.DirectorySeparatorChar)));
                string targetTexFull = Path.GetFullPath(Path.Combine(targetDir, relTex.Replace('/', Path.DirectorySeparatorChar)));

                if (File.Exists(stagedTexFull))
                {
                    string? texSubDir = Path.GetDirectoryName(targetTexFull);
                    if (!string.IsNullOrWhiteSpace(texSubDir))
                    {
                        Directory.CreateDirectory(texSubDir);
                    }

                    if (!File.Exists(targetTexFull) || options.AllowOverwrite)
                    {
                        File.Move(stagedTexFull, targetTexFull, overwrite: options.AllowOverwrite);
                    }

                    finalCopiedTexturePaths.Add(targetTexFull);
                }
            }

            sw.Stop();
            return new ObjExportResult
            {
                Succeeded = true,
                ObjFilePath = fullObjPath,
                MtlFilePath = shouldWriteMtl ? fullMtlPath : null,
                CopiedTexturePaths = finalCopiedTexturePaths,
                VertexCount = objWriteResult.VertexCount,
                TextureCoordCount = objWriteResult.TextureCoordCount,
                NormalCount = objWriteResult.NormalCount,
                FaceCount = objWriteResult.FaceCount,
                MaterialCount = shouldWriteMtl && mtlToExport is not null ? mtlToExport.Materials.Count : 0,
                Issues = issues,
                Elapsed = sw.Elapsed
            };
        }
        finally
        {
            if (Directory.Exists(stagingDir))
            {
                try
                {
                    Directory.Delete(stagingDir, recursive: true);
                }
                catch
                {
                    // Ignore best-effort staging cleanup failure
                }
            }
        }
    }

    public async Task<FormatExportReport> ExportSceneAsync(
        Scene3D scene,
        Stream destination,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            var mappingOptions = new ObjMappingOptions
            {
                StrictMode = options?.StrictMapping ?? false,
                PreserveRawData = options?.PreserveUnknownData ?? true
            };

            var (objDoc, _, convReport) = _sceneToObjConverter.Convert(scene, mappingOptions);

            using var memoryStream = new MemoryStream();
            ObjExportResult exportResult = await _objExporter.ExportAsync(
                objDoc,
                memoryStream,
                ObjExportOptions.Default,
                cancellationToken).ConfigureAwait(false);

            if (options?.VerifyAfterWrite ?? true)
            {
                memoryStream.Position = 0;
                ObjDocument verified = await _objParserForVerification.ParseDocumentAsync(
                    memoryStream,
                    new ObjParseOptions { StrictValidation = false },
                    cancellationToken).ConfigureAwait(false);

                if (verified.Positions.Count != exportResult.VertexCount ||
                    verified.Faces.Count != exportResult.FaceCount)
                {
                    return new FormatExportReport(
                        IsSuccess: false,
                        BytesWritten: 0,
                        LossReport: convReport.LossReport,
                        Warnings: convReport.Warnings,
                        Error: new Error(ObjErrorCodes.ExportFailed, "Stream read-back verification failed after OBJ export."));
                }
            }

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

            return new FormatExportReport(
                IsSuccess: true,
                BytesWritten: memoryStream.Length,
                LossReport: convReport.LossReport,
                Warnings: convReport.Warnings);
        }
        catch (ObjException ex)
        {
            return new FormatExportReport(
                IsSuccess: false,
                BytesWritten: 0,
                LossReport: new LossReport(),
                Warnings: [],
                Error: new Error(ex.ErrorCode, ex.Message));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new FormatExportReport(
                IsSuccess: false,
                BytesWritten: 0,
                LossReport: new LossReport(),
                Warnings: [],
                Error: new Error(ObjErrorCodes.ExportFailed, ex.Message));
        }
    }

    public Task<OperationResult> ValidateTargetAsync(
        string outputFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return Task.FromResult(OperationResult.Failure(ObjErrorCodes.ExportFailed, "Output file path cannot be empty."));
        }

        string ext = Path.GetExtension(outputFilePath);
        if (!ext.Equals(".obj", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(OperationResult.Failure(
                ObjErrorCodes.ExportFailed,
                $"Extension '{ext}' is not a supported Wavefront OBJ extension (.obj)."));
        }

        return Task.FromResult(OperationResult.Success());
    }
}
