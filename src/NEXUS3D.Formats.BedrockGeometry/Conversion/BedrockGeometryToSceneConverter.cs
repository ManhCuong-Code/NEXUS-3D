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
        int totalLocators = 0;
        int totalPolyMeshes = 0;
        int generatedMeshes = 0;
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

            // Pre-allocate BoneData & SceneNode for every bone in original declaration order
            var boneDataByName = new Dictionary<string, BoneData>(StringComparer.Ordinal);
            var boneNodeByName = new Dictionary<string, SceneNode>(StringComparer.Ordinal);

            for (int bIdx = 0; bIdx < geo.Bones.Count; bIdx++)
            {
                BedrockBone bone = geo.Bones[bIdx];
                totalBones++;
                preservedExtensions += bone.ExtensionData.Count;

                string boneName = !string.IsNullOrWhiteSpace(bone.Name) ? bone.Name : $"bone_{bIdx}";
                Vector3D usmPivot = BedrockCoordinateConverter.BedrockPositionToUsm(bone.Pivot);
                Vector3D usmRotEuler = BedrockCoordinateConverter.BedrockRotationToUsmEuler(bone.Rotation);
                QuaternionD usmRotQuat = BedrockCoordinateConverter.BedrockRotationToUsmQuaternion(bone.Rotation);

                var usmBone = new BoneData(boneName)
                {
                    Pivot = usmPivot,
                    BindRotationDegrees = usmRotEuler,
                    BindingExpression = bone.Binding,
                    LocalTransform = new Transform3D(usmPivot, usmRotQuat, Vector3D.One)
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

                if (options.GenerateCubeMeshes)
                {
                    for (int cIdx = 0; cIdx < bone.Cubes.Count; cIdx++)
                    {
                        BedrockCube cube = bone.Cubes[cIdx];
                        totalCubes++;
                        preservedExtensions += cube.ExtensionData.Count;
                        if (cube.Uv is not null)
                        {
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
                            continue;
                        }

                        MaterialData cubeMat = !string.IsNullOrWhiteSpace(bone.Material)
                            ? GetOrCreateMaterial(scene, materialByName, bone.Material!)
                            : defaultGeoMaterial;

                        MeshData cubeMesh = BedrockCoordinateConverter.BuildCubeMesh(
                            cube,
                            bone,
                            geo.Description,
                            $"{boneName}_cube_{cIdx}",
                            cubeMat.Id);

                        boneSceneObj.Meshes.Add(cubeMesh);
                        generatedMeshes++;
                    }

                    if (bone.PolyMesh is { Positions.Count: > 0 } polyMesh)
                    {
                        totalPolyMeshes++;
                        preservedExtensions += polyMesh.ExtensionData.Count;
                        MeshData? convertedPoly = BuildPolyMesh(polyMesh, $"{boneName}_polymesh", defaultGeoMaterial.Id);
                        if (convertedPoly is not null)
                        {
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

                var boneNode = new SceneNode(boneName, boneSceneObj)
                {
                    LocalTransform = new Transform3D(usmPivot, usmRotQuat, Vector3D.One),
                    IsVisible = bone.NeverRender != true
                };

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

        lossReport.RecordSupported("BedrockGeometry", $"Converted {document.Geometries.Count} Bedrock geometry definition(s), {totalBones} bone(s), {totalCubes} cube(s), and {totalLocators} locator(s).");
        if (preservedExtensions > 0 && options.PreserveRawData)
        {
            lossReport.RecordPreservedRaw("BedrockExtensions", $"Preserved {preservedExtensions} unknown/custom JSON properties in USM ExtensionData.");
        }

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

    private static MeshData? BuildPolyMesh(BedrockPolyMesh polyMesh, string meshName, Guid materialId)
    {
        if (polyMesh.Positions.Count == 0)
        {
            return null;
        }

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

                    Vector3D pos = BedrockCoordinateConverter.BedrockPositionToUsm(polyMesh.Positions[posIdx]);
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
                Vector3D pos = BedrockCoordinateConverter.BedrockPositionToUsm(polyMesh.Positions[i]);
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
