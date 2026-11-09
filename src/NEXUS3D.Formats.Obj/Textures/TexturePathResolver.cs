using System.Buffers.Binary;
using NEXUS3D.Domain.Textures;

namespace NEXUS3D.Formats.Obj.Textures;

/// <summary>
/// Resolves, normalizes, and safely copies texture paths referenced by Wavefront MTL files across Windows and Unix path conventions.
/// </summary>
public sealed class TexturePathResolver
{
    /// <summary>
    /// Normalizes a raw texture path from an MTL file into a portable forward-slash relative path
    /// (unless it is a rooted absolute path).
    /// </summary>
    public string NormalizeRelativePath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        string trimmed = rawPath.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1].Trim();
        }

        string normalized = trimmed.Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized;
    }

    /// <summary>
    /// Resolves a raw MTL texture path against the directory containing the <c>.mtl</c> (or <c>.obj</c>) file.
    /// </summary>
    public string? ResolveFullPath(string rawPath, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        string cleaned = rawPath.Trim();
        if (cleaned.Length >= 2 && cleaned[0] == '"' && cleaned[^1] == '"')
        {
            cleaned = cleaned[1..^1].Trim();
        }

        string osPath = cleaned.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        try
        {
            if (Path.IsPathRooted(osPath))
            {
                return Path.GetFullPath(osPath);
            }

            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return null;
            }

            return Path.GetFullPath(Path.Combine(baseDirectory, osPath));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves a complete <see cref="ObjTextureReference"/> descriptor without decoding full image pixel buffers into memory.
    /// </summary>
    public ObjTextureReference ResolveReference(
        string materialName,
        string slotName,
        string mtlKeyword,
        string rawPath,
        string? baseDirectory)
    {
        string normalizedRel = NormalizeRelativePath(rawPath);
        string? resolvedFull = ResolveFullPath(rawPath, baseDirectory);
        TextureFormat format = DetectFormat(normalizedRel);
        TextureColorSpace colorSpace = InferColorSpace(slotName, mtlKeyword);

        bool exists = false;
        long? fileSize = null;
        int width = 16;
        int height = 16;

        if (!string.IsNullOrWhiteSpace(resolvedFull) && File.Exists(resolvedFull))
        {
            exists = true;
            try
            {
                var info = new FileInfo(resolvedFull);
                fileSize = info.Length;
                if (TryReadHeaderDimensions(resolvedFull, format, out int w, out int h))
                {
                    width = w;
                    height = h;
                }
            }
            catch (Exception)
            {
                // Ignore header inspection failure; keep default dimensions
            }
        }

        return new ObjTextureReference
        {
            MaterialName = materialName,
            SlotName = slotName,
            MtlKeyword = mtlKeyword,
            RawPath = rawPath,
            NormalizedRelativePath = normalizedRel,
            ResolvedFullPath = resolvedFull,
            Format = format,
            ColorSpace = colorSpace,
            ExistsOnDisk = exists,
            Width = width,
            Height = height,
            FileSizeBytes = fileSize
        };
    }

    /// <summary>
    /// Detects the <see cref="TextureFormat"/> from a file path extension.
    /// </summary>
    public static TextureFormat DetectFormat(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return TextureFormat.Unknown;
        }

        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => TextureFormat.Png,
            ".jpg" or ".jpeg" => TextureFormat.Jpg,
            ".tga" => TextureFormat.Tga,
            ".bmp" => TextureFormat.Bmp,
            ".webp" => TextureFormat.WebP,
            ".dds" => TextureFormat.Dds,
            _ => TextureFormat.Unknown
        };
    }

    /// <summary>
    /// Infers the appropriate <see cref="TextureColorSpace"/> from the material slot or MTL keyword.
    /// </summary>
    public static TextureColorSpace InferColorSpace(string slotName, string mtlKeyword)
    {
        if (string.Equals(slotName, "Normal", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mtlKeyword, "norm", StringComparison.OrdinalIgnoreCase))
        {
            return TextureColorSpace.NormalMap;
        }

        if (string.Equals(slotName, "Bump", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(slotName, "Displacement", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(slotName, "Shininess", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(slotName, "Opacity", StringComparison.OrdinalIgnoreCase))
        {
            return TextureColorSpace.Linear;
        }

        return TextureColorSpace.Srgb;
    }

    /// <summary>
    /// Safely copies a source texture file into the target export directory, returning the portable relative path
    /// to write into the exported <c>.mtl</c> file.
    /// </summary>
    public async Task<string> CopyTextureForExportAsync(
        string sourceFullPath,
        string preferredRelativePath,
        string targetExportDirectory,
        bool preserveDirectoryStructure = true,
        bool allowOverwrite = false,
        ISet<string>? allocatedRelativePaths = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExportDirectory);

        string normalizedRel = NormalizeRelativePath(preferredRelativePath);
        if (string.IsNullOrWhiteSpace(normalizedRel) || Path.IsPathRooted(normalizedRel) || normalizedRel.StartsWith("../", StringComparison.Ordinal))
        {
            normalizedRel = Path.GetFileName(sourceFullPath);
        }

        if (!preserveDirectoryStructure)
        {
            normalizedRel = Path.GetFileName(normalizedRel);
        }

        // Prevent relative path collisions within the same export batch
        if (allocatedRelativePaths is not null && !allocatedRelativePaths.Add(normalizedRel))
        {
            string dir = Path.GetDirectoryName(normalizedRel)?.Replace('\\', '/') ?? string.Empty;
            string stem = Path.GetFileNameWithoutExtension(normalizedRel);
            string ext = Path.GetExtension(normalizedRel);
            int counter = 2;
            while (true)
            {
                string candidate = string.IsNullOrEmpty(dir)
                    ? $"{stem}_{counter}{ext}"
                    : $"{dir}/{stem}_{counter}{ext}";
                if (allocatedRelativePaths.Add(candidate))
                {
                    normalizedRel = candidate;
                    break;
                }

                counter++;
            }
        }

        string targetFullPath = Path.GetFullPath(
            Path.Combine(targetExportDirectory, normalizedRel.Replace('/', Path.DirectorySeparatorChar)));

        // If source and destination resolve to the exact same physical file, do not copy over itself
        if (string.Equals(Path.GetFullPath(sourceFullPath), targetFullPath, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedRel;
        }

        // If destination exists and overwrite is disabled, pick a non-colliding filename if different, or keep existing
        if (File.Exists(targetFullPath) && !allowOverwrite)
        {
            return normalizedRel;
        }

        string? targetSubDir = Path.GetDirectoryName(targetFullPath);
        if (!string.IsNullOrWhiteSpace(targetSubDir))
        {
            Directory.CreateDirectory(targetSubDir);
        }

        await using var sourceStream = new FileStream(
            sourceFullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 65536,
            useAsync: true);

        await using var destStream = new FileStream(
            targetFullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        await sourceStream.CopyToAsync(destStream, 65536, cancellationToken).ConfigureAwait(false);
        return normalizedRel;
    }

    /// <summary>
    /// Reads image width and height directly from the first 32 bytes of PNG, BMP, or TGA headers without decoding pixel data.
    /// </summary>
    public static bool TryReadHeaderDimensions(string fullPath, TextureFormat format, out int width, out int height)
    {
        width = 16;
        height = 16;

        Span<byte> header = stackalloc byte[32];
        using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        int read = fs.Read(header);
        if (read < 18)
        {
            return false;
        }

        // PNG: 8-byte signature + 4-byte length + "IHDR" + 4-byte BE width + 4-byte BE height (24 bytes total)
        if (format == TextureFormat.Png && read >= 24 &&
            header[0] == 0x89 && header[1] == (byte)'P' && header[2] == (byte)'N' && header[3] == (byte)'G')
        {
            int w = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
            int h = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
            if (w > 0 && h > 0)
            {
                width = w;
                height = h;
                return true;
            }
        }

        // BMP: "BM" + ... + offset 18: 4-byte LE width, offset 22: 4-byte LE height
        if (format == TextureFormat.Bmp && read >= 26 && header[0] == (byte)'B' && header[1] == (byte)'M')
        {
            int w = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(18, 4));
            int h = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(header.Slice(22, 4)));
            if (w > 0 && h > 0)
            {
                width = w;
                height = h;
                return true;
            }
        }

        // TGA: offset 12: 2-byte LE width, offset 14: 2-byte LE height
        if (format == TextureFormat.Tga && read >= 18)
        {
            int w = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(12, 2));
            int h = BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(14, 2));
            if (w > 0 && h > 0)
            {
                width = w;
                height = h;
                return true;
            }
        }

        return false;
    }
}
