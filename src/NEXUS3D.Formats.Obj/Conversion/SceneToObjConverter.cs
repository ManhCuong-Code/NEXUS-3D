using System.Diagnostics;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Textures;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Geometry;
using NEXUS3D.Formats.Obj.Materials;

namespace NEXUS3D.Formats.Obj.Conversion;

/// <summary>
/// Contract for converting a Universal Scene Model (<see cref="Scene3D"/>) into Wavefront <see cref="ObjDocument"/> and <see cref="MtlDocument"/>.
/// </summary>
public interface ISceneToObjConverter
{
    (ObjDocument ObjDocument, MtlDocument MtlDocument, ObjConversionReport Report) Convert(
        Scene3D scene,
        ObjMappingOptions? options = null);
}

/// <summary>
/// Converts a Universal Scene Model (<see cref="Scene3D"/>) into a Wavefront <see cref="ObjDocument"/> and companion <see cref="MtlDocument"/>.
/// Preserves independent <c>v</c>/<c>vt</c>/<c>vn</c> indexing, UV seams, hard normal edges, <c>o</c>/<c>g</c>/<c>s</c> groupings,
/// and all material/texture properties.
/// </summary>
public sealed class SceneToObjConverter : ISceneToObjConverter
{
    public (ObjDocument ObjDocument, MtlDocument MtlDocument, ObjConversionReport Report) Convert(
        Scene3D scene,
        ObjMappingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        options ??= ObjMappingOptions.Default;

        var sw = Stopwatch.StartNew();
        var objDoc = new ObjDocument { SourceFileName = scene.Metadata.Source?.OriginalFileName };
        var mtlDoc = new MtlDocument();
        var issues = new List<ValidationIssue>();
        var warnings = new List<WarningRecord>();
        var lossReport = new LossReport();

        ObjPreservedPayload? preserved = options.PreserveRawData
            ? ObjDataPreserver.TryRestore(scene)
            : null;

        if (preserved is not null)
        {
            objDoc.MaterialLibraries.AddRange(preserved.MaterialLibraries);
            objDoc.HeaderComments.AddRange(preserved.ObjHeaderComments);
            mtlDoc.HeaderComments.AddRange(preserved.MtlHeaderComments);
            objDoc.ParameterVertices.AddRange(preserved.ParameterVertices);
            objDoc.Polylines.AddRange(preserved.Polylines);
            objDoc.Points.AddRange(preserved.Points);
            objDoc.UnrecognizedStatements.AddRange(preserved.UnrecognizedStatements);
        }

        // Build texture lookup by Guid
        var textureIdToPath = new Dictionary<Guid, string>();
        for (int i = 0; i < scene.Textures.Count; i++)
        {
            TextureData tex = scene.Textures[i];
            string? relPath = tex.Reference.RelativePath;
            if (string.IsNullOrWhiteSpace(relPath) && tex.Metadata.Tags.TryGetValue("obj.rawPath", out string? rawTag))
            {
                relPath = rawTag;
            }

            if (!string.IsNullOrWhiteSpace(relPath))
            {
                textureIdToPath[tex.Id] = relPath;
            }
        }

        // Convert USM Materials -> MtlDocument
        var materialIdToName = new Dictionary<Guid, string>();
        for (int i = 0; i < scene.Materials.Materials.Count; i++)
        {
            MaterialData usmMat = scene.Materials.Materials[i];
            MtlMaterial mtlMat = MtlParser.FromUsmMaterial(
                usmMat,
                texId => textureIdToPath.GetValueOrDefault(texId));

            mtlDoc.Materials.Add(mtlMat);
            materialIdToName[usmMat.Id] = mtlMat.Name;
        }

        if (mtlDoc.Materials.Count > 0 && objDoc.MaterialLibraries.Count == 0)
        {
            string mtlName = !string.IsNullOrWhiteSpace(options.ExportOptions.MtlFileName)
                ? options.ExportOptions.MtlFileName
                : $"{SanitizeFileStem(scene.Name)}.mtl";
            objDoc.MaterialLibraries.Add(mtlName);
        }

        // Collect all (NodeName, MeshData) pairs from the SceneGraph
        var meshEntries = new List<(string ObjectName, MeshData Mesh)>();
        foreach (SceneNode node in scene.Graph.Traverse())
        {
            if (node.Object is not { Meshes.Count: > 0 } sceneObj)
            {
                continue;
            }

            for (int mIdx = 0; mIdx < sceneObj.Meshes.Count; mIdx++)
            {
                MeshData mesh = sceneObj.Meshes[mIdx];
                string objName = !string.IsNullOrWhiteSpace(mesh.Name) && !string.Equals(mesh.Name, "Mesh", StringComparison.Ordinal)
                    ? mesh.Name
                    : (!string.IsNullOrWhiteSpace(node.Name) && !string.Equals(node.Name, "Root", StringComparison.OrdinalIgnoreCase)
                        ? node.Name
                        : sceneObj.Name);
                meshEntries.Add((objName, mesh));
            }
        }

        bool canUsePreservedSharedPools = CanUsePreservedPools(preserved, meshEntries);

        if (canUsePreservedSharedPools && preserved is not null)
        {
            objDoc.Positions.AddRange(preserved.SharedPositions!);
            objDoc.TextureCoordinates.AddRange(preserved.SharedTextureCoordinates!);
            objDoc.Normals.AddRange(preserved.SharedNormals!);

            foreach (var (objName, mesh) in meshEntries)
            {
                // Synchronize any USM vertex edits back into the shared pools
                for (int fIdx = 0; fIdx < mesh.Faces.Count; fIdx++)
                {
                    Face usmFace = mesh.Faces[fIdx];
                    IReadOnlyList<FaceCorner> corners = usmFace.Corners!;
                    var objCorners = new ObjFaceVertex[corners.Count];

                    for (int cIdx = 0; cIdx < corners.Count; cIdx++)
                    {
                        FaceCorner c = corners[cIdx];
                        int usmVertIdx = usmFace.VertexIndices[cIdx];
                        if (usmVertIdx >= 0 && usmVertIdx < mesh.Vertices.Count)
                        {
                            Vertex v = mesh.Vertices[usmVertIdx];
                            objDoc.Positions[c.PositionIndex] = ExtractObjVertex(v);
                            if (c.TexCoordIndex.HasValue && v.TexCoord.HasValue)
                            {
                                objDoc.TextureCoordinates[c.TexCoordIndex.Value] = ExtractObjTexCoord(v);
                            }

                            if (c.NormalIndex.HasValue && v.Normal.HasValue)
                            {
                                objDoc.Normals[c.NormalIndex.Value] = new ObjNormal(v.Normal.Value.X, v.Normal.Value.Y, v.Normal.Value.Z);
                            }
                        }

                        objCorners[cIdx] = new ObjFaceVertex(c.PositionIndex, c.TexCoordIndex, c.NormalIndex);
                    }

                    AddFaceToObjDocument(objDoc, objName, mesh, fIdx, usmFace, objCorners, materialIdToName);
                }
            }
        }
        else
        {
            // Deduplicate Positions, TextureCoordinates, and Normals across all meshes
            var posPool = new Dictionary<(double X, double Y, double Z, double W), int>();
            var uvPool = new Dictionary<(double U, double V, double W, int Dim), int>();
            var normPool = new Dictionary<(double X, double Y, double Z), int>();

            foreach (var (objName, mesh) in meshEntries)
            {
                if (mesh.Faces.Count == 0 && mesh.Vertices.Count > 0)
                {
                    for (int vIdx = 0; vIdx < mesh.Vertices.Count; vIdx++)
                    {
                        GetOrAddPosition(objDoc, posPool, mesh.Vertices[vIdx]);
                    }

                    continue;
                }

                for (int fIdx = 0; fIdx < mesh.Faces.Count; fIdx++)
                {
                    Face usmFace = mesh.Faces[fIdx];
                    if (usmFace.VertexIndices.Count < 3)
                    {
                        continue;
                    }

                    var objCorners = new ObjFaceVertex[usmFace.VertexIndices.Count];
                    for (int cIdx = 0; cIdx < usmFace.VertexIndices.Count; cIdx++)
                    {
                        int vIdx = usmFace.VertexIndices[cIdx];
                        Vertex v = mesh.Vertices[vIdx];

                        int posIndex = GetOrAddPosition(objDoc, posPool, v);
                        int? uvIndex = v.TexCoord.HasValue ? GetOrAddTexCoord(objDoc, uvPool, v) : null;
                        int? normIndex = v.Normal.HasValue ? GetOrAddNormal(objDoc, normPool, v.Normal.Value) : null;

                        objCorners[cIdx] = new ObjFaceVertex(posIndex, uvIndex, normIndex);
                    }

                    AddFaceToObjDocument(objDoc, objName, mesh, fIdx, usmFace, objCorners, materialIdToName);
                }
            }
        }

        int totalExpandedVertices = meshEntries.Sum(m => m.Mesh.Vertices.Count);
        lossReport.RecordSupported(
            "USM to OBJ/MTL",
            $"Exported {objDoc.Positions.Count} positions, {objDoc.TextureCoordinates.Count} UVs, {objDoc.Normals.Count} normals, {objDoc.Faces.Count} faces, and {mtlDoc.Materials.Count} materials.");

        if (scene.Skeletons.Count > 0)
        {
            lossReport.RecordUnmapped(
                "Skeletons",
                $"Wavefront OBJ does not natively support skeletal hierarchies ({scene.Skeletons.Count} skeleton(s) omitted).");
        }

        if (scene.Animations.Count > 0)
        {
            lossReport.RecordUnmapped(
                "Animations",
                $"Wavefront OBJ does not natively support keyframe animations ({scene.Animations.Count} animation(s) omitted).");
        }

        sw.Stop();
        var report = new ObjConversionReport
        {
            SharedPositionCount = objDoc.Positions.Count,
            SharedTexCoordCount = objDoc.TextureCoordinates.Count,
            SharedNormalCount = objDoc.Normals.Count,
            ParameterVertexCount = objDoc.ParameterVertices.Count,
            ExpandedUsmVertexCount = totalExpandedVertices,
            FaceCount = objDoc.Faces.Count,
            PolylineCount = objDoc.Polylines.Count,
            PointElementCount = objDoc.Points.Count,
            ObjectCount = Math.Max(1, objDoc.Objects.Count),
            GroupCount = objDoc.Groups.Count,
            SmoothingGroupCount = objDoc.SmoothingGroups.Count,
            MaterialCount = mtlDoc.Materials.Count,
            TextureCount = scene.Textures.Count,
            Issues = issues,
            Warnings = warnings,
            LossReport = lossReport,
            Elapsed = sw.Elapsed
        };

        return (objDoc, mtlDoc, report);
    }

