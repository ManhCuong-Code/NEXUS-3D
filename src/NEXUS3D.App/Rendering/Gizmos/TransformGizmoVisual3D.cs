using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using NEXUS3D.Editor.Abstractions;

namespace NEXUS3D.App.Rendering.Gizmos;

/// <summary>
/// 3D interactive manipulation gizmo supporting Translate, Rotate, Scale, and Pivot modes in HelixViewport3D.
/// Built with pure native WPF MeshGeometry3D for 100% platform stability and performance.
/// </summary>
public sealed class TransformGizmoVisual3D : ModelVisual3D
{
    private readonly Model3DGroup _gizmoGroup = new();
    private readonly Dictionary<GeometryModel3D, GizmoHandle> _handleMap = new();

    private EditorToolType _currentTool = EditorToolType.None;
    private Point3D _position = new(0, 0, 0);
    private System.Windows.Media.Media3D.Quaternion _rotation = System.Windows.Media.Media3D.Quaternion.Identity;
    private double _scale = 1.0;

    public EditorToolType CurrentTool => _currentTool;

    public TransformGizmoVisual3D()
    {
        Content = _gizmoGroup;
    }

    /// <summary>
    /// Performs hit testing to identify which gizmo handle was clicked.
    /// </summary>
    public GizmoHandle HitTestHandle(GeometryModel3D? hitModel)
    {
        if (hitModel is not null && _handleMap.TryGetValue(hitModel, out var handle))
        {
            return handle;
        }

        return GizmoHandle.None;
    }

    /// <summary>
    /// Updates the gizmo tool type, spatial placement, orientation, and camera-distance scaling.
    /// </summary>
    public void UpdateGizmo(
        EditorToolType tool,
        Point3D worldPosition,
        System.Windows.Media.Media3D.Quaternion worldRotation,
        double cameraDistance)
    {
        _position = worldPosition;
        _rotation = worldRotation;
        _scale = Math.Max(0.2, cameraDistance * 0.08);

        if (_currentTool != tool)
        {
            _currentTool = tool;
            RebuildGizmoGeometry();
        }

        UpdateTransformMatrix();
    }

    public void Hide()
    {
        _currentTool = EditorToolType.None;
        _gizmoGroup.Children.Clear();
        _handleMap.Clear();
    }

    private void UpdateTransformMatrix()
    {
        var tg = new Transform3DGroup();
        tg.Children.Add(new ScaleTransform3D(_scale, _scale, _scale));
        tg.Children.Add(new RotateTransform3D(new QuaternionRotation3D(_rotation)));
        tg.Children.Add(new TranslateTransform3D(_position.X, _position.Y, _position.Z));
        Transform = tg;
    }

    private void RebuildGizmoGeometry()
    {
        _gizmoGroup.Children.Clear();
        _handleMap.Clear();

        switch (_currentTool)
        {
            case EditorToolType.Translate:
                BuildTranslateGizmo();
                break;
            case EditorToolType.Rotate:
                BuildRotateGizmo();
                break;
            case EditorToolType.Scale:
                BuildScaleGizmo();
                break;
            case EditorToolType.Pivot:
                BuildPivotGizmo();
                break;
        }
    }

    private void BuildTranslateGizmo()
    {
        // 3 Arrows
        AddHandleModel(CreateArrow(new Point3D(0, 0, 0), new Point3D(2.0, 0, 0), 0.06, 0.45), Colors.Red, GizmoHandle.AxisX);
        AddHandleModel(CreateArrow(new Point3D(0, 0, 0), new Point3D(0, 2.0, 0), 0.06, 0.45), Colors.LimeGreen, GizmoHandle.AxisY);
        AddHandleModel(CreateArrow(new Point3D(0, 0, 0), new Point3D(0, 0, 2.0), 0.06, 0.45), Colors.DodgerBlue, GizmoHandle.AxisZ);

        // 3 Planar Quads (Double-sided)
        AddHandleModel(CreateQuad(new Point3D(0.4, 0.4, 0), new Point3D(1.1, 0.4, 0), new Point3D(1.1, 1.1, 0), new Point3D(0.4, 1.1, 0)), Color.FromArgb(170, 255, 230, 0), GizmoHandle.PlaneXY);
        AddHandleModel(CreateQuad(new Point3D(0.4, 0, 0.4), new Point3D(1.1, 0, 0.4), new Point3D(1.1, 0, 1.1), new Point3D(0.4, 0, 1.1)), Color.FromArgb(170, 0, 240, 255), GizmoHandle.PlaneXZ);
        AddHandleModel(CreateQuad(new Point3D(0, 0.4, 0.4), new Point3D(0, 1.1, 0.4), new Point3D(0, 1.1, 1.1), new Point3D(0, 0.4, 1.1)), Color.FromArgb(170, 255, 0, 200), GizmoHandle.PlaneYZ);

        // Center origin sphere
        AddHandleModel(CreateSphere(new Point3D(0, 0, 0), 0.14, 12), Colors.Gold, GizmoHandle.Center);
    }

