using System.Diagnostics;
using NEXUS3D.Domain.Coordinates;
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
using NEXUS3D.Formats.Obj.Textures;
using NEXUS3D.Formats.Obj.Validation;

namespace NEXUS3D.Formats.Obj.Conversion;

/// <summary>
/// Contract for converting a Wavefront <see cref="ObjDocument"/> (and optional <see cref="MtlDocument"/>)
/// into the Universal Scene Model (<see cref="Scene3D"/>).
/// </summary>
public interface IObjToSceneConverter
{
    (Scene3D Scene, ObjConversionReport Report) Convert(
        ObjDocument objDocument,
        MtlDocument? mtlDocument = null,
        ObjMappingOptions? options = null);
}

/// <summary>
/// Converts Wavefront OBJ and MTL documents into the Universal Scene Model (<see cref="Scene3D"/>).
/// Strictly preserves UV seams, hard normal edges, polygon topology, object/group hierarchy, smoothing groups,
/// materials, texture references, and auxiliary OBJ data.
/// </summary>
public sealed class ObjToSceneConverter : IObjToSceneConverter
{
    private readonly ObjValidator _validator;
    private readonly TexturePathResolver _texturePathResolver;
    private readonly TextureValidationService _textureValidator;

    public ObjToSceneConverter(
        ObjValidator? validator = null,
        TexturePathResolver? texturePathResolver = null,
        TextureValidationService? textureValidator = null)
    {
        _validator = validator ?? new ObjValidator();
        _texturePathResolver = texturePathResolver ?? new TexturePathResolver();
        _textureValidator = textureValidator ?? new TextureValidationService(_texturePathResolver);
    }

    public (Scene3D Scene, ObjConversionReport Report) Convert(
        ObjDocument objDocument,
        MtlDocument? mtlDocument = null,
        ObjMappingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(objDocument);
        options ??= ObjMappingOptions.Default;

        var sw = Stopwatch.StartNew();
        var issues = new List<ValidationIssue>();
        var warnings = new List<WarningRecord>();
        var lossReport = new LossReport();

        // 1. Validate OBJ + MTL documents
        ValidationResult validation = _validator.ValidateDocument(
            objDocument,
            mtlDocument,
            options.BaseDirectory,
            options.StrictMode);

        issues.AddRange(validation.Issues);
        foreach (ValidationIssue issue in validation.Issues)
        {
            if (options.StrictMode && issue.Severity >= ValidationSeverity.Error)
            {
                throw new ObjException(issue.Code, issue.Message);
            }

            if (issue.Severity == ValidationSeverity.Warning)
            {
                warnings.Add(new WarningRecord
                {
                    Code = issue.Code,
                    Message = issue.Message,
                    SourceComponent = issue.PropertyPath ?? "NEXUS3D.Formats.Obj"
                });
            }
        }

        string sceneName = !string.IsNullOrWhiteSpace(options.SourceFileName)
            ? Path.GetFileNameWithoutExtension(options.SourceFileName)
            : (!string.IsNullOrWhiteSpace(objDocument.SourceFileName)
                ? Path.GetFileNameWithoutExtension(objDocument.SourceFileName)
                : "OBJ Scene");

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            sceneName = "OBJ Scene";
        }

        var scene = new Scene3D
        {
            Name = sceneName,
            Coordinates = new CoordinateMetadata
            {
                CurrentSystem = CoordinateSystem.WavefrontObjDefault,
                SourceSystem = CoordinateSystem.WavefrontObjDefault
            }
        };

        scene.Metadata.Source = new SourceMetadata
        {
            OriginalFileName = options.SourceFileName ?? objDocument.SourceFileName,
            OriginalExtension = ".obj",
            OriginalFormatId = "wavefront:obj",
            OriginalFormatVersion = "1.0",
            FileSizeBytes = options.SourceFileSizeBytes,
            OriginalCoordinateSystem = CoordinateSystem.WavefrontObjDefault
        };
        scene.Metadata.ProvenanceHistory.Add(new DataProvenance(
            DataOriginKind.Imported,
            "NEXUS3D.Formats.Obj",
            "ObjToSceneConverter.Convert",
            $"Imported OBJ scene '{sceneName}'"));

        // 2. Map MTL Materials & Textures into USM MaterialLibrary and Scene.Textures
        var materialNameToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var texturePathToData = new Dictionary<string, TextureData>(StringComparer.OrdinalIgnoreCase);

