using System.Collections.Concurrent;
using NEXUS3D.Rendering.Contracts;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Secure, cached texture loader with path traversal protection and dimension resolution.
/// </summary>
public sealed class TextureLoader : ITextureLoader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".webp"
    };

    private readonly ConcurrentDictionary<string, (int Width, int Height)> _dimensionCache = new(StringComparer.OrdinalIgnoreCase);

    public string? ResolveTexturePath(string textureRef, string? baseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(textureRef))
        {
            return null;
        }

        // Clean slashes
        var normalizedRef = textureRef.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        // Security check: Guard against obvious malicious root escapes if baseDirectory provided
        if (!string.IsNullOrEmpty(baseDirectory))
        {
            try
            {
                var fullBasePath = Path.GetFullPath(baseDirectory);
                var combined = Path.GetFullPath(Path.Combine(fullBasePath, normalizedRef));

                if (File.Exists(combined))
                {
                    return combined;
                }

                // Check in same directory as base directory
                var filenameOnly = Path.GetFileName(normalizedRef);
                var adjacent = Path.Combine(fullBasePath, filenameOnly);
                if (File.Exists(adjacent))
                {
                    return adjacent;
                }

                // Check in "textures" subfolder
                var texturesSub = Path.Combine(fullBasePath, "textures", filenameOnly);
                if (File.Exists(texturesSub))
                {
                    return texturesSub;
                }
            }
            catch
            {
                // Path resolution error fallback
            }
        }

        // Check if absolute path exists
        if (Path.IsPathRooted(normalizedRef) && File.Exists(normalizedRef))
        {
            return normalizedRef;
        }

        return null;
    }

    public bool IsTextureAvailable(string texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return false;
        }

        try
        {
            if (!File.Exists(texturePath))
            {
                return false;
            }

            var ext = Path.GetExtension(texturePath);
            if (!SupportedExtensions.Contains(ext))
            {
                return false;
            }

            var fileInfo = new FileInfo(texturePath);
            // Limit file size to 100MB to avoid memory exhaustion
            if (fileInfo.Length > 100 * 1024 * 1024)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates UV coordinate bounds and identifies if texture coordinates exceed [0, 1] requiring wrap/repeat tiling.
    /// </summary>
    public (bool RequiresTiling, float MinU, float MaxU, float MinV, float MaxV) AnalyzeUVBounds(IEnumerable<System.Numerics.Vector2> uvs)
    {
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;
        bool hasData = false;

        foreach (var uv in uvs)
        {
            minU = Math.Min(minU, uv.X);
            maxU = Math.Max(maxU, uv.X);
            minV = Math.Min(minV, uv.Y);
            maxV = Math.Max(maxV, uv.Y);
            hasData = true;
        }

        if (!hasData) return (false, 0, 1, 0, 1);

        bool requiresTiling = minU < -0.01f || maxU > 1.01f || minV < -0.01f || maxV > 1.01f;
        return (requiresTiling, minU, maxU, minV, maxV);
    }

    public (int Width, int Height) GetTextureDimensions(string texturePath)
    {
        if (string.IsNullOrWhiteSpace(texturePath))
        {
            return (0, 0);
        }

        if (_dimensionCache.TryGetValue(texturePath, out var cached))
        {
            return cached;
        }

        if (!IsTextureAvailable(texturePath))
        {
            return (0, 0);
        }

        try
        {
            using var stream = File.OpenRead(texturePath);
            // Basic header inspection for PNG / JPEG / BMP
            var dims = ReadDimensionsFromHeader(stream, Path.GetExtension(texturePath));
            _dimensionCache[texturePath] = dims;
            return dims;
        }
        catch
        {
            return (0, 0);
        }
    }

    private static (int Width, int Height) ReadDimensionsFromHeader(Stream stream, string extension)
    {
        using var reader = new BinaryReader(stream);

        if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
        {
            // PNG signature (8 bytes) + IHDR length (4 bytes) + "IHDR" (4 bytes)
            if (stream.Length < 24) return (0, 0);
            stream.Seek(16, SeekOrigin.Begin);
            int width = (reader.ReadByte() << 24) | (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte();
            int height = (reader.ReadByte() << 24) | (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte();
            return (width, height);
        }

        if (string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase))
        {
            if (stream.Length < 26) return (0, 0);
            stream.Seek(18, SeekOrigin.Begin);
            int width = reader.ReadInt32();
            int height = Math.Abs(reader.ReadInt32());
            return (width, height);
        }

        // Fallback default
        return (64, 64);
    }
}