    private void BuildRotateGizmo()
    {
        // 3 Rings around X, Y, Z
        AddHandleModel(CreateRing(new Point3D(0, 0, 0), 1.8, 0.04, 32, 8, new System.Windows.Media.Media3D.Vector3D(1, 0, 0)), Colors.Red, GizmoHandle.AxisX);
        AddHandleModel(CreateRing(new Point3D(0, 0, 0), 1.8, 0.04, 32, 8, new System.Windows.Media.Media3D.Vector3D(0, 1, 0)), Colors.LimeGreen, GizmoHandle.AxisY);
        AddHandleModel(CreateRing(new Point3D(0, 0, 0), 1.8, 0.04, 32, 8, new System.Windows.Media.Media3D.Vector3D(0, 0, 1)), Colors.DodgerBlue, GizmoHandle.AxisZ);

        AddHandleModel(CreateSphere(new Point3D(0, 0, 0), 0.12, 12), Colors.White, GizmoHandle.Center);
    }

    private void BuildScaleGizmo()
    {
        // 3 Sticks with Cube handles
        AddHandleModel(CreateStickAndBox(new Point3D(0, 0, 0), new Point3D(1.9, 0, 0), 0.05, 0.28), Colors.Red, GizmoHandle.AxisX);
        AddHandleModel(CreateStickAndBox(new Point3D(0, 0, 0), new Point3D(0, 1.9, 0), 0.05, 0.28), Colors.LimeGreen, GizmoHandle.AxisY);
        AddHandleModel(CreateStickAndBox(new Point3D(0, 0, 0), new Point3D(0, 0, 1.9), 0.05, 0.28), Colors.DodgerBlue, GizmoHandle.AxisZ);

        // Center Uniform Box
        AddHandleModel(CreateBox(new Point3D(0, 0, 0), 0.32, 0.32, 0.32), Colors.Yellow, GizmoHandle.Center);
    }

    private void BuildPivotGizmo()
    {
        var geom = new MeshGeometry3D();
        AppendSphere(geom, new Point3D(0, 0, 0), 0.2, 14);
        AppendCylinder(geom, new Point3D(-0.9, 0, 0), new Point3D(0.9, 0, 0), 0.04, 8);
        AppendCylinder(geom, new Point3D(0, -0.9, 0), new Point3D(0, 0.9, 0), 0.04, 8);
        AppendCylinder(geom, new Point3D(0, 0, -0.9), new Point3D(0, 0, 0.9), 0.04, 8);
        AddHandleModel(geom, Colors.DarkOrange, GizmoHandle.Center);
    }

    private void AddHandleModel(
        MeshGeometry3D geometry,
        Color color,
        GizmoHandle handle)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var mat = new DiffuseMaterial(brush);
        mat.Freeze();

        var model = new GeometryModel3D
        {
            Geometry = geometry,
            Material = mat,
            BackMaterial = mat
        };

