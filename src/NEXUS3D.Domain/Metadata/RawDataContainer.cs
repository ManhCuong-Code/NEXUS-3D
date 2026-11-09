using System.Text.Json;
using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Interfaces;

namespace NEXUS3D.Domain.Metadata;

public enum RawPayloadKind
{
    Json = 0,
    Binary = 1
}

/// <summary>
/// Safely preserves unmapped raw JSON or binary payloads from source files with strict size limits.
/// Never executes or deserializes arbitrary untrusted types.
/// </summary>
public sealed class RawDataContainer : IDeepCloneable<RawDataContainer>
{
    public const int DefaultMaxPayloadBytes = 4 * 1024 * 1024; // 4 MB cap per container

    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string SourceFormat { get; set; } = string.Empty;
    public RawPayloadKind PayloadKind { get; set; } = RawPayloadKind.Json;
    public string? SchemaVersion { get; set; }
    public int MaxAllowedBytes { get; set; } = DefaultMaxPayloadBytes;
    public JsonElement? JsonPayload { get; set; }
    public byte[]? BinaryPayload { get; set; }

    public int GetEstimatedSizeBytes()
    {
        if (PayloadKind == RawPayloadKind.Binary)
        {
            return BinaryPayload?.Length ?? 0;
        }

        if (JsonPayload.HasValue && JsonPayload.Value.ValueKind != JsonValueKind.Undefined)
        {
            return JsonPayload.Value.GetRawText().Length;
        }

        return 0;
    }

    public bool IsWithinSizeLimit()
    {
        int size = GetEstimatedSizeBytes();
        return MaxAllowedBytes > 0 && size >= 0 && size <= MaxAllowedBytes;
    }

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(SourceFormat))
        {
            return false;
        }

        if (!IsWithinSizeLimit())
        {
            return false;
        }

        return PayloadKind switch
        {
            RawPayloadKind.Json => JsonPayload.HasValue && JsonPayload.Value.ValueKind != JsonValueKind.Undefined,
            RawPayloadKind.Binary => BinaryPayload is { Length: > 0 },
            _ => false
        };
    }

    public RawDataContainer DeepClone() => new()
    {
        Id = Id,
        Name = Name,
        SourceFormat = SourceFormat,
        PayloadKind = PayloadKind,
        SchemaVersion = SchemaVersion,
        MaxAllowedBytes = MaxAllowedBytes,
        JsonPayload = JsonPayload.HasValue && JsonPayload.Value.ValueKind != JsonValueKind.Undefined
            ? JsonPayload.Value.Clone()
            : null,
        BinaryPayload = BinaryPayload is not null ? (byte[])BinaryPayload.Clone() : null
    };
}

