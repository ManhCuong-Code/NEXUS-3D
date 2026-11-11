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

        // Check if already an existing absolute path
        if (Path.IsPathRooted(normalizedRef) && File.Exists(normalizedRef))
        {
            return normalizedRef;
        }

        // Resolve against baseDirectory
        if (!string.IsNullOrEmpty(baseDirectory))
        {
            try
            {
                var fullBasePath = Path.GetFullPath(baseDirectory);
                var combined = Path.GetFullPath(Path.Combine(fullBasePath, normalizedRef));

                // Security check: Guard against malicious path traversal escaping baseDirectory
                if (!combined.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

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

                // Check in "texture" subfolder
                var textureSub = Path.Combine(fullBasePath, "texture", filenameOnly);
                if (File.Exists(textureSub))
                {
                    return textureSub;
                }
            }
            catch
            {
                // Path resolution error fallback
            }
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
        if (!IsTextureAvailable(texturePath))
        {
            return (0, 0);
        }

        return _dimensionCache.GetOrAdd(texturePath, path =>
        {
            try
            {
                // Fast header inspection without full decode
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var ext = Path.GetExtension(path).ToLowerInvariant();

                return ext switch
                {
                    ".png" => ReadPngDimensions(fs),
                    ".jpg" or ".jpeg" => ReadJpegDimensions(fs),
                    _ => (16, 16)
                };
            }
            catch
            {
                return (16, 16);
            }
        });
    }

    private static (int Width, int Height) ReadPngDimensions(Stream stream)
    {
        var buffer = new byte[24];
        if (stream.Read(buffer, 0, 24) == 24)
        {
            // PNG signature check
            if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
            {
                int width = (buffer[16] << 24) | (buffer[17] << 16) | (buffer[18] << 8) | buffer[19];
                int height = (buffer[20] << 24) | (buffer[21] << 16) | (buffer[22] << 8) | buffer[23];
                return (Math.Max(1, width), Math.Max(1, height));
            }
        }
        return (16, 16);
    }

    private static (int Width, int Height) ReadJpegDimensions(Stream stream)
    {
        using var reader = new BinaryReader(stream);
        if (reader.ReadByte() != 0xFF || reader.ReadByte() != 0xD8)
        {
            return (16, 16);
        }

        while (stream.Position < stream.Length)
        {
            byte marker = reader.ReadByte();
            while (marker != 0xFF)
            {
                marker = reader.ReadByte();
            }

            marker = reader.ReadByte();
            while (marker == 0xFF)
            {
                marker = reader.ReadByte();
            }

            if (marker is 0xC0 or 0xC1 or 0xC2) // SOF0, SOF1, SOF2 markers
            {
                reader.ReadInt16(); // skip length
                reader.ReadByte();  // skip precision
                int height = (reader.ReadByte() << 8) | reader.ReadByte();
                int width = (reader.ReadByte() << 8) | reader.ReadByte();
                return (Math.Max(1, width), Math.Max(1, height));
            }

            if (marker is 0xD9 or 0xDA) // EOI or SOS
            {
                break;
            }

            // Skip block
            ushort length = (ushort)((reader.ReadByte() << 8) | reader.ReadByte());
            if (length > 2)
            {
                stream.Seek(length - 2, SeekOrigin.Current);
            }
        }

        return (16, 16);
    }
}