        if (mtlDocument is not null)
        {
            string? effectiveMtlBaseDir = options.BaseDirectory;
            if (string.IsNullOrWhiteSpace(effectiveMtlBaseDir) && !string.IsNullOrWhiteSpace(mtlDocument.SourcePath))
            {
                effectiveMtlBaseDir = Path.GetDirectoryName(mtlDocument.SourcePath);
            }

            foreach (MtlMaterial mtlMat in mtlDocument.Materials)
            {
                if (materialNameToId.ContainsKey(mtlMat.Name))
                {
                    continue;
                }

                MaterialData usmMaterial = MtlParser.ToUsmMaterial(mtlMat, (statement, slotName) =>
                {
                    ObjTextureReference resolved = _texturePathResolver.ResolveReference(
                        mtlMat.Name,
                        slotName,
                        statement.Keyword,
                        statement.TexturePath,
                        effectiveMtlBaseDir);

                    string key = resolved.NormalizedRelativePath;
                    if (!texturePathToData.TryGetValue(key, out TextureData? texData))
                    {
                        texData = resolved.ToUsmTextureData();
                        texturePathToData[key] = texData;
                        scene.Textures.Add(texData);
                    }

                    return new TextureSlot(texData.Id, statement.Channel);
                });

                scene.Materials.Add(usmMaterial);
                materialNameToId[mtlMat.Name] = usmMaterial.Id;
            }
        }

        // Ensure any material referenced by 'usemtl' in OBJ faces has a corresponding USM MaterialData
        for (int i = 0; i < objDocument.Faces.Count; i++)
        {
            string? matName = objDocument.Faces[i].MaterialName;
            if (!string.IsNullOrWhiteSpace(matName) && !materialNameToId.ContainsKey(matName))
            {
                if (mtlDocument is null && objDocument.MaterialLibraries.Count == 0)
                {
                    var issue = new ValidationIssue(
                        ObjErrorCodes.MissingMaterial,
                        $"Face references material '{matName}' via 'usemtl', but no MTL library was declared.",
                        options.StrictMode ? ValidationSeverity.Error : ValidationSeverity.Warning,
                        $"usemtl:{matName}");
                    issues.Add(issue);
                    if (options.StrictMode)
                    {
                        throw new ObjException(issue.Code, issue.Message);
                    }
                }

                var fallbackMaterial = new MaterialData(matName);
                scene.Materials.Add(fallbackMaterial);
                materialNameToId[matName] = fallbackMaterial.Id;
            }
        }

        // 3. Partition faces by ObjectName (preserving document order)
        var objectOrder = new List<string>();
        var facesByObject = new Dictionary<string, List<ObjFace>>(StringComparer.Ordinal);

        for (int i = 0; i < objDocument.Faces.Count; i++)
        {
            ObjFace face = objDocument.Faces[i];
            string objName = string.IsNullOrWhiteSpace(face.ObjectName) ? "default" : face.ObjectName!;
            if (!facesByObject.TryGetValue(objName, out List<ObjFace>? list))
            {
                list = [];
                facesByObject[objName] = list;
                objectOrder.Add(objName);
            }

            list.Add(face);
        }

        int totalExpandedVertices = 0;
        int totalConvertedFaces = 0;

