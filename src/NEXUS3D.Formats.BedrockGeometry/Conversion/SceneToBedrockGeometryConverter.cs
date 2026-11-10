using System.Text.Json;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Domain.Skeletons;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.BedrockGeometry.Models;
using NEXUS3D.Formats.BedrockGeometry.Parsing;

namespace NEXUS3D.Formats.BedrockGeometry.Conversion;

public interface ISceneToBedrockGeometryConverter
{
    (BedrockGeometryDocument Document, BedrockConversionReport Report) Convert(
        Scene3D scene,
        BedrockMappingOptions? options = null);
}

/// <summary>
/// Converts a Universal Scene Model (<see cref="Scene3D"/>) back into a <see cref="BedrockGeometryDocument"/>,
/// synchronizing any USM bone/locator edits while preserving Bedrock-specific cubes, UVs, and extension data,
/// or synthesizing Bedrock cuboid geometry from arbitrary USM meshes and voxel grids.
/// </summary>
public sealed class SceneToBedrockGeometryConverter : ISceneToBedrockGeometryConverter
{
    public (BedrockGeometryDocument Document, BedrockConversionReport Report) Convert(
        Scene3D scene,
        BedrockMappingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        options ??= BedrockMappingOptions.Default;

        var lossReport = new LossReport();
        var warnings = new List<WarningRecord>();
        var issues = new List<ValidationIssue>();

        BedrockGeometryDocument? document = null;

        if (scene.Metadata.Extensions.TryGetValue(BedrockGeometryToSceneConverter.ExtensionNamespace, out ExtensionData? ext) &&
            ext.Values.TryGetValue("documentSnapshot", out JsonElement snapshotElem))
        {
            var parser = new BedrockGeometryParser();
            BedrockGeometryParseResult parsed = parser.Parse(
                snapshotElem.GetRawText(),
                new BedrockGeometryReadOptions
                {
                    StrictMode = false,
                    PreserveExtensionData = options.PreserveRawData
                });

            document = parsed.Document;
            SynchronizeUsmSkeletonChanges(scene, document);
        }

        if (document is null)
        {
            document = SynthesizeFromUsmScene(scene, options, lossReport);
        }

        if (!string.IsNullOrWhiteSpace(options.TargetFormatVersion))
        {
            document.FormatVersion = options.TargetFormatVersion!;
            BedrockGeometrySchemaFamily targetFamily = BedrockGeometryDetector.ClassifyVersionString(document.FormatVersion);
            if (targetFamily != BedrockGeometrySchemaFamily.Unknown)
            {
                document.SchemaFamily = targetFamily;
            }
        }

        int totalBones = 0;
        int totalCubes = 0;
        int totalLocators = 0;
        int totalPolyMeshes = 0;
        int preservedExtensions = document.RootExtensionData.Count;

        foreach (Models.BedrockGeometry geo in document.Geometries)
        {
            preservedExtensions += geo.ExtensionData.Count + geo.Description.ExtensionData.Count;
            foreach (BedrockBone bone in geo.Bones)
            {
                totalBones++;
                totalCubes += bone.Cubes.Count;
                totalLocators += bone.Locators.Count;
                if (bone.PolyMesh is not null) totalPolyMeshes++;
                preservedExtensions += bone.ExtensionData.Count;
            }
        }

        lossReport.RecordSupported("BedrockGeometry", $"Prepared {document.Geometries.Count} Bedrock geometry definition(s) ({totalBones} bones, {totalCubes} cubes).");

        var report = new BedrockConversionReport
        {
            FormatVersion = document.FormatVersion,
            SchemaFamily = document.SchemaFamily,
            GeometryCount = document.Geometries.Count,
            BoneCount = totalBones,
            CubeCount = totalCubes,
            LocatorCount = totalLocators,
            PolyMeshCount = totalPolyMeshes,
            PreservedExtensionCount = preservedExtensions,
            LossReport = lossReport,
            Warnings = warnings,
            Issues = issues
        };

        return (document, report);
    }

    private static void SynchronizeUsmSkeletonChanges(Scene3D scene, BedrockGeometryDocument document)
    {
        for (int gIdx = 0; gIdx < document.Geometries.Count && gIdx < scene.Skeletons.Count; gIdx++)
        {
            Models.BedrockGeometry geo = document.Geometries[gIdx];
            SkeletonData skeleton = scene.Skeletons[gIdx];

            var usmBoneByName = new Dictionary<string, BoneData>(StringComparer.Ordinal);
            foreach (BoneData b in skeleton.Bones)
            {
                usmBoneByName.TryAdd(b.Name, b);
            }

            foreach (BedrockBone bedrockBone in geo.Bones)
            {
                if (!usmBoneByName.TryGetValue(bedrockBone.Name, out BoneData? usmBone))
                {
                    continue;
                }

                bedrockBone.Pivot = BedrockCoordinateConverter.UsmPositionToBedrock(usmBone.Pivot);
                bedrockBone.Rotation = BedrockCoordinateConverter.UsmEulerToBedrockRotation(usmBone.BindRotationDegrees);
                bedrockBone.Binding = usmBone.BindingExpression;

                foreach (BoneLocator usmLoc in usmBone.Locators)
                {
                    if (bedrockBone.Locators.TryGetValue(usmLoc.Name, out BedrockLocator? existingLoc))
                    {
                        existingLoc.Offset = BedrockCoordinateConverter.UsmPositionToBedrock(usmLoc.Offset);
                        existingLoc.Rotation = usmLoc.RotationDegrees.HasValue
                            ? BedrockCoordinateConverter.UsmEulerToBedrockRotation(usmLoc.RotationDegrees.Value)
                            : null;
                        existingLoc.IgnoreInheritedScale = usmLoc.IgnoreInheritedScale;
                    }
                    else
                    {
                        bedrockBone.Locators[usmLoc.Name] = new BedrockLocator
                        {
                            Name = usmLoc.Name,
                            Offset = BedrockCoordinateConverter.UsmPositionToBedrock(usmLoc.Offset),
                            Rotation = usmLoc.RotationDegrees.HasValue
                                ? BedrockCoordinateConverter.UsmEulerToBedrockRotation(usmLoc.RotationDegrees.Value)
                                : null,
                            IgnoreInheritedScale = usmLoc.IgnoreInheritedScale,
                            IsObjectForm = usmLoc.RotationDegrees.HasValue || usmLoc.IgnoreInheritedScale.HasValue
                        };
                    }
                }
            }
        }
    }

