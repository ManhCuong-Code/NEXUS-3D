using System.Text.Json;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.BedrockGeometry.Exporting;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;
using NEXUS3D.Formats.Json.Abstractions;

namespace NEXUS3D.Formats.BedrockGeometry.Conversion;

public interface IBedrockGeometryToSceneConverter
{
    (Scene3D Scene, BedrockConversionReport Report) Convert(
        BedrockGeometryDocument document,
        BedrockMappingOptions? options = null,
        IReadOnlyList<BedrockGeometryDiagnostic>? parseDiagnostics = null);
}

/// <summary>
/// Converts a parsed <see cref="BedrockGeometryDocument"/> into a Universal Scene Model (<see cref="Scene3D"/>)
/// with hierarchical <see cref="SceneNode"/> rigs, <see cref="SkeletonData"/> (<see cref="BoneData"/> + <see cref="BoneLocator"/>),
/// <see cref="MeshData"/> for cubes and poly_meshes, and preserved extension metadata.
/// </summary>
public sealed class BedrockGeometryToSceneConverter : IBedrockGeometryToSceneConverter
{
    public const string ExtensionNamespace = "minecraft:bedrock_geometry";

    public (Scene3D Scene, BedrockConversionReport Report) Convert(
        BedrockGeometryDocument document,
        BedrockMappingOptions? options = null,
        IReadOnlyList<BedrockGeometryDiagnostic>? parseDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= BedrockMappingOptions.Default;

        var lossReport = new LossReport();
        var warnings = new List<WarningRecord>();
        var issues = new List<ValidationIssue>();

        if (parseDiagnostics is not null)
        {
            foreach (BedrockGeometryDiagnostic diag in parseDiagnostics)
            {
                var severity = diag.IsError ? ValidationSeverity.Error : ValidationSeverity.Warning;
                issues.Add(new ValidationIssue(diag.Code, diag.Message, severity, diag.JsonPath));
                if (!diag.IsError)
                {
                    warnings.Add(new WarningRecord
                    {
                        Code = diag.Code,
                        Message = diag.Message,
                        SourceComponent = nameof(BedrockGeometryToSceneConverter)
                    });
                }
            }
        }

        string primaryName = document.Geometries.FirstOrDefault()?.Description.Identifier ?? "geometry.bedrock";
        var scene = new Scene3D
        {
            Name = primaryName
        };

        scene.Metadata.Title = primaryName;
        scene.Metadata.Source = new SourceMetadata
        {
            OriginalFileName = options.SourceFileName,
            OriginalExtension = ".geo.json",
            OriginalFormatId = ExtensionNamespace,
            OriginalFormatVersion = document.FormatVersion,
            FileSizeBytes = options.SourceFileSizeBytes,
            SourceApplication = "NEXUS3D.Formats.BedrockGeometry",
            OriginalCoordinateSystem = CoordinateSystem.BedrockDefault
        };

        scene.Metadata.Formats.Add(new FormatMetadata(ExtensionNamespace, document.FormatVersion));
        scene.Metadata.ProvenanceHistory.Add(new DataProvenance(
            DataOriginKind.Imported,
            "NEXUS3D.Formats.BedrockGeometry",
            "BedrockGeometryToSceneConverter.Convert",
            $"Imported {document.Geometries.Count} Bedrock geometry definition(s) (format_version {document.FormatVersion})."));

        int totalBones = 0;
        int totalCubes = 0;
        int validCubes = 0;
        int errorCubes = 0;
        int cubesWithUv = 0;
        int bonesWithParent = 0;
        int totalLocators = 0;
        int totalPolyMeshes = 0;
        int generatedMeshes = 0;
        int totalFaces = 0;
        int totalVertices = 0;
        int totalTriangles = 0;
        int preservedExtensions = document.RootExtensionData.Count;

        var materialByName = new Dictionary<string, MaterialData>(StringComparer.OrdinalIgnoreCase);

        for (int gIdx = 0; gIdx < document.Geometries.Count; gIdx++)
        {
            Models.BedrockGeometry geo = document.Geometries[gIdx];
            preservedExtensions += geo.ExtensionData.Count + geo.Description.ExtensionData.Count;

            string geoId = !string.IsNullOrWhiteSpace(geo.Description.Identifier)
                ? geo.Description.Identifier
                : $"geometry.model_{gIdx}";

            MaterialData defaultGeoMaterial = GetOrCreateMaterial(scene, materialByName, $"{geoId}_material");
            defaultGeoMaterial.CustomProperties["bedrock.textureWidth"] = JsonSerializer.SerializeToElement(geo.Description.TextureWidth);
            defaultGeoMaterial.CustomProperties["bedrock.textureHeight"] = JsonSerializer.SerializeToElement(geo.Description.TextureHeight);

            var geoRootObj = new SceneObject(geoId);
            var geoRootNode = new SceneNode(geoId, geoRootObj);
            scene.Graph.AddNode(scene.Graph.Root, geoRootNode);

            var skeleton = new SkeletonData(geoId);
            geoRootObj.SkeletonId = skeleton.Id;

            var bedrockBoneByName = geo.Bones.ToDictionary(b => b.Name, StringComparer.Ordinal);

            // Pre-allocate BoneData & SceneNode for every bone in original declaration order
            var boneDataByName = new Dictionary<string, BoneData>(StringComparer.Ordinal);
            var boneNodeByName = new Dictionary<string, SceneNode>(StringComparer.Ordinal);

            for (int bIdx = 0; bIdx < geo.Bones.Count; bIdx++)
            {
                BedrockBone bone = geo.Bones[bIdx];
                totalBones++;
                preservedExtensions += bone.ExtensionData.Count;

                if (!string.IsNullOrWhiteSpace(bone.Parent))
                {
                    bonesWithParent++;
                }

                string boneName = !string.IsNullOrWhiteSpace(bone.Name) ? bone.Name : $"bone_{bIdx}";
                Vector3D usmPivot = BedrockCoordinateConverter.BedrockPositionToUsm(bone.Pivot);
                Vector3D usmRotEuler = BedrockCoordinateConverter.BedrockRotationToUsmEuler(bone.Rotation);
                QuaternionD usmRotQuat = BedrockCoordinateConverter.BedrockRotationToUsmQuaternion(bone.Rotation);

                // Hierarchical local transform:
                // - Root bone: local position is bone.pivot in USM space
                // - Child bone: local position is (bone.pivot - parent.pivot) in USM space
                Vector3D localPosition;
                if (!string.IsNullOrWhiteSpace(bone.Parent) &&
                    bedrockBoneByName.TryGetValue(bone.Parent, out BedrockBone? parentBone))
                {
                    localPosition = BedrockCoordinateConverter.BedrockPositionToUsm(bone.Pivot - parentBone.Pivot);
                }
                else
                {
                    localPosition = usmPivot;
                }

                var boneTransform = new Transform3D(localPosition, usmRotQuat, Vector3D.One);

                var usmBone = new BoneData(boneName)
                {
                    Pivot = usmPivot,
                    BindRotationDegrees = usmRotEuler,
                    BindingExpression = bone.Binding,
                    LocalTransform = boneTransform.DeepClone()
                };

                foreach ((string locName, BedrockLocator locator) in bone.Locators)
                {
                    totalLocators++;
                    preservedExtensions += locator.ExtensionData.Count;
                    usmBone.Locators.Add(new BoneLocator(
                        locName,
                        BedrockCoordinateConverter.BedrockPositionToUsm(locator.Offset),
                        locator.Rotation.HasValue
                            ? BedrockCoordinateConverter.BedrockRotationToUsmEuler(locator.Rotation.Value)
                            : null,
                        locator.IgnoreInheritedScale));
                }

                var boneSceneObj = new SceneObject(boneName)
                {
                    SkeletonId = skeleton.Id
                };
                boneSceneObj.Properties["bedrock.boneOrderIndex"] = JsonSerializer.SerializeToElement(bIdx);
                boneSceneObj.Properties["bedrock.rawPivot"] = JsonSerializer.SerializeToElement(new[] { bone.Pivot.X, bone.Pivot.Y, bone.Pivot.Z });
                boneSceneObj.Properties["bedrock.rawRotation"] = JsonSerializer.SerializeToElement(new[] { bone.Rotation.X, bone.Rotation.Y, bone.Rotation.Z });

                var boneNode = new SceneNode(boneName, boneSceneObj)
                {
                    LocalTransform = boneTransform,
                    IsVisible = bone.NeverRender != true
                };

                if (options.GenerateCubeMeshes)
                {
                    for (int cIdx = 0; cIdx < bone.Cubes.Count; cIdx++)
                    {
                        BedrockCube cube = bone.Cubes[cIdx];
                        totalCubes++;
                        preservedExtensions += cube.ExtensionData.Count;

                        if (cube.Uv is not null)
                        {
                            cubesWithUv++;
                            preservedExtensions += cube.Uv.ExtensionData.Count;
                            foreach (BedrockFaceUv fUv in cube.Uv.Faces.Values)
                            {
                                preservedExtensions += fUv.ExtensionData.Count;
                                if (!string.IsNullOrWhiteSpace(fUv.MaterialInstance))
                                {
                                    GetOrCreateMaterial(scene, materialByName, fUv.MaterialInstance!);
                                }
                            }
                        }

                        if (!cube.HasValidOrigin || !cube.HasValidSize)
                        {
                            errorCubes++;
                            continue;
                        }

                        validCubes++;

                        MaterialData cubeMat = !string.IsNullOrWhiteSpace(bone.Material)
                            ? GetOrCreateMaterial(scene, materialByName, bone.Material!)
                            : defaultGeoMaterial;

                        string cubeName = $"{boneName}_cube_{cIdx}";
                        if (cube.ExtensionData.TryGet("name", out var extNameElem) &&
                            extNameElem.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrWhiteSpace(extNameElem.GetString()))
                        {
                            cubeName = extNameElem.GetString()!;
                        }

                        MeshData cubeMesh = BedrockCoordinateConverter.BuildCubeMesh(
                            cube,
                            bone,
                            geo.Description,
                            cubeName,
                            cubeMat.Id);

                        totalFaces += cubeMesh.Faces.Count;
                        totalVertices += cubeMesh.Vertices.Count;
                        totalTriangles += cubeMesh.Faces.Sum(f => Math.Max(0, f.VertexIndices.Count - 2));

                        var cubeSceneObj = new SceneObject(cubeName)
                        {
                            SkeletonId = skeleton.Id
                        };
                        cubeSceneObj.Meshes.Add(cubeMesh);

                        Guid cubeId = cubeSceneObj.Id;
                        Vector3D cubePivot = cube.Pivot ?? bone.Pivot;
                        Vector3D cubeRotation = cube.Rotation ?? Vector3D.Zero;
                        double cubeInflate = cube.Inflate ?? bone.Inflate ?? 0.0;
                        bool cubeMirror = cube.Mirror ?? bone.Mirror ?? false;
                        string uvDescription = cube.Uv?.Mode == BedrockUvMode.PerFaceUv
                            ? "PerFace"
                            : (cube.Uv?.Mode == BedrockUvMode.BoxUv ? $"[{cube.Uv.BoxUvOffset.X:F1}, {cube.Uv.BoxUvOffset.Y:F1}]" : "None");

                        // Full cube metadata properties
                        cubeSceneObj.Properties["CubeId"] = JsonSerializer.SerializeToElement(cubeId.ToString());
                        cubeSceneObj.Properties["Name"] = JsonSerializer.SerializeToElement(cubeName);
                        cubeSceneObj.Properties["GeometryId"] = JsonSerializer.SerializeToElement(geoId);
                        cubeSceneObj.Properties["BoneId"] = JsonSerializer.SerializeToElement(usmBone.Id.ToString());
                        cubeSceneObj.Properties["BoneName"] = JsonSerializer.SerializeToElement(boneName);
                        cubeSceneObj.Properties["ParentBone"] = JsonSerializer.SerializeToElement(bone.Parent ?? string.Empty);
                        cubeSceneObj.Properties["Origin"] = JsonSerializer.SerializeToElement(new[] { cube.Origin.X, cube.Origin.Y, cube.Origin.Z });
                        cubeSceneObj.Properties["Size"] = JsonSerializer.SerializeToElement(new[] { cube.Size.X, cube.Size.Y, cube.Size.Z });
                        cubeSceneObj.Properties["Pivot"] = JsonSerializer.SerializeToElement(new[] { cubePivot.X, cubePivot.Y, cubePivot.Z });
                        cubeSceneObj.Properties["Rotation"] = JsonSerializer.SerializeToElement(new[] { cubeRotation.X, cubeRotation.Y, cubeRotation.Z });
                        cubeSceneObj.Properties["UV"] = JsonSerializer.SerializeToElement(uvDescription);
                        cubeSceneObj.Properties["Inflate"] = JsonSerializer.SerializeToElement(cubeInflate);
                        cubeSceneObj.Properties["Mirror"] = JsonSerializer.SerializeToElement(cubeMirror);
                        cubeSceneObj.Properties["Material"] = JsonSerializer.SerializeToElement(cubeMat.Name);

                        cubeSceneObj.Properties["bedrock.cubeId"] = JsonSerializer.SerializeToElement(cubeId.ToString());
                        cubeSceneObj.Properties["bedrock.cubeIndex"] = JsonSerializer.SerializeToElement(cIdx);
                        cubeSceneObj.Properties["bedrock.name"] = JsonSerializer.SerializeToElement(cubeName);
                        cubeSceneObj.Properties["bedrock.geometryId"] = JsonSerializer.SerializeToElement(geoId);
                        cubeSceneObj.Properties["bedrock.boneId"] = JsonSerializer.SerializeToElement(usmBone.Id.ToString());
                        cubeSceneObj.Properties["bedrock.boneName"] = JsonSerializer.SerializeToElement(boneName);
                        cubeSceneObj.Properties["bedrock.parentBone"] = JsonSerializer.SerializeToElement(bone.Parent ?? string.Empty);
                        cubeSceneObj.Properties["bedrock.origin"] = JsonSerializer.SerializeToElement(new[] { cube.Origin.X, cube.Origin.Y, cube.Origin.Z });
                        cubeSceneObj.Properties["bedrock.rawOrigin"] = JsonSerializer.SerializeToElement(new[] { cube.Origin.X, cube.Origin.Y, cube.Origin.Z });
                        cubeSceneObj.Properties["bedrock.size"] = JsonSerializer.SerializeToElement(new[] { cube.Size.X, cube.Size.Y, cube.Size.Z });
                        cubeSceneObj.Properties["bedrock.rawSize"] = JsonSerializer.SerializeToElement(new[] { cube.Size.X, cube.Size.Y, cube.Size.Z });
                        cubeSceneObj.Properties["bedrock.pivot"] = JsonSerializer.SerializeToElement(new[] { cubePivot.X, cubePivot.Y, cubePivot.Z });
                        cubeSceneObj.Properties["bedrock.rawPivot"] = JsonSerializer.SerializeToElement(new[] { cubePivot.X, cubePivot.Y, cubePivot.Z });
                        cubeSceneObj.Properties["bedrock.rotation"] = JsonSerializer.SerializeToElement(new[] { cubeRotation.X, cubeRotation.Y, cubeRotation.Z });
                        cubeSceneObj.Properties["bedrock.rawRotation"] = JsonSerializer.SerializeToElement(new[] { cubeRotation.X, cubeRotation.Y, cubeRotation.Z });
                        cubeSceneObj.Properties["bedrock.uv"] = JsonSerializer.SerializeToElement(uvDescription);
                        cubeSceneObj.Properties["bedrock.uvMode"] = JsonSerializer.SerializeToElement(cube.Uv?.Mode.ToString() ?? "None");
                        if (cube.Uv?.Mode == BedrockUvMode.BoxUv)
                        {
                            cubeSceneObj.Properties["bedrock.boxUvOffset"] = JsonSerializer.SerializeToElement(new[] { cube.Uv.BoxUvOffset.X, cube.Uv.BoxUvOffset.Y });
                        }
                        cubeSceneObj.Properties["bedrock.inflate"] = JsonSerializer.SerializeToElement(cubeInflate);
                        cubeSceneObj.Properties["bedrock.mirror"] = JsonSerializer.SerializeToElement(cubeMirror);
                        cubeSceneObj.Properties["bedrock.material"] = JsonSerializer.SerializeToElement(cubeMat.Name);

                        var cubeNode = new SceneNode(cubeName, cubeSceneObj)
                        {
                            LocalTransform = Transform3D.Identity(),
                            IsVisible = bone.NeverRender != true
                        };

                        boneNode.AddChild(cubeNode);
                        generatedMeshes++;
                    }

                    if (bone.PolyMesh is { Positions.Count: > 0 } polyMesh)
                    {
                        totalPolyMeshes++;
                        preservedExtensions += polyMesh.ExtensionData.Count;
                        MeshData? convertedPoly = BuildPolyMesh(polyMesh, $"{boneName}_polymesh", defaultGeoMaterial.Id, bone);
                        if (convertedPoly is not null)
                        {
                            totalFaces += convertedPoly.Faces.Count;
                            totalVertices += convertedPoly.Vertices.Count;
                            totalTriangles += convertedPoly.Faces.Sum(f => Math.Max(0, f.VertexIndices.Count - 2));

                            boneSceneObj.Meshes.Add(convertedPoly);
                            generatedMeshes++;
                        }
                    }
                }
                else
                {
                    totalCubes += bone.Cubes.Count;
                    if (bone.PolyMesh is not null) totalPolyMeshes++;
                }

                skeleton.Bones.Add(usmBone);
                boneDataByName.TryAdd(boneName, usmBone);
                boneNodeByName.TryAdd(boneName, boneNode);
            }

            // Wire parent-child relationships using topological order so out-of-order parent declarations work seamlessly
            IReadOnlyList<BedrockBone> topologicalBones = geo.GetTopologicalBones();
            foreach (BedrockBone bone in topologicalBones)
            {
                if (string.IsNullOrWhiteSpace(bone.Name) ||
                    !boneDataByName.TryGetValue(bone.Name, out BoneData? childBoneData) ||
                    !boneNodeByName.TryGetValue(bone.Name, out SceneNode? childNode))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(bone.Parent) &&
                    boneDataByName.TryGetValue(bone.Parent, out BoneData? parentBoneData) &&
                    boneNodeByName.TryGetValue(bone.Parent, out SceneNode? parentNode) &&
                    !ReferenceEquals(parentNode, childNode))
                {
                    childBoneData.ParentBoneId = parentBoneData.Id;
                    if (!parentBoneData.ChildBoneIds.Contains(childBoneData.Id))
                    {
                        parentBoneData.ChildBoneIds.Add(childBoneData.Id);
                    }

                    if (!childNode.ParentId.HasValue)
                    {
                        scene.Graph.AddNode(parentNode, childNode);
                    }
                }
                else
                {
                    skeleton.RootBoneId ??= childBoneData.Id;
                    if (!childNode.ParentId.HasValue)
                    {
                        scene.Graph.AddNode(geoRootNode, childNode);
                    }
                }
            }

            scene.Skeletons.Add(skeleton);
        }