        if (objectOrder.Count == 0 && objDocument.Positions.Count > 0)
        {
            // Vertex-only / point-cloud / polyline-only OBJ document
            string defaultObjName = objDocument.Objects.Keys.FirstOrDefault() ?? sceneName;
            var mesh = new MeshData(defaultObjName, MeshTopology.Points);
            for (int i = 0; i < objDocument.Positions.Count; i++)
            {
                ObjVertex pos = objDocument.Positions[i];
                if (!pos.IsFinite)
                {
                    continue;
                }

                var v = new Vertex(pos.ToRawVector3D());
                if (Math.Abs(pos.W - 1.0) > MathUtilities.Epsilon)
                {
                    v.CustomAttributes["obj.w"] = pos.ToVector4D();
                }

                mesh.Vertices.Add(v);
            }

            totalExpandedVertices += mesh.Vertices.Count;
            var sceneObj = new SceneObject(defaultObjName);
            sceneObj.Meshes.Add(mesh);
            scene.Graph.AddNode(scene.Graph.Root, new SceneNode(defaultObjName, sceneObj));
        }
        else
        {
            foreach (string objName in objectOrder)
            {
                List<ObjFace> objectFaces = facesByObject[objName];
                MeshTopology topology = options.TriangulatePolygons ? MeshTopology.Triangles : MeshTopology.Polygons;
                var mesh = new MeshData(objName, topology);

                // Deduplicate (PositionIndex, TextureCoordIndex, NormalIndex) within this MeshData
                // so UV seams and hard normal edges are NEVER merged!
                var cornerToUsmVertexIndex = new Dictionary<ObjFaceVertex, int>();

                for (int fIdx = 0; fIdx < objectFaces.Count; fIdx++)
                {
                    ObjFace srcFace = objectFaces[fIdx];

                    // Validate indices before dereferencing
                    if (!AreFaceIndicesValid(srcFace, objDocument))
                    {
                        if (options.StrictMode)
                        {
                            throw new ObjException(
                                ObjErrorCodes.IndexOutOfRange,
                                $"Face at line {srcFace.SourceLineNumber} has out-of-range indices.",
                                srcFace.SourceLineNumber);
                        }

                        continue;
                    }

                    IReadOnlyList<ObjFace> targetFaces = options.TriangulatePolygons && srcFace.Vertices.Count > 3
                        ? srcFace.Triangulate(objDocument.Positions)
                        : [srcFace];

                    for (int tIdx = 0; tIdx < targetFaces.Count; tIdx++)
                    {
                        ObjFace poly = targetFaces[tIdx];
                        if (poly.Vertices.Count < 3)
                        {
                            continue;
                        }

                        // Skip collinear / duplicate-position degenerate faces in non-strict mode (already reported in issues)
                        if (HasDuplicatePositions(poly) || poly.ComputeFaceNormal(objDocument.Positions).LengthSquared() <= MathUtilities.Epsilon)
                        {
                            if (options.StrictMode)
                            {
                                throw new ObjException(
                                    ObjErrorCodes.DegeneratePolygon,
                                    $"Degenerate polygon face at line {poly.SourceLineNumber}.",
                                    poly.SourceLineNumber);
                            }

                            continue;
                        }

                        var usmIndices = new int[poly.Vertices.Count];
                        var corners = new FaceCorner[poly.Vertices.Count];

                        for (int cIdx = 0; cIdx < poly.Vertices.Count; cIdx++)
                        {
                            ObjFaceVertex corner = poly.Vertices[cIdx];
                            corners[cIdx] = new FaceCorner(
                                corner.PositionIndex,
                                corner.TextureCoordIndex,
                                corner.NormalIndex);

                            if (!cornerToUsmVertexIndex.TryGetValue(corner, out int usmVertIdx))
                            {
                                usmVertIdx = CreateUsmVertex(mesh, corner, objDocument);
                                cornerToUsmVertexIndex[corner] = usmVertIdx;
                            }

                            usmIndices[cIdx] = usmVertIdx;
                        }

                        Guid? materialId = null;
                        if (!string.IsNullOrWhiteSpace(poly.MaterialName) &&
                            materialNameToId.TryGetValue(poly.MaterialName, out Guid resolvedMatId))
                        {
                            materialId = resolvedMatId;
                        }

                        var usmFace = new Face(
                            usmIndices,
                            materialId,
                            poly.SmoothingGroup > 0 ? poly.SmoothingGroup : null)
                        {
                            Corners = corners,
                            GroupNames = poly.GroupNames.Count > 0 ? [.. poly.GroupNames] : []
                        };

                        mesh.Faces.Add(usmFace);
                        totalConvertedFaces++;
                    }
                }

                BuildSubmeshes(mesh);
                totalExpandedVertices += mesh.Vertices.Count;

                var sceneObj = new SceneObject(objName);
                sceneObj.Meshes.Add(mesh);
                scene.Graph.AddNode(scene.Graph.Root, new SceneNode(objName, sceneObj));
            }
        }

        // 4. Record fidelity items and preserve raw auxiliary OBJ data
        lossReport.RecordSupported(
            "Geometry (v, vt, vn, f)",
            $"Mapped {objDocument.Positions.Count} positions, {objDocument.TextureCoordinates.Count} UVs, {objDocument.Normals.Count} normals, and {totalConvertedFaces} faces.");

        if (scene.Materials.Count > 0)
        {
            lossReport.RecordSupported(
                "Materials & Textures (MTL)",
                $"Mapped {scene.Materials.Count} materials and {scene.Textures.Count} texture references.");
        }

        if (options.PreserveRawData)
        {
            ObjDataPreserver.Preserve(scene, objDocument, mtlDocument, lossReport);
        }

