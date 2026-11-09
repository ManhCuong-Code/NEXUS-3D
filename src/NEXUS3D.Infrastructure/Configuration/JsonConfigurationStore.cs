using System.Text.Json;
using System.Text.Json.Serialization;
using NEXUS3D.Shared.Guards;

namespace NEXUS3D.Infrastructure.Configuration;

/// <summary>
/// Provides asynchronous JSON file persistence using <see cref="System.Text.Json"/>.
/// </summary>
public sealed class JsonConfigurationStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task SaveAsync<T>(string filePath, T data, CancellationToken cancellationToken = default) where T : class
    {
        Guard.NotNullOrWhiteSpace(filePath);
        Guard.NotNull(data);

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(stream, data, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> LoadAsync<T>(string filePath, CancellationToken cancellationToken = default) where T : class
    {
        Guard.NotNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            return default;
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await JsonSerializer.DeserializeAsync<T>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}