        // Preserve full lossless Bedrock Geometry document snapshot in ExtensionData
        AttachDocumentExtensionData(scene, document, options.PreserveRawData);

        lossReport.RecordSupported("BedrockGeometry", $"Converted {document.Geometries.Count} Bedrock geometry definition(s), {totalBones} bone(s) ({bonesWithParent} with parent), {totalCubes} cube(s) ({validCubes} valid, {errorCubes} invalid, {cubesWithUv} with UV), and {totalLocators} locator(s).");
        lossReport.RecordSupported("BedrockGeometryDiagnostics", $"Diagnostics: {document.Geometries.Count} geometries, {totalBones} bones, {totalCubes} cubes, {generatedMeshes} meshes, {totalFaces} faces, {totalVertices} vertices, {totalTriangles} triangles.");
        if (preservedExtensions > 0 && options.PreserveRawData)
        {
            lossReport.RecordPreservedRaw("BedrockExtensions", $"Preserved {preservedExtensions} unknown/custom JSON properties in USM ExtensionData.");
        }

        var diagExt = new ExtensionData("bedrock:diagnostics");
        diagExt.Values["totalGeometries"] = JsonSerializer.SerializeToElement(document.Geometries.Count);
        diagExt.Values["totalBones"] = JsonSerializer.SerializeToElement(totalBones);
        diagExt.Values["totalCubes"] = JsonSerializer.SerializeToElement(totalCubes);
        diagExt.Values["validCubes"] = JsonSerializer.SerializeToElement(validCubes);
        diagExt.Values["errorCubes"] = JsonSerializer.SerializeToElement(errorCubes);
        diagExt.Values["meshes"] = JsonSerializer.SerializeToElement(generatedMeshes);
        diagExt.Values["faces"] = JsonSerializer.SerializeToElement(totalFaces);
        diagExt.Values["vertices"] = JsonSerializer.SerializeToElement(totalVertices);
        diagExt.Values["triangles"] = JsonSerializer.SerializeToElement(totalTriangles);
        diagExt.Values["cubesWithUv"] = JsonSerializer.SerializeToElement(cubesWithUv);
        diagExt.Values["bonesWithParent"] = JsonSerializer.SerializeToElement(bonesWithParent);
        scene.Metadata.AddExtension(diagExt);

