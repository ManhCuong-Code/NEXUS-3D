using Microsoft.Extensions.Logging;
using NEXUS3D.Conversion.Abstractions.Interfaces;
using NEXUS3D.Conversion.Abstractions.Models;
using NEXUS3D.Domain.Coordinates;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Shared.Guards;
using DomainCoordConverter = NEXUS3D.Domain.Coordinates.CoordinateConverter;

namespace NEXUS3D.Conversion.Engine.Services;

/// <summary>
/// Service executing coordinate system transforms, axis permutations, scale adjustments,
/// and winding order corrections across all geometry, nodes, and bones in a <see cref="Scene3D"/>.
/// </summary>
public sealed class CoordinateConversionService : NEXUS3D.Conversion.Abstractions.Interfaces.ICoordinateConverter
{
    private readonly DomainCoordConverter _domainMathConverter = new();
    private readonly ILogger<CoordinateConversionService> _logger;

    public CoordinateConversionService(ILogger<CoordinateConversionService> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public void AdaptCoordinates(
        Scene3D scene,
        CoordinateSystem sourceCoords,
        CoordinateSystem targetCoords,
        CoordinateTransformOptions? options = null)
    {
        Guard.NotNull(scene);
        Guard.NotNull(sourceCoords);
        Guard.NotNull(targetCoords);

        options ??= new CoordinateTransformOptions();

        Matrix4x4D matrix = _domainMathConverter.CreateConversionMatrix(sourceCoords, targetCoords);

        if (Math.Abs(options.Scale - 1.0) > 1e-6)
        {
            matrix = Matrix4x4D.CreateScale(new Vector3D(options.Scale, options.Scale, options.Scale)) * matrix;
        }

        bool requiresWindingFlip = _domainMathConverter.RequiresWindingFlip(sourceCoords, targetCoords) ||
                                   matrix.HasNegativeScale ||
                                   (options.InvertWindingOrder && (options.InvertAxisX || options.InvertAxisY || options.InvertAxisZ));

        _logger.LogInformation(
            "Adapting scene coordinates: Handedness {SourceHand} -> {TargetHand}, Unit {SourceUnit} -> {TargetUnit}, Scale {Scale:F2}x, WindingFlip: {WindingFlip}",
            sourceCoords.Handedness,
            targetCoords.Handedness,
            sourceCoords.Unit,
            targetCoords.Unit,
            options.Scale,
            requiresWindingFlip);

        // 1. Transform Meshes in SceneNodes
        int totalVertices = 0;
        int totalFaces = 0;

        foreach (var node in scene.Graph.Traverse())
        {
            if (node.Object is not { } obj)
            {
                continue;
            }

            foreach (var mesh in obj.Meshes)
            {
                TransformMesh(mesh, matrix, requiresWindingFlip, options);
                totalVertices += mesh.Vertices.Count;
                totalFaces += mesh.Faces.Count;
            }
        }

        // 2. Transform Skeletons & Bones
        foreach (var skeleton in scene.Skeletons)
        {
            foreach (var bone in skeleton.Bones)
            {
                bone.Pivot = matrix.TransformPoint(bone.Pivot);

                if (requiresWindingFlip)
                {
                    bone.BindRotationDegrees = new Vector3D(
                        bone.BindRotationDegrees.X,
                        -bone.BindRotationDegrees.Y,
                        -bone.BindRotationDegrees.Z);
                }

                for (int i = 0; i < bone.Locators.Count; i++)
                {
                    bone.Locators[i].Offset = matrix.TransformPoint(bone.Locators[i].Offset);
                }
            }
        }

        // 3. Update Scene Coordinates Metadata
        scene.Coordinates.SourceSystem = sourceCoords.DeepClone();
        scene.Coordinates.CurrentSystem = targetCoords.DeepClone();
        scene.Coordinates.ScaleFactor = options.Scale;
        scene.Coordinates.AppliedConversionMatrix = matrix;

        _logger.LogInformation(
            "Successfully adapted scene '{SceneName}' ({Vertices} vertices, {Faces} faces).",
            scene.Name,
            totalVertices,
            totalFaces);
    }

    private static void TransformMesh(
        MeshData mesh,
        Matrix4x4D matrix,
        bool requiresWindingFlip,
        CoordinateTransformOptions options)
    {
        // Transform vertices
        for (int i = 0; i < mesh.Vertices.Count; i++)
        {
            var v = mesh.Vertices[i];
            Vector3D pos = matrix.TransformPoint(v.Position);

            Vector3D? norm = null;
            if (v.Normal.HasValue)
            {
                norm = matrix.TransformDirection(v.Normal.Value).Normalize();
                if (options.InvertNormals && requiresWindingFlip)
                {
                    norm = -norm.Value;
                }
            }

            v.Position = pos;
            v.Normal = norm;
        }

        // Correct face winding order on reflection/handedness flip
        if (requiresWindingFlip)
        {
            for (int i = 0; i < mesh.Faces.Count; i++)
            {
                var face = mesh.Faces[i];
                if (face.VertexIndices.Count >= 3)
                {
                    var reversedIndices = face.VertexIndices.Reverse().ToArray();
                    face.VertexIndices = reversedIndices;

                    if (face.Corners is { Count: >= 3 } corners)
                    {
                        face.Corners = corners.Reverse().ToArray();
                    }
                }
            }
        }
    }
}
