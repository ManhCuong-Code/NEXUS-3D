using System.Numerics;
using System.Text.Json;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Rendering.Contracts;
using NEXUS3D.Rendering.Domain;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Adapts format-independent USM MaterialData and Texture references into GPU RenderMaterial,
/// resolving texture paths across multiple formats (OBJ/MTL, Bedrock, VoxelJson, Schematics).
/// </summary>
public sealed class MaterialRenderAdapter : IMaterialRenderAdapter
{
    private readonly ITextureLoader _textureLoader;

    public MaterialRenderAdapter(ITextureLoader textureLoader)
    {
        _textureLoader = textureLoader;
    }

    public RenderMaterial GetDefaultMaterial() => RenderMaterial.Default;

    public RenderMaterial AdaptMaterial(MaterialData material, string? baseDirectory = null)
    {
        if (material is null)
        {
            return GetDefaultMaterial();
        }

        // 1. Resolve Base / Diffuse Color
        var baseColor = new Vector4(
            (float)material.BaseColor.R,
            (float)material.BaseColor.G,
            (float)material.BaseColor.B,
            (float)(material.BaseColor.A * material.Opacity));

        // If MTL Kd is present in CustomProperties, override
        if (material.CustomProperties.TryGetValue("obj.mtl.Kd", out var kdElem) &&
            kdElem.ValueKind == JsonValueKind.Array && kdElem.GetArrayLength() >= 3)
        {
            float r = (float)kdElem[0].GetDouble();
            float g = (float)kdElem[1].GetDouble();
            float b = (float)kdElem[2].GetDouble();
            baseColor = new Vector4(r, g, b, (float)material.Opacity);
        }

        // 2. Resolve Specular & Shininess
        var spec = Vector4.One * 0.2f;
        float shininess = 20f;
        bool isDoubleSided = true;

        if (material.CustomProperties.TryGetValue("obj.mtl.Ks", out var ksElem) &&
            ksElem.ValueKind == JsonValueKind.Array && ksElem.GetArrayLength() >= 3)
        {
            float sr = (float)ksElem[0].GetDouble();
            float sg = (float)ksElem[1].GetDouble();
            float sb = (float)ksElem[2].GetDouble();
            spec = new Vector4(sr, sg, sb, 1.0f);
        }

        if (material.CustomProperties.TryGetValue("obj.mtl.Ns", out var nsElem) && nsElem.ValueKind == JsonValueKind.Number)
        {
            shininess = Math.Clamp((float)nsElem.GetDouble(), 1f, 1000f);
        }
        else if (material.CustomProperties.TryGetValue("shininess", out var shineElem) && shineElem.ValueKind == JsonValueKind.Number)
        {
            shininess = (float)shineElem.GetDouble();
        }

        if (material.CustomProperties.TryGetValue("isDoubleSided", out var sideElem) && sideElem.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            isDoubleSided = sideElem.GetBoolean();
        }

        // 3. Resolve Texture Paths
        string? diffusePath = null;
        string? normalPath = null;
        string? emissivePath = null;

        // Check Diffuse / Albedo / map_Kd slots
        if (material.TextureSlots.TryGetValue("Albedo", out var albedoslot) ||
            material.TextureSlots.TryGetValue("Diffuse", out albedoslot) ||
            material.TextureSlots.TryGetValue("map_Kd", out albedoslot))
        {
            if (!string.IsNullOrWhiteSpace(albedoslot.Channel) && albedoslot.Channel.IndexOfAny(Path.GetInvalidPathChars()) < 0)
            {
                diffusePath = albedoslot.Channel;
            }
        }

        if (material.TextureSlots.TryGetValue("Normal", out var normSlot) ||
            material.TextureSlots.TryGetValue("Bump", out normSlot) ||
            material.TextureSlots.TryGetValue("map_Bump", out normSlot))
        {
            if (!string.IsNullOrWhiteSpace(normSlot.Channel) && normSlot.Channel.IndexOfAny(Path.GetInvalidPathChars()) < 0)
            {
                normalPath = normSlot.Channel;
            }
        }

        if (material.TextureSlots.TryGetValue("Emissive", out var emSlot))
        {
            if (!string.IsNullOrWhiteSpace(emSlot.Channel) && emSlot.Channel.IndexOfAny(Path.GetInvalidPathChars()) < 0)
            {
                emissivePath = emSlot.Channel;
            }
        }

        // Check CustomProperties
        if (string.IsNullOrEmpty(diffusePath))
        {
            if (material.CustomProperties.TryGetValue("obj.mtl.map_Kd", out var mapKdElem))
            {
                diffusePath = mapKdElem.GetString();
            }
            else if (material.CustomProperties.TryGetValue("diffuseTexture", out var dtElem))
            {
                diffusePath = dtElem.GetString();
            }
            else if (material.CustomProperties.TryGetValue("voxel.textureReference", out var voxTexElem))
            {
                diffusePath = voxTexElem.GetString();
            }
        }

        // Fallback: Check if there's an adjacent or companion texture file in baseDirectory
        if (string.IsNullOrEmpty(diffusePath) && !string.IsNullOrEmpty(baseDirectory) && Directory.Exists(baseDirectory))
        {
            var searchDirectories = new List<string>
            {
                baseDirectory,
                Path.Combine(baseDirectory, "textures", "entity"),
                Path.Combine(baseDirectory, "textures"),
                Path.Combine(baseDirectory, "textures", "models"),
                Path.Combine(baseDirectory, "texture")
            };

            var parent = Directory.GetParent(baseDirectory)?.FullName;
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
            {
                searchDirectories.Add(Path.Combine(parent, "textures", "entity"));
                searchDirectories.Add(Path.Combine(parent, "textures"));
                searchDirectories.Add(parent);
            }

            var nameCandidates = new List<string>();

            // 1. Raw and clean material name
            if (!string.IsNullOrWhiteSpace(material.Name))
            {
                nameCandidates.Add(material.Name);

                string cleanMat = material.Name;
                if (cleanMat.StartsWith("geometry.", StringComparison.OrdinalIgnoreCase))
                {
                    cleanMat = cleanMat[9..];
                }
                if (cleanMat.EndsWith("_material", StringComparison.OrdinalIgnoreCase))
                {
                    cleanMat = cleanMat[..^9];
                }

                if (!nameCandidates.Contains(cleanMat, StringComparer.OrdinalIgnoreCase))
                {
                    nameCandidates.Add(cleanMat);
                }

                // Sub-segments for dot notation (e.g. humanoid.custom -> custom, humanoid)
                int dotIdx = cleanMat.LastIndexOf('.');
                if (dotIdx >= 0)
                {
                    string suffix = cleanMat[(dotIdx + 1)..];
                    string prefix = cleanMat[..dotIdx];
                    if (!string.IsNullOrEmpty(suffix) && !nameCandidates.Contains(suffix, StringComparer.OrdinalIgnoreCase))
                    {
                        nameCandidates.Add(suffix);
                    }
                    if (!string.IsNullOrEmpty(prefix) && !nameCandidates.Contains(prefix, StringComparer.OrdinalIgnoreCase))
                    {
                        nameCandidates.Add(prefix);
                    }
                }
            }

            // 2. Identifier / model name in CustomProperties
            foreach (var key in new[] { "modelName", "identifier", "bedrock.geometryId", "geometryIdentifier", "bedrock.material" })
            {
                if (material.CustomProperties.TryGetValue(key, out var propElem) && propElem.ValueKind == JsonValueKind.String)
                {
                    var val = propElem.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        if (!nameCandidates.Contains(val, StringComparer.OrdinalIgnoreCase))
                        {
                            nameCandidates.Add(val);
                        }
                        if (val.StartsWith("geometry.", StringComparison.OrdinalIgnoreCase))
                        {
                            var stripped = val[9..];
                            if (!nameCandidates.Contains(stripped, StringComparer.OrdinalIgnoreCase))
                            {
                                nameCandidates.Add(stripped);
                            }
                        }
                    }
                }
            }

            // 3. Standard companion skin texture filenames
            foreach (var standard in new[] { "skin", "steve", "alex", "default", "texture" })
            {
                if (!nameCandidates.Contains(standard, StringComparer.OrdinalIgnoreCase))
                {
                    nameCandidates.Add(standard);
                }
            }

            // A. Search for named candidates across search directories
            foreach (var dir in searchDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var name in nameCandidates)
                {
                    string cand = Path.Combine(dir, $"{name}.png");
                    if (File.Exists(cand))
                    {
                        diffusePath = cand;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(diffusePath)) break;
            }

            // B. If still not found, check if there is an unambiguous single PNG in search directories
            if (string.IsNullOrEmpty(diffusePath))
            {
                foreach (var dir in searchDirectories)
                {
                    if (!Directory.Exists(dir)) continue;
                    try
                    {
                        var pngs = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
                        if (pngs.Length == 1)
                        {
                            diffusePath = pngs[0];
                            break;
                        }
                    }
                    catch { }
                }
            }
        }