        _handleMap[model] = handle;
        _gizmoGroup.Children.Add(model);
    }

    // --- Native WPF Mesh Generators ---

    private static MeshGeometry3D CreateArrow(Point3D start, Point3D end, double radius, double tipLength)
    {
        var geom = new MeshGeometry3D();
        var dir = end - start;
        double len = dir.Length;
        if (len <= 0.001) return geom;

        dir.Normalize();
        var shaftEnd = end - dir * tipLength;

        AppendCylinder(geom, start, shaftEnd, radius, 10);
        AppendCone(geom, shaftEnd, end, radius * 2.5, 10);
        return geom;
    }

    private static MeshGeometry3D CreateStickAndBox(Point3D start, Point3D end, double radius, double boxSize)
    {
        var geom = new MeshGeometry3D();
        var dir = end - start;
        if (dir.Length <= 0.001) return geom;

        AppendCylinder(geom, start, end, radius, 10);
        AppendBox(geom, end, boxSize, boxSize, boxSize);
        return geom;
    }

    private static MeshGeometry3D CreateBox(Point3D center, double sx, double sy, double sz)
    {
        var geom = new MeshGeometry3D();
        AppendBox(geom, center, sx, sy, sz);
        return geom;
    }

    private static MeshGeometry3D CreateQuad(Point3D p0, Point3D p1, Point3D p2, Point3D p3)
    {
        var geom = new MeshGeometry3D();
        int b = geom.Positions.Count;
        geom.Positions.Add(p0);
        geom.Positions.Add(p1);
        geom.Positions.Add(p2);
        geom.Positions.Add(p3);

        // Front
        geom.TriangleIndices.Add(b + 0);
        geom.TriangleIndices.Add(b + 1);
        geom.TriangleIndices.Add(b + 2);
        geom.TriangleIndices.Add(b + 0);
        geom.TriangleIndices.Add(b + 2);
        geom.TriangleIndices.Add(b + 3);

        // Back
        geom.TriangleIndices.Add(b + 2);
        geom.TriangleIndices.Add(b + 1);
        geom.TriangleIndices.Add(b + 0);
        geom.TriangleIndices.Add(b + 3);
        geom.TriangleIndices.Add(b + 2);
        geom.TriangleIndices.Add(b + 0);

        return geom;
    }

    private static MeshGeometry3D CreateSphere(Point3D center, double radius, int segments)
    {
        var geom = new MeshGeometry3D();
        AppendSphere(geom, center, radius, segments);
        return geom;
    }

    private static MeshGeometry3D CreateRing(Point3D center, double radius, double tubeRadius, int circleSegments, int tubeSegments, System.Windows.Media.Media3D.Vector3D normal)
    {
        var geom = new MeshGeometry3D();
        normal.Normalize();

        // Find orthogonal axes
        var u = Math.Abs(normal.X) < 0.9 ? System.Windows.Media.Media3D.Vector3D.CrossProduct(normal, new System.Windows.Media.Media3D.Vector3D(1, 0, 0)) : System.Windows.Media.Media3D.Vector3D.CrossProduct(normal, new System.Windows.Media.Media3D.Vector3D(0, 1, 0));
        u.Normalize();
        var v = System.Windows.Media.Media3D.Vector3D.CrossProduct(normal, u);
        v.Normalize();

        int startIndex = geom.Positions.Count;

        for (int i = 0; i <= circleSegments; i++)
        {
            double theta = (i % circleSegments) * 2.0 * Math.PI / circleSegments;
            var circleCenter = center + (u * Math.Cos(theta) + v * Math.Sin(theta)) * radius;
            var radial = circleCenter - center;
            radial.Normalize();

            for (int j = 0; j <= tubeSegments; j++)
            {
                double phi = (j % tubeSegments) * 2.0 * Math.PI / tubeSegments;
                var pt = circleCenter + (radial * Math.Cos(phi) + normal * Math.Sin(phi)) * tubeRadius;
                geom.Positions.Add(pt);
            }
        }

        for (int i = 0; i < circleSegments; i++)
        {
            for (int j = 0; j < tubeSegments; j++)
            {
                int current = startIndex + i * (tubeSegments + 1) + j;
                int next = startIndex + (i + 1) * (tubeSegments + 1) + j;

                geom.TriangleIndices.Add(current);
                geom.TriangleIndices.Add(next);
                geom.TriangleIndices.Add(current + 1);

                geom.TriangleIndices.Add(next);
                geom.TriangleIndices.Add(next + 1);
                geom.TriangleIndices.Add(current + 1);
            }
        }

        return geom;
    }

    private static void AppendBox(MeshGeometry3D geom, Point3D center, double sx, double sy, double sz)
    {
        double hx = sx * 0.5, hy = sy * 0.5, hz = sz * 0.5;
        int b = geom.Positions.Count;

        Point3D[] corners = [
            new(center.X - hx, center.Y - hy, center.Z - hz),
            new(center.X + hx, center.Y - hy, center.Z - hz),
            new(center.X + hx, center.Y + hy, center.Z - hz),
            new(center.X - hx, center.Y + hy, center.Z - hz),
            new(center.X - hx, center.Y - hy, center.Z + hz),
            new(center.X + hx, center.Y - hy, center.Z + hz),
            new(center.X + hx, center.Y + hy, center.Z + hz),
            new(center.X - hx, center.Y + hy, center.Z + hz)
        ];

        foreach (var c in corners) geom.Positions.Add(c);

        int[] indices = [
            0,2,1, 0,3,2, // -Z
            4,5,6, 4,6,7, // +Z
            0,1,5, 0,5,4, // -Y
            2,3,7, 2,7,6, // +Y
            0,4,7, 0,7,3, // -X
            1,2,6, 1,6,5  // +X
        ];

        foreach (var idx in indices) geom.TriangleIndices.Add(b + idx);
    }

    private static void AppendCylinder(MeshGeometry3D geom, Point3D p1, Point3D p2, double radius, int segments)
    {
        var axis = p2 - p1;
        double len = axis.Length;
        if (len <= 0.0001) return;

        axis.Normalize();
        var u = Math.Abs(axis.X) < 0.9 ? System.Windows.Media.Media3D.Vector3D.CrossProduct(axis, new System.Windows.Media.Media3D.Vector3D(1, 0, 0)) : System.Windows.Media.Media3D.Vector3D.CrossProduct(axis, new System.Windows.Media.Media3D.Vector3D(0, 1, 0));
        u.Normalize();
        var v = System.Windows.Media.Media3D.Vector3D.CrossProduct(axis, u);
        v.Normalize();

        int b = geom.Positions.Count;

        for (int i = 0; i <= segments; i++)
        {
            double angle = (i % segments) * 2.0 * Math.PI / segments;
            var offset = (u * Math.Cos(angle) + v * Math.Sin(angle)) * radius;

            geom.Positions.Add(p1 + offset);
            geom.Positions.Add(p2 + offset);
        }

        for (int i = 0; i < segments; i++)
        {
            int i0 = b + i * 2;
            int i1 = b + i * 2 + 1;
            int i2 = b + (i + 1) * 2;
            int i3 = b + (i + 1) * 2 + 1;

            geom.TriangleIndices.Add(i0);
            geom.TriangleIndices.Add(i2);
            geom.TriangleIndices.Add(i1);

            geom.TriangleIndices.Add(i1);
            geom.TriangleIndices.Add(i2);
            geom.TriangleIndices.Add(i3);
        }
    }

    private static void AppendCone(MeshGeometry3D geom, Point3D baseCenter, Point3D tip, double baseRadius, int segments)
    {
        var axis = tip - baseCenter;
        if (axis.Length <= 0.0001) return;

        axis.Normalize();
        var u = Math.Abs(axis.X) < 0.9 ? System.Windows.Media.Media3D.Vector3D.CrossProduct(axis, new System.Windows.Media.Media3D.Vector3D(1, 0, 0)) : System.Windows.Media.Media3D.Vector3D.CrossProduct(axis, new System.Windows.Media.Media3D.Vector3D(0, 1, 0));
        u.Normalize();
        var v = System.Windows.Media.Media3D.Vector3D.CrossProduct(axis, u);
        v.Normalize();

        int tipIdx = geom.Positions.Count;
        geom.Positions.Add(tip);

        int baseStart = geom.Positions.Count;
        for (int i = 0; i <= segments; i++)
        {
            double angle = (i % segments) * 2.0 * Math.PI / segments;
            var offset = (u * Math.Cos(angle) + v * Math.Sin(angle)) * baseRadius;
            geom.Positions.Add(baseCenter + offset);
        }

        for (int i = 0; i < segments; i++)
        {
            geom.TriangleIndices.Add(tipIdx);
            geom.TriangleIndices.Add(baseStart + i);
            geom.TriangleIndices.Add(baseStart + i + 1);
        }
    }

    private static void AppendSphere(MeshGeometry3D geom, Point3D center, double radius, int segments)
    {
        int startIndex = geom.Positions.Count;

        for (int lat = 0; lat <= segments; lat++)
        {
            double theta = lat * Math.PI / segments;
            double sinTheta = Math.Sin(theta);
            double cosTheta = Math.Cos(theta);

            for (int lon = 0; lon <= segments; lon++)
            {
                double phi = lon * 2.0 * Math.PI / segments;
                double sinPhi = Math.Sin(phi);
                double cosPhi = Math.Cos(phi);

                var dir = new System.Windows.Media.Media3D.Vector3D(cosPhi * sinTheta, cosTheta, sinPhi * sinTheta);
                geom.Positions.Add(center + dir * radius);
            }
        }

        for (int lat = 0; lat < segments; lat++)
        {
            for (int lon = 0; lon < segments; lon++)
            {
                int first = startIndex + lat * (segments + 1) + lon;
                int second = first + segments + 1;

                geom.TriangleIndices.Add(first);
                geom.TriangleIndices.Add(second);
                geom.TriangleIndices.Add(first + 1);

                geom.TriangleIndices.Add(second);
                geom.TriangleIndices.Add(second + 1);
                geom.TriangleIndices.Add(first + 1);
            }
        }
    }
}