        sw.Stop();
        var report = new ObjConversionReport
        {
            SharedPositionCount = objDocument.Positions.Count,
            SharedTexCoordCount = objDocument.TextureCoordinates.Count,
            SharedNormalCount = objDocument.Normals.Count,
            ParameterVertexCount = objDocument.ParameterVertices.Count,
            ExpandedUsmVertexCount = totalExpandedVertices,
            FaceCount = totalConvertedFaces,
            PolylineCount = objDocument.Polylines.Count,
            PointElementCount = objDocument.Points.Count,
            ObjectCount = Math.Max(1, objectOrder.Count),
            GroupCount = objDocument.Groups.Count,
            SmoothingGroupCount = objDocument.SmoothingGroups.Count,
            MaterialCount = scene.Materials.Count,
            TextureCount = scene.Textures.Count,
            Issues = issues,
            Warnings = warnings,
            LossReport = lossReport,
            Elapsed = sw.Elapsed
        };

        return (scene, report);
    }

    private static int CreateUsmVertex(MeshData mesh, ObjFaceVertex corner, ObjDocument doc)
    {
        ObjVertex objPos = doc.Positions[corner.PositionIndex];
        Vector3D position = objPos.ToRawVector3D();

        Vector2D? texCoord = null;
        ObjTextureCoordinate? rawUv = null;
        if (corner.TextureCoordIndex.HasValue)
        {
            ObjTextureCoordinate vt = doc.TextureCoordinates[corner.TextureCoordIndex.Value];
            if (vt.IsFinite)
            {
                texCoord = vt.ToVector2D();
                rawUv = vt;
            }
        }

        Vector3D? normal = null;
        if (corner.NormalIndex.HasValue)
        {
            ObjNormal vn = doc.Normals[corner.NormalIndex.Value];
            if (vn.IsFinite && !vn.IsZero)
            {
                normal = vn.ToVector3D();
            }
        }

        var vertex = new Vertex(position, normal, texCoord);
        if (Math.Abs(objPos.W - 1.0) > MathUtilities.Epsilon)
        {
            vertex.CustomAttributes["obj.w"] = objPos.ToVector4D();
        }

        if (rawUv.HasValue && (rawUv.Value.Dimension != 2 || Math.Abs(rawUv.Value.W) > MathUtilities.Epsilon))
        {
            vertex.CustomAttributes["obj.uvw"] = new Vector4D(
                rawUv.Value.U,
                rawUv.Value.V,
                rawUv.Value.W,
                rawUv.Value.Dimension);
        }

        int index = mesh.Vertices.Count;
        mesh.Vertices.Add(vertex);
        return index;
    }

    private static void BuildSubmeshes(MeshData mesh)
    {
        mesh.Submeshes.Clear();
        if (mesh.Faces.Count == 0)
        {
            return;
        }

        int rangeStart = 0;
        string currentGroup = GetPrimaryGroupName(mesh.Faces[0]);
        Guid? currentMaterial = mesh.Faces[0].MaterialId;

        for (int i = 1; i < mesh.Faces.Count; i++)
        {
            string nextGroup = GetPrimaryGroupName(mesh.Faces[i]);
            Guid? nextMaterial = mesh.Faces[i].MaterialId;

            if (!string.Equals(currentGroup, nextGroup, StringComparison.Ordinal) ||
                currentMaterial != nextMaterial)
            {
                mesh.Submeshes.Add(new MeshSubmesh(currentGroup, rangeStart, i - rangeStart, currentMaterial));
                rangeStart = i;
                currentGroup = nextGroup;
                currentMaterial = nextMaterial;
            }
        }

        mesh.Submeshes.Add(new MeshSubmesh(currentGroup, rangeStart, mesh.Faces.Count - rangeStart, currentMaterial));
    }

    private static string GetPrimaryGroupName(Face face) =>
        face.GroupNames.Count > 0 && !string.IsNullOrWhiteSpace(face.GroupNames[0])
            ? face.GroupNames[0]
            : "default";

    private static bool AreFaceIndicesValid(ObjFace face, ObjDocument doc)
    {
        for (int i = 0; i < face.Vertices.Count; i++)
        {
            ObjFaceVertex c = face.Vertices[i];
            if (c.PositionIndex < 0 || c.PositionIndex >= doc.Positions.Count)
            {
                return false;
            }

            if (c.TextureCoordIndex.HasValue &&
                (c.TextureCoordIndex.Value < 0 || c.TextureCoordIndex.Value >= doc.TextureCoordinates.Count))
            {
                return false;
            }

            if (c.NormalIndex.HasValue &&
                (c.NormalIndex.Value < 0 || c.NormalIndex.Value >= doc.Normals.Count))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasDuplicatePositions(ObjFace face)
    {
        for (int i = 0; i < face.Vertices.Count; i++)
        {
            for (int j = i + 1; j < face.Vertices.Count; j++)
            {
                if (face.Vertices[i].PositionIndex == face.Vertices[j].PositionIndex)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