        // Resolve paths safely
        diffusePath = _textureLoader.ResolveTexturePath(diffusePath ?? string.Empty, baseDirectory);
        normalPath = _textureLoader.ResolveTexturePath(normalPath ?? string.Empty, baseDirectory);
        emissivePath = _textureLoader.ResolveTexturePath(emissivePath ?? string.Empty, baseDirectory);

        return new RenderMaterial(
            Id: material.Id.ToString(),
            Name: material.Name,
            BaseColor: baseColor,
            SpecularColor: spec,
            Shininess: shininess,
            Opacity: (float)material.Opacity,
            IsDoubleSided: isDoubleSided,
            DiffuseTexturePath: diffusePath,
            NormalTexturePath: normalPath,
            EmissiveTexturePath: emissivePath);
    }

    /// <summary>
    /// Generates a standardized UV sphere geometry for material swatches and preview inspector.
    /// </summary>
    public RenderMesh CreatePreviewSphere(RenderMaterial material, int segments = 16, float radius = 1.0f)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var indices = new List<int>();

        for (int lat = 0; lat <= segments; lat++)
        {
            float theta = lat * (float)Math.PI / segments;
            float sinTheta = (float)Math.Sin(theta);
            float cosTheta = (float)Math.Cos(theta);

            for (int lon = 0; lon <= segments; lon++)
            {
                float phi = lon * 2f * (float)Math.PI / segments;
                float sinPhi = (float)Math.Sin(phi);
                float cosPhi = (float)Math.Cos(phi);

                var n = new Vector3(cosPhi * sinTheta, cosTheta, sinPhi * sinTheta);
                positions.Add(n * radius);
                normals.Add(n);
                uvs.Add(new Vector2((float)lon / segments, (float)lat / segments));
            }
        }

        for (int lat = 0; lat < segments; lat++)
        {
            for (int lon = 0; lon < segments; lon++)
            {
                int first = (lat * (segments + 1)) + lon;
                int second = first + segments + 1;

                indices.Add(first);
                indices.Add(second);
                indices.Add(first + 1);

                indices.Add(second);
                indices.Add(second + 1);
                indices.Add(first + 1);
            }
        }

        return new RenderMesh
        {
            Name = $"{material.Name}_SpherePreview",
            Positions = [.. positions],
            Normals = [.. normals],
            UVs = [.. uvs],
            Indices = [.. indices],
            MaterialId = material.Id,
            BoundsMin = -Vector3.One * radius,
            BoundsMax = Vector3.One * radius
        };
    }
}