    private static BedrockGeometryDocument SynthesizeFromUsmScene(
        Scene3D scene,
        BedrockMappingOptions options,
        LossReport lossReport)
    {
        string targetVersion = !string.IsNullOrWhiteSpace(options.TargetFormatVersion)
            ? options.TargetFormatVersion!
            : "1.12.0";

        BedrockGeometrySchemaFamily family = BedrockGeometryDetector.ClassifyVersionString(targetVersion);
        if (family == BedrockGeometrySchemaFamily.Unknown)
        {
            family = BedrockGeometrySchemaFamily.Intermediate1120;
        }

        string rawName = !string.IsNullOrWhiteSpace(scene.Name) ? scene.Name : "model";
        string identifier = rawName.StartsWith("geometry.", StringComparison.OrdinalIgnoreCase)
            ? rawName
            : $"geometry.{SanitizeIdentifier(rawName)}";

        var geo = new Models.BedrockGeometry
        {
            Description = new BedrockGeometryDescription
            {
                Identifier = identifier,
                TextureWidth = 64,
                TextureHeight = 64,
                VisibleBoundsWidth = 2.0,
                VisibleBoundsHeight = 2.0,
                VisibleBoundsOffset = Vector3D.Zero
            }
        };

        if (scene.Skeletons.Count > 0)
        {
            SkeletonData skel = scene.Skeletons[0];
            var boneById = skel.Bones.ToDictionary(b => b.Id);
            foreach (BoneData usmBone in skel.Bones)
            {
                string? parentName = usmBone.ParentBoneId.HasValue && boneById.TryGetValue(usmBone.ParentBoneId.Value, out BoneData? parent)
                    ? parent.Name
                    : null;

                var bedrockBone = new BedrockBone
                {
                    Name = usmBone.Name,
                    Parent = parentName,
                    Pivot = BedrockCoordinateConverter.UsmPositionToBedrock(usmBone.Pivot),
                    Rotation = BedrockCoordinateConverter.UsmEulerToBedrockRotation(usmBone.BindRotationDegrees),
                    Binding = usmBone.BindingExpression
                };

                foreach (BoneLocator loc in usmBone.Locators)
                {
                    bedrockBone.Locators[loc.Name] = new BedrockLocator
                    {
                        Name = loc.Name,
                        Offset = BedrockCoordinateConverter.UsmPositionToBedrock(loc.Offset),
                        Rotation = loc.RotationDegrees.HasValue
                            ? BedrockCoordinateConverter.UsmEulerToBedrockRotation(loc.RotationDegrees.Value)
                            : null,
                        IgnoreInheritedScale = loc.IgnoreInheritedScale,
                        IsObjectForm = loc.RotationDegrees.HasValue || loc.IgnoreInheritedScale.HasValue
                    };
                }

                geo.Bones.Add(bedrockBone);
            }
        }

        if (geo.Bones.Count == 0)
        {
            var rootBone = new BedrockBone
            {
                Name = "root",
                Pivot = Vector3D.Zero
            };

            foreach (SceneNode node in scene.Graph.Traverse())
            {
                if (node.Object is null) continue;

                foreach (VoxelGrid grid in node.Object.VoxelGrids)
                {
                    foreach (Voxel v in grid.Voxels)
                    {
                        if (v.IsAir) continue;
                        rootBone.Cubes.Add(new BedrockCube
                        {
                            Origin = new Vector3D(v.Position.X, v.Position.Y, v.Position.Z),
                            Size = grid.VoxelSize.ToVector3D(),
                            Uv = BedrockCubeUv.CreateBoxUv(0, 0)
                        });
                    }
                }

                foreach (MeshData mesh in node.Object.Meshes)
                {
                    BoundingBox bounds = mesh.CalculateBounds();
                    if (bounds.IsValid)
                    {
                        rootBone.Cubes.Add(new BedrockCube
                        {
                            Origin = bounds.Min,
                            Size = bounds.Size,
                            Uv = BedrockCubeUv.CreateBoxUv(0, 0)
                        });
                        lossReport.RecordApproximated(
                            $"Mesh:{mesh.Name}",
                            $"Approximated polygon mesh '{mesh.Name}' as bounding Bedrock cube.");
                    }
                }
            }

            geo.Bones.Add(rootBone);
        }

        return new BedrockGeometryDocument
        {
            FormatVersion = targetVersion,
            SchemaFamily = family,
            Geometries = [geo]
        };
    }

    private static string SanitizeIdentifier(string name)
    {
        var chars = name
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) || c is '_' or '.' ? c : '_')
            .ToArray();
        string cleaned = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? "model" : cleaned;
    }
}