    private static bool CanUsePreservedPools(
        ObjPreservedPayload? preserved,
        List<(string ObjectName, MeshData Mesh)> meshEntries)
    {
        if (preserved?.SharedPositions is null ||
            preserved.SharedTextureCoordinates is null ||
            preserved.SharedNormals is null)
        {
            return false;
        }

        int posCount = preserved.SharedPositions.Count;
        int uvCount = preserved.SharedTextureCoordinates.Count;
        int normCount = preserved.SharedNormals.Count;

        foreach (var (_, mesh) in meshEntries)
        {
            for (int fIdx = 0; fIdx < mesh.Faces.Count; fIdx++)
            {
                Face face = mesh.Faces[fIdx];
                if (face.Corners is null || face.Corners.Count != face.VertexIndices.Count)
                {
                    return false;
                }

                for (int cIdx = 0; cIdx < face.Corners.Count; cIdx++)
                {
                    FaceCorner c = face.Corners[cIdx];
                    if (c.PositionIndex < 0 || c.PositionIndex >= posCount)
                    {
                        return false;
                    }

                    if (c.TexCoordIndex.HasValue && (c.TexCoordIndex.Value < 0 || c.TexCoordIndex.Value >= uvCount))
                    {
                        return false;
                    }

                    if (c.NormalIndex.HasValue && (c.NormalIndex.Value < 0 || c.NormalIndex.Value >= normCount))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static void AddFaceToObjDocument(
        ObjDocument objDoc,
        string objName,
        MeshData mesh,
        int faceIndexInMesh,
        Face usmFace,
        ObjFaceVertex[] objCorners,
        Dictionary<Guid, string> materialIdToName)
    {
        IReadOnlyList<string> groups = usmFace.GroupNames;
        if (groups.Count == 0 && mesh.Submeshes.Count > 0)
        {
            for (int sIdx = 0; sIdx < mesh.Submeshes.Count; sIdx++)
            {
                MeshSubmesh sub = mesh.Submeshes[sIdx];
                if (faceIndexInMesh >= sub.FaceStartIndex && faceIndexInMesh < sub.FaceStartIndex + sub.FaceCount)
                {
                    if (!string.Equals(sub.Name, "default", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(sub.Name, "Submesh", StringComparison.OrdinalIgnoreCase))
                    {
                        groups = [sub.Name];
                    }

                    break;
                }
            }
        }

        string? matName = null;
        if (usmFace.MaterialId.HasValue && materialIdToName.TryGetValue(usmFace.MaterialId.Value, out string? resolvedMat))
        {
            matName = resolvedMat;
        }

        int smoothing = usmFace.SmoothingGroup ?? 0;
        string? effectiveObjName = string.Equals(objName, "default", StringComparison.OrdinalIgnoreCase)
            ? null
            : objName;

        var objFace = new ObjFace
        {
            Vertices = objCorners,
            ObjectName = effectiveObjName,
            GroupNames = groups,
            SmoothingGroup = smoothing,
            MaterialName = matName
        };

        int globalFaceIdx = objDoc.Faces.Count;
        objDoc.Faces.Add(objFace);

        string objKey = effectiveObjName ?? "default";
        if (!objDoc.Objects.TryGetValue(objKey, out ObjObject? objEntry))
        {
            objEntry = new ObjObject(objKey);
            objDoc.Objects[objKey] = objEntry;
        }
        objEntry.FaceIndices.Add(globalFaceIdx);

        for (int g = 0; g < groups.Count; g++)
        {
            string gName = groups[g];
            if (!objDoc.Groups.TryGetValue(gName, out ObjGroup? grpEntry))
            {
                grpEntry = new ObjGroup(gName);
                objDoc.Groups[gName] = grpEntry;
            }
            grpEntry.FaceIndices.Add(globalFaceIdx);
        }

        if (usmFace.SmoothingGroup.HasValue)
        {
            if (!objDoc.SmoothingGroups.TryGetValue(smoothing, out ObjSmoothingGroup? sgEntry))
            {
                sgEntry = new ObjSmoothingGroup(smoothing);
                objDoc.SmoothingGroups[smoothing] = sgEntry;
            }
            sgEntry.FaceIndices.Add(globalFaceIdx);
        }
    }

    private static ObjVertex ExtractObjVertex(Vertex v)
    {
        double w = 1.0;
        if (v.CustomAttributes.TryGetValue("obj.w", out Vector4D vec4) && Math.Abs(vec4.W) > MathUtilities.Epsilon)
        {
            w = vec4.W;
        }

        return new ObjVertex(v.Position.X, v.Position.Y, v.Position.Z, w);
    }

    private static ObjTextureCoordinate ExtractObjTexCoord(Vertex v)
    {
        Vector2D uv = v.TexCoord ?? Vector2D.Zero;
        if (v.CustomAttributes.TryGetValue("obj.uvw", out Vector4D uvw))
        {
            int dim = Math.Clamp((int)Math.Round(uvw.W), 1, 3);
            return new ObjTextureCoordinate(uv.X, uv.Y, uvw.Z, dim);
        }

        return new ObjTextureCoordinate(uv.X, uv.Y, 0.0, 2);
    }

    private static int GetOrAddPosition(
        ObjDocument doc,
        Dictionary<(double X, double Y, double Z, double W), int> pool,
        Vertex v)
    {
        ObjVertex objV = ExtractObjVertex(v);
        var key = (objV.X, objV.Y, objV.Z, objV.W);
        if (!pool.TryGetValue(key, out int idx))
        {
            idx = doc.Positions.Count;
            doc.Positions.Add(objV);
            pool[key] = idx;
        }

        return idx;
    }

    private static int GetOrAddTexCoord(
        ObjDocument doc,
        Dictionary<(double U, double V, double W, int Dim), int> pool,
        Vertex v)
    {
        ObjTextureCoordinate vt = ExtractObjTexCoord(v);
        var key = (vt.U, vt.V, vt.W, vt.Dimension);
        if (!pool.TryGetValue(key, out int idx))
        {
            idx = doc.TextureCoordinates.Count;
            doc.TextureCoordinates.Add(vt);
            pool[key] = idx;
        }

        return idx;
    }

    private static int GetOrAddNormal(
        ObjDocument doc,
        Dictionary<(double X, double Y, double Z), int> pool,
        Vector3D n)
    {
        var key = (n.X, n.Y, n.Z);
        if (!pool.TryGetValue(key, out int idx))
        {
            idx = doc.Normals.Count;
            doc.Normals.Add(new ObjNormal(n.X, n.Y, n.Z));
            pool[key] = idx;
        }

        return idx;
    }

    private static string SanitizeFileStem(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "materials";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray();
        string cleaned = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "materials" : cleaned;
    }
}

