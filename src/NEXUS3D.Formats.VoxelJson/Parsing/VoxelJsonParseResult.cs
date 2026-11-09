using System.Text.Json;
using NEXUS3D.Formats.Json.Abstractions;
using NEXUS3D.Formats.VoxelJson.Models;

namespace NEXUS3D.Formats.VoxelJson.Parsing;

/// <summary>
/// Diagnostic item produced during Voxel JSON parsing or validation.
/// </summary>
public sealed record JsonFormatDiagnostic(
    string Code,
    string Message,
    bool IsError = true,
    string? FilePath = null,
    string? JsonPath = null);

/// <summary>
/// Semantic error code aliases mapping Voxel JSON conditions to <see cref="JsonFormatErrorCodes"/>.
/// </summary>
public static class JsonErrorCodes
{
    public const string EmptyOrInvalidRoot = JsonFormatErrorCodes.EmptyOrInvalidRoot;
    public const string SyntaxError = JsonFormatErrorCodes.SyntaxError;
    public const string UnsupportedFormat = JsonFormatErrorCodes.UnsupportedSchemaOrDialect;
    public const string MissingRequiredField = JsonFormatErrorCodes.MissingRequiredProperty;
    public const string InvalidCoordinate = JsonFormatErrorCodes.InvalidNumericValue;
    public const string InvalidDimensions = JsonFormatErrorCodes.InvalidNumericValue;
    public const string InvalidVoxelElement = JsonFormatErrorCodes.TypeMismatch;
    public const string DuplicateVoxel = JsonFormatErrorCodes.DuplicateIdentifierOrCoordinate;
    public const string BrokenReference = JsonFormatErrorCodes.BrokenReferenceOrHierarchy;
    public const string InvalidUv = JsonFormatErrorCodes.InvalidNumericValue;
    public const string LimitExceeded = JsonFormatErrorCodes.LimitExceeded;
    public const string AnimationFileNotSupported = JsonFormatErrorCodes.MisplacedAnimationDocument;
}

internal static class VoxelJsonExtensionHelpers
{
    public static void CaptureUnknownProperties(
        this JsonExtensionData extensionData,
        JsonElement obj,
        HashSet<string> knownProperties)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty prop in obj.EnumerateObject())
        {
            if (!knownProperties.Contains(prop.Name))
            {
                extensionData.Set(prop.Name, prop.Value);
            }
        }
    }

    public static JsonDocument ParseDocument(Stream stream, JsonReadOptions options, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        if (stream.CanSeek && stream.Length - stream.Position > options.MaxInputBytes)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.LimitExceeded,
                $"JSON stream size ({stream.Length - stream.Position} bytes) exceeds maximum allowed limit ({options.MaxInputBytes} bytes).",
                filePath ?? "$");
        }

        using var buffer = new MemoryStream();
        byte[] chunk = new byte[65536];
        long totalRead = 0;

        while (true)
        {
            int read = stream.Read(chunk, 0, chunk.Length);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > options.MaxInputBytes)
            {
                throw new JsonFormatException(
                    JsonFormatErrorCodes.LimitExceeded,
                    $"JSON stream exceeds maximum allowed size ({options.MaxInputBytes} bytes).",
                    filePath ?? "$");
            }

            buffer.Write(chunk, 0, read);
        }

        return ParseDocument(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), options, filePath);
    }

    public static JsonDocument ParseDocument(ReadOnlyMemory<byte> utf8Bytes, JsonReadOptions options, string? filePath = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (utf8Bytes.Length > options.MaxInputBytes)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.LimitExceeded,
                $"JSON input size ({utf8Bytes.Length} bytes) exceeds maximum allowed limit ({options.MaxInputBytes} bytes).",
                filePath ?? "$");
        }

        ReadOnlySpan<byte> span = utf8Bytes.Span;
        // Trim UTF-8 BOM if present
        if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
        {
            utf8Bytes = utf8Bytes[3..];
            span = utf8Bytes.Span;
        }

        bool allWhitespace = true;
        for (int i = 0; i < span.Length; i++)
        {
            byte b = span[i];
            if (b is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                allWhitespace = false;
                break;
            }
        }

        if (allWhitespace)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.EmptyOrInvalidRoot,
                "JSON document is empty or whitespace only.",
                filePath ?? "$");
        }

        var docOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            CommentHandling = options.AllowComments ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow,
            MaxDepth = Math.Clamp(options.MaxDepth, 1, 1000)
        };

        try
        {
            return JsonDocument.Parse(utf8Bytes, docOptions);
        }
        catch (JsonException ex)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.SyntaxError,
                $"Invalid JSON syntax: {ex.Message}",
                ex.Path ?? filePath,
                ex.LineNumber,
                ex.BytePositionInLine,
                ex);
        }
    }
}

/// <summary>
/// Result of parsing a Voxel JSON document, including diagnostics and duplicate statistics.
/// </summary>
public sealed class VoxelJsonParseResult
{
    public required VoxelDocument Document { get; init; }
    public required VoxelJsonDialect DetectedDialect { get; init; }
    public IReadOnlyList<JsonFormatDiagnostic> Diagnostics { get; init; } = [];
    public int RawVoxelCount { get; init; }
    public int DuplicateVoxelCount { get; init; }
    public TimeSpan Elapsed { get; init; }

    public bool HasErrors => Diagnostics.Any(d => d.IsError);
    public bool HasWarnings => Diagnostics.Any(d => !d.IsError);
}
