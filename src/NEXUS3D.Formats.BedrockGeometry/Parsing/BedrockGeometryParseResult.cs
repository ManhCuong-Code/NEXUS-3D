using System.Text.Json;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Parsing;

/// <summary>
/// Diagnostic item produced during Minecraft Bedrock Geometry JSON parsing or validation.
/// </summary>
public sealed record BedrockGeometryDiagnostic(
    string Code,
    string Message,
    bool IsError = true,
    string? FilePath = null,
    string? JsonPath = null);

/// <summary>
/// Result of parsing a Minecraft Bedrock Geometry (<c>.geo.json</c>) document.
/// </summary>
public sealed class BedrockGeometryParseResult
{
    public required BedrockGeometryDocument Document { get; init; }
    public IReadOnlyList<BedrockGeometryDiagnostic> Diagnostics { get; init; } = [];
    public TimeSpan Elapsed { get; init; }

    public bool HasErrors => Diagnostics.Any(d => d.IsError);
    public bool HasWarnings => Diagnostics.Any(d => !d.IsError);
}

internal static class BedrockJsonExtensionHelpers
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
                "Bedrock Geometry JSON document is empty or whitespace only.",
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
                $"Invalid Bedrock Geometry JSON syntax: {ex.Message}",
                ex.Path ?? filePath,
                ex.LineNumber,
                ex.BytePositionInLine,
                ex);
        }
    }
}