        scene.Metadata.LossReport = lossReport;
        scene.Metadata.Warnings.AddRange(warnings);

        var report = new BedrockConversionReport
        {
            FormatVersion = document.FormatVersion,
            SchemaFamily = document.SchemaFamily,
            GeometryCount = document.Geometries.Count,
            BoneCount = totalBones,
            CubeCount = totalCubes,
            LocatorCount = totalLocators,
            PolyMeshCount = totalPolyMeshes,
            GeneratedMeshCount = generatedMeshes,
            PreservedExtensionCount = preservedExtensions,
            ValidCubeCount = validCubes,
            ErrorCubeCount = errorCubes,
            MeshCount = generatedMeshes,
            FaceCount = totalFaces,
            VertexCount = totalVertices,
            TriangleCount = totalTriangles,
            CubesWithUvCount = cubesWithUv,
            BonesWithParentCount = bonesWithParent,
            LossReport = lossReport,
            Warnings = warnings,
            Issues = issues
        };

        return (scene, report);
    }

    private static void AttachDocumentExtensionData(
        Scene3D scene,
        BedrockGeometryDocument document,
        bool preserveRawData)
    {
        var exporter = new BedrockGeometryExporter();
        string canonicalJson = exporter.ExportToString(document, new BedrockGeometryWriteOptions
        {
            PrettyPrint = false,
            PreserveOrder = true,
            PreserveMetadata = true,
            WriteExtensionData = preserveRawData,
            VerifyAfterWrite = false
        });

        using JsonDocument jsonDoc = JsonDocument.Parse(canonicalJson);
        var ext = new ExtensionData(ExtensionNamespace);
        ext.Values["formatVersion"] = JsonSerializer.SerializeToElement(document.FormatVersion);
        ext.Values["schemaFamily"] = JsonSerializer.SerializeToElement(document.SchemaFamily.ToString());
        ext.Values["documentSnapshot"] = jsonDoc.RootElement.Clone();
        scene.Metadata.AddExtension(ext);
    }

    private static MaterialData GetOrCreateMaterial(
        Scene3D scene,
        Dictionary<string, MaterialData> cache,
        string name)
    {
        if (cache.TryGetValue(name, out MaterialData? existing))
        {
            return existing;
        }

        var mat = new MaterialData(name);
        scene.Materials.Add(mat);
        cache[name] = mat;
        return mat;
    }

    private static MeshData? BuildPolyMesh(BedrockPolyMesh polyMesh, string meshName, Guid materialId, BedrockBone owningBone)
    {
        if (polyMesh.Positions.Count == 0)
        {
            return null;
        }

        Vector3D usmBonePivot = BedrockCoordinateConverter.BedrockPositionToUsm(owningBone.Pivot);
        var mesh = new MeshData(meshName, MeshTopology.Polygons);
        if (polyMesh.Polys.Count > 0)
        {
            foreach (int[][] face in polyMesh.Polys)
            {
                if (face.Length < 3)
                {
                    continue;
                }

                var faceIndices = new List<int>(face.Length);
                foreach (int[] vertIndices in face)
                {
                    if (vertIndices.Length == 0) continue;
                    int posIdx = vertIndices[0];
                    if (posIdx < 0 || posIdx >= polyMesh.Positions.Count) continue;

                    Vector3D pos = BedrockCoordinateConverter.BedrockPositionToUsm(polyMesh.Positions[posIdx]) - usmBonePivot;
                    Vector3D? norm = vertIndices.Length >= 2 && vertIndices[1] >= 0 && vertIndices[1] < polyMesh.Normals.Count
                        ? BedrockCoordinateConverter.BedrockPositionToUsm(polyMesh.Normals[vertIndices[1]]).Normalize()
                        : null;
                    Vector2D? uv = vertIndices.Length >= 3 && vertIndices[2] >= 0 && vertIndices[2] < polyMesh.Uvs.Count
                        ? polyMesh.Uvs[vertIndices[2]]
                        : null;

                    faceIndices.Add(mesh.Vertices.Count);
                    mesh.Vertices.Add(new Vertex(pos, norm, uv));
                }

                if (faceIndices.Count >= 3)
                {
                    mesh.Faces.Add(new Face(faceIndices, materialId));
                }
            }
        }
        else
        {
            for (int i = 0; i < polyMesh.Positions.Count; i++)
            {
                Vector3D pos = BedrockCoordinateConverter.BedrockPositionToUsm(polyMesh.Positions[i]) - usmBonePivot;
                Vector3D? norm = i < polyMesh.Normals.Count
                    ? BedrockCoordinateConverter.BedrockPositionToUsm(polyMesh.Normals[i]).Normalize()
                    : null;
                Vector2D? uv = i < polyMesh.Uvs.Count ? polyMesh.Uvs[i] : null;
                mesh.Vertices.Add(new Vertex(pos, norm, uv));
            }

            for (int i = 0; i + 2 < mesh.Vertices.Count; i += 3)
            {
                mesh.Faces.Add(new Face([i, i + 1, i + 2], materialId));
            }
        }

        return mesh.Vertices.Count > 0 ? mesh : null;
    }
}
