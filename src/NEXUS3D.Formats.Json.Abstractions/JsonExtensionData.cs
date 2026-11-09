using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Validation;

namespace NEXUS3D.Formats.Json.Abstractions;

/// <summary>
/// Preserves unknown or vendor-specific JSON properties as cloned <see cref="JsonElement"/> trees
/// without losing structure or primitive types.
/// </summary>
public sealed class JsonExtensionData
{
    private readonly Dictionary<string, JsonElement> _properties = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, JsonElement> Properties => _properties;
    public int Count => _properties.Count;
    public bool IsEmpty => _properties.Count == 0;

    public void Set(string propertyName, JsonElement element)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        _properties[propertyName] = element.Clone();
    }

    public bool TryGet(string propertyName, out JsonElement element) =>
        _properties.TryGetValue(propertyName, out element);

    public void WriteTo(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        foreach (var kvp in _properties)
        {
            writer.WritePropertyName(kvp.Key);
            kvp.Value.WriteTo(writer);
        }
    }

    public JsonExtensionData DeepClone()
    {
        var clone = new JsonExtensionData();
        foreach (var kvp in _properties)
        {
            clone._properties[kvp.Key] = kvp.Value.Clone();
        }

        return clone;
    }
}

/// <summary>
/// Shared streaming, validation, and element-reading utilities for Stage 05 JSON parsers and exporters.
/// </summary>
public static class JsonParsingUtilities
{
    /// <summary>
    /// Reads a <see cref="JsonDocument"/> from a stream while enforcing <see cref="JsonReadOptions.MaxInputBytes"/>
    /// and <see cref="JsonReadOptions.MaxDepth"/>.
    /// </summary>
    public static async Task<JsonDocument> ParseBoundedDocumentAsync(
        Stream source,
        JsonReadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        if (source.CanSeek && source.Length - source.Position > options.MaxInputBytes)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.LimitExceeded,
                $"JSON stream size ({source.Length - source.Position} bytes) exceeds maximum allowed limit ({options.MaxInputBytes} bytes).");
        }

        // Read bounded bytes into memory buffer so we enforce MaxInputBytes even on non-seekable streams
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[65536];
        long totalRead = 0;

        while (true)
        {
            int read = await source.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > options.MaxInputBytes)
            {
                throw new JsonFormatException(
                    JsonFormatErrorCodes.LimitExceeded,
                    $"JSON stream exceeds maximum allowed size ({options.MaxInputBytes} bytes).");
            }

            buffer.Write(chunk, 0, read);
        }

        if (totalRead == 0)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.EmptyOrInvalidRoot,
                "JSON input stream is empty.");
        }

        buffer.Position = 0;
        var docOptions = new JsonDocumentOptions
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            CommentHandling = options.AllowComments ? JsonCommentHandling.Skip : JsonCommentHandling.Disallow,
            MaxDepth = Math.Clamp(options.MaxDepth, 1, 1000)
        };

        try
        {
            return await JsonDocument.ParseAsync(buffer, docOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.SyntaxError,
                $"Invalid JSON syntax: {ex.Message}",
                ex.Path,
                ex.LineNumber,
                ex.BytePositionInLine,
                ex);
        }
    }

    public static Vector3D ReadVector3DArray(JsonElement element, string jsonPath)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 3)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.TypeMismatch,
                $"Expected a 3-element numeric array [x, y, z], but found {element.ValueKind}.",
                jsonPath);
        }

        double x = ReadFiniteDouble(element[0], $"{jsonPath}[0]");
        double y = ReadFiniteDouble(element[1], $"{jsonPath}[1]");
        double z = ReadFiniteDouble(element[2], $"{jsonPath}[2]");
        return new Vector3D(x, y, z);
    }

    public static Vector2D ReadVector2DArray(JsonElement element, string jsonPath)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 2)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.TypeMismatch,
                $"Expected a 2-element numeric array [u, v], but found {element.ValueKind}.",
                jsonPath);
        }

        double u = ReadFiniteDouble(element[0], $"{jsonPath}[0]");
        double v = ReadFiniteDouble(element[1], $"{jsonPath}[1]");
        return new Vector2D(u, v);
    }

    public static double ReadFiniteDouble(JsonElement element, string jsonPath)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out double val) || !double.IsFinite(val))
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.InvalidNumericValue,
                $"Expected a finite number at '{jsonPath}', but found '{element}'.",
                jsonPath);
        }

        return val;
    }

    public static int ReadInt32(JsonElement element, string jsonPath)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int val))
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.InvalidNumericValue,
                $"Expected an integer at '{jsonPath}', but found '{element}'.",
                jsonPath);
        }

        return val;
    }

    public static void HandleUnknownProperty(
        JsonProperty property,
        string parentPath,
        JsonExtensionData extensionData,
        JsonReadOptions options,
        List<ValidationIssue> issues)
    {
        string path = $"{parentPath}.{property.Name}";
        if (options.PreserveExtensionData)
        {
            extensionData.Set(property.Name, property.Value);
        }

        ValidationSeverity severity = options.StrictMode ? ValidationSeverity.Error : ValidationSeverity.Warning;
        issues.Add(new ValidationIssue(
            JsonFormatErrorCodes.UnknownExtensionProperty,
            $"Unrecognized JSON property '{property.Name}' at '{path}' was preserved in extension data.",
            severity,
            path));

        if (options.StrictMode)
        {
            throw new JsonFormatException(
                JsonFormatErrorCodes.UnknownExtensionProperty,
                $"Strict mode rejected unknown JSON property '{property.Name}'.",
                path);
        }
    }

    public static void WriteVector3DArray(Utf8JsonWriter writer, string propertyName, Vector3D v, JsonWriteOptions options)
    {
        writer.WriteStartArray(propertyName);
        writer.WriteNumberValue(options.RoundCoordinate(v.X));
        writer.WriteNumberValue(options.RoundCoordinate(v.Y));
        writer.WriteNumberValue(options.RoundCoordinate(v.Z));
        writer.WriteEndArray();
    }

    public static void WriteVector2DArray(Utf8JsonWriter writer, string propertyName, Vector2D v, JsonWriteOptions options)
    {
        writer.WriteStartArray(propertyName);
        writer.WriteNumberValue(options.RoundCoordinate(v.X));
        writer.WriteNumberValue(options.RoundCoordinate(v.Y));
        writer.WriteEndArray();
    }
}
