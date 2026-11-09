using System.Runtime.CompilerServices;
using System.Text;
using NEXUS3D.Formats.Obj.Common;

namespace NEXUS3D.Formats.Obj.Parsing;

/// <summary>
/// Represents a single logical line emitted by <see cref="ObjLexer"/>.
/// </summary>
public readonly record struct ObjLexedLine(
    int LineNumber,
    string Content,
    string? Comment,
    bool IsCommentOnly);

/// <summary>
/// Streaming line lexer for Wavefront OBJ and MTL text streams.
/// Handles UTF-8 validation, line-continuation backslashes (<c>\</c>), inline comments (<c>#</c>),
/// and enforces maximum file size and line length limits.
/// </summary>
public sealed class ObjLexer
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async IAsyncEnumerable<ObjLexedLine> TokenizeLinesAsync(
        Stream source,
        long maxFileSizeBytes,
        int maxLineLength,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.CanSeek && (source.Length - source.Position) > maxFileSizeBytes)
        {
            throw new ObjException(
                ObjErrorCodes.LimitExceeded,
                $"Input stream size ({source.Length - source.Position} bytes) exceeds MaxFileSizeBytes ({maxFileSizeBytes} bytes).");
        }

        using var reader = new StreamReader(
            source,
            StrictUtf8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 65536,
            leaveOpen: true);

        long totalCharsRead = 0;
        int physicalLineNumber = 0;
        int logicalStartLine = 0;
        StringBuilder? continuationBuffer = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? rawLine;
            try
            {
                rawLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DecoderFallbackException ex)
            {
                throw new ObjException(
                    ObjErrorCodes.InvalidEncoding,
                    $"Stream contains invalid UTF-8 byte sequence near line {physicalLineNumber + 1}.",
                    ex,
                    physicalLineNumber + 1);
            }

            if (rawLine is null)
            {
                break;
            }

            physicalLineNumber++;
            totalCharsRead += rawLine.Length + 1;

            if (totalCharsRead > maxFileSizeBytes)
            {
                throw new ObjException(
                    ObjErrorCodes.LimitExceeded,
                    $"Stream content exceeds maximum allowed size ({maxFileSizeBytes} bytes).",
                    physicalLineNumber);
            }

            if (rawLine.Length > maxLineLength)
            {
                throw new ObjException(
                    ObjErrorCodes.LimitExceeded,
                    $"Line {physicalLineNumber} length ({rawLine.Length}) exceeds MaxLineLength ({maxLineLength}).",
                    physicalLineNumber);
            }

            // Reject binary files masquerading as text (null bytes)
            if (rawLine.Contains('\0', StringComparison.Ordinal))
            {
                throw new ObjException(
                    ObjErrorCodes.InvalidEncoding,
                    $"Line {physicalLineNumber} contains binary null byte ('\\0'); input is not a valid text OBJ/MTL file.",
                    physicalLineNumber);
            }

            ReadOnlySpan<char> trimmedEnd = rawLine.AsSpan().TrimEnd();

            // Check for OBJ backslash line continuation (only outside comment-only lines)
            ReadOnlySpan<char> trimmedStart = trimmedEnd.TrimStart();
            bool isCommentLine = trimmedStart.Length > 0 && trimmedStart[0] == '#';

            if (!isCommentLine && trimmedEnd.Length > 0 && trimmedEnd[^1] == '\\')
            {
                continuationBuffer ??= new StringBuilder();
                if (continuationBuffer.Length == 0)
                {
                    logicalStartLine = physicalLineNumber;
                }

                continuationBuffer.Append(trimmedEnd[..^1]);
                continuationBuffer.Append(' ');

                if (continuationBuffer.Length > maxLineLength)
                {
                    throw new ObjException(
                        ObjErrorCodes.LimitExceeded,
                        $"Logical continued line starting at {logicalStartLine} exceeds MaxLineLength ({maxLineLength}).",
                        logicalStartLine);
                }

                continue;
            }

            string logicalLine;
            int effectiveLineNumber;
            if (continuationBuffer is { Length: > 0 })
            {
                continuationBuffer.Append(rawLine);
                if (continuationBuffer.Length > maxLineLength)
                {
                    throw new ObjException(
                        ObjErrorCodes.LimitExceeded,
                        $"Logical continued line starting at {logicalStartLine} exceeds MaxLineLength ({maxLineLength}).",
                        logicalStartLine);
                }

                logicalLine = continuationBuffer.ToString();
                effectiveLineNumber = logicalStartLine;
                continuationBuffer.Clear();
            }
            else
            {
                logicalLine = rawLine;
                effectiveLineNumber = physicalLineNumber;
            }

            var parsed = SplitContentAndComment(effectiveLineNumber, logicalLine);
            if (!string.IsNullOrWhiteSpace(parsed.Content) || parsed.Comment is not null)
            {
                yield return parsed;
            }
        }

        if (continuationBuffer is { Length: > 0 })
        {
            var parsed = SplitContentAndComment(logicalStartLine, continuationBuffer.ToString());
            if (!string.IsNullOrWhiteSpace(parsed.Content) || parsed.Comment is not null)
            {
                yield return parsed;
            }
        }
    }

    private static ObjLexedLine SplitContentAndComment(int lineNumber, string line)
    {
        int hashIndex = line.IndexOf('#', StringComparison.Ordinal);
        if (hashIndex < 0)
        {
            return new ObjLexedLine(lineNumber, line.Trim(), null, IsCommentOnly: false);
        }

        string content = line[..hashIndex].Trim();
        string comment = line[(hashIndex + 1)..].Trim();
        return new ObjLexedLine(
            lineNumber,
            content,
            comment,
            IsCommentOnly: string.IsNullOrEmpty(content));
    }
}
