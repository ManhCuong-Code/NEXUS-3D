using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Meshes;

namespace NEXUS3D.Voxelization.Tests;

public static class TestMeshFactory
{
    /// <summary>
    /// Creates a 1x1x1 closed watertight cube mesh centered at (0, 0, 0) with 12 triangles.
    /// </summary>
    public static MeshData CreateCubeMesh(string name = "TestCube")
    {
        var mesh = new MeshData(name);

        // 8 vertices for [-0.5, 0.5]^3
        mesh.Vertices.Add(new Vertex(new Vector3D(-0.5, -0.5, -0.5))); // 0
        mesh.Vertices.Add(new Vertex(new Vector3D( 0.5, -0.5, -0.5))); // 1
        mesh.Vertices.Add(new Vertex(new Vector3D( 0.5,  0.5, -0.5))); // 2
        mesh.Vertices.Add(new Vertex(new Vector3D(-0.5,  0.5, -0.5))); // 3
        mesh.Vertices.Add(new Vertex(new Vector3D(-0.5, -0.5,  0.5))); // 4
        mesh.Vertices.Add(new Vertex(new Vector3D( 0.5, -0.5,  0.5))); // 5
        mesh.Vertices.Add(new Vertex(new Vector3D( 0.5,  0.5,  0.5))); // 6
        mesh.Vertices.Add(new Vertex(new Vector3D(-0.5,  0.5,  0.5))); // 7

        // 12 triangles (2 per cube face) with outward normals
        AddQuad(mesh, 0, 3, 2, 1); // Front (-Z)
        AddQuad(mesh, 5, 6, 7, 4); // Back (+Z)
        AddQuad(mesh, 4, 7, 3, 0); // Left (-X)
        AddQuad(mesh, 1, 2, 6, 5); // Right (+X)
        AddQuad(mesh, 3, 7, 6, 2); // Top (+Y)
        AddQuad(mesh, 4, 0, 1, 5); // Bottom (-Y)

        return mesh;
    }

    /// <summary>
    /// Creates a cube mesh missing one face (open non-manifold boundary).
    /// </summary>
    public static MeshData CreateOpenCubeMesh(string name = "OpenCube")
    {
        var mesh = new MeshData(name);

        mesh.Vertices.Add(new Vertex(new Vector3D(-0.5, -0.5, -0.5))); // 0
        mesh.Vertices.Add(new Vertex(new Vector3D( 0.5, -0.5, -0.5))); // 1
        mesh.Vertices.Add(new Vertex(new Vector3D( 0.5,  0.5, -0.5))); // 2
        mesh.Vertices.Add(new Vertex(new Vector3D(-0.5,  0.5, -0.5))); // 3
        mesh.Vertices.Add(new Vertex(new Vector3D(-0.5, -0.5,  0.5))); // 4
        mesh.Vertices.Add(new Vertex(new Vector3D( 0.5, -0.5,  0.5))); // 5
        mesh.Vertices.Add(new Vertex(new Vector3D( 0.5,  0.5,  0.5))); // 6
        mesh.Vertices.Add(new Vertex(new Vector3D(-0.5,  0.5,  0.5))); // 7

        // Omit top (+Y) face
        AddQuad(mesh, 0, 3, 2, 1);
        AddQuad(mesh, 5, 6, 7, 4);
        AddQuad(mesh, 4, 7, 3, 0);
        AddQuad(mesh, 1, 2, 6, 5);
        AddQuad(mesh, 4, 0, 1, 5);

        return mesh;
    }

    /// <summary>
    /// Creates a single thin diagonal plane mesh.
    /// </summary>
    public static MeshData CreatePlaneMesh(string name = "Plane")
    {
        var mesh = new MeshData(name);

        mesh.Vertices.Add(new Vertex(new Vector3D(0.0, 0.0, 0.0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(2.0, 0.0, 2.0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(2.0, 2.0, 2.0)));
        mesh.Vertices.Add(new Vertex(new Vector3D(0.0, 2.0, 0.0)));

        AddQuad(mesh, 0, 1, 2, 3);
        return mesh;
    }

    private static void AddQuad(MeshData mesh, int i0, int i1, int i2, int i3)
    {
        mesh.Faces.Add(new Face([i0, i1, i2]));
        mesh.Faces.Add(new Face([i0, i2, i3]));
    }
}
