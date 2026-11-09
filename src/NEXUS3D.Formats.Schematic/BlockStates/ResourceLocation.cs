using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.BlockStates;

/// <summary>
/// Represents a namespaced Minecraft resource location (<c>namespace:path</c>),
/// preserving modded namespaces and validating allowed identifier characters.
/// </summary>
public sealed class ResourceLocation : IEquatable<ResourceLocation>
{
    public const string DefaultNamespace = "minecraft";

    public string Namespace { get; }
    public string Path { get; }
    public string FullId => $"{Namespace}:{Path}";

    public ResourceLocation(string @namespace, string path)
    {
        if (string.IsNullOrWhiteSpace(@namespace) || !IsValidNamespace(@namespace))
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockState,
                $"Invalid resource location namespace '{@namespace}'.");
        }

        if (string.IsNullOrWhiteSpace(path) || !IsValidPath(path))
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockState,
                $"Invalid resource location path '{path}'.");
        }

        Namespace = @namespace;
        Path = path;
    }

    public static ResourceLocation Parse(string value)
    {
        if (!TryParse(value, out var result) || result is null)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockState,
                $"Malformed resource location '{value}'.");
        }

        return result;
    }

    public static bool TryParse(string? value, out ResourceLocation? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();
        int colonIdx = trimmed.IndexOf(':');

        string ns;
        string path;

        if (colonIdx < 0)
        {
            ns = DefaultNamespace;
            path = trimmed;
        }
        else
        {
            if (colonIdx == 0 || colonIdx == trimmed.Length - 1 || trimmed.IndexOf(':', colonIdx + 1) >= 0)
            {
                return false;
            }

            ns = trimmed[..colonIdx];
            path = trimmed[(colonIdx + 1)..];
        }

        if (!IsValidNamespace(ns) || !IsValidPath(path))
        {
            return false;
        }

        result = new ResourceLocation(ns, path);
        return true;
    }

    private static bool IsValidNamespace(string ns)
    {
        for (int i = 0; i < ns.Length; i++)
        {
            char c = ns[i];
            bool ok = (c >= 'a' && c <= 'z') ||
                      (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') ||
                      c == '_' || c == '-' || c == '.';
            if (!ok) return false;
        }
        return true;
    }

    private static bool IsValidPath(string path)
    {
        for (int i = 0; i < path.Length; i++)
        {
            char c = path[i];
            bool ok = (c >= 'a' && c <= 'z') ||
                      (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') ||
                      c == '_' || c == '-' || c == '.' || c == '/';
            if (!ok) return false;
        }
        return true;
    }

    public bool Equals(ResourceLocation? other) =>
        other is not null &&
        string.Equals(Namespace, other.Namespace, StringComparison.Ordinal) &&
        string.Equals(Path, other.Path, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as ResourceLocation);

    public override int GetHashCode() => HashCode.Combine(Namespace, Path);

    public override string ToString() => FullId;
}
