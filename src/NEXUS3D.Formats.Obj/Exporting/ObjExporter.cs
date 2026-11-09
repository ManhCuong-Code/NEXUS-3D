using System.Diagnostics;
using System.Globalization;
using System.Text;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Geometry;

namespace NEXUS3D.Formats.Obj.Exporting;

/// <summary>
/// Contract for exporting an <see cref="ObjDocument"/> to Wavefront OBJ text streams or files.
/// </summary>
public interface IObjDocumentExporter
{
    Task<ObjExportResult> ExportFileAsync(
        ObjDocument document,
        string objFilePath,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<ObjExportResult> ExportAsync(
        ObjDocument document,
        Stream outputStream,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default);

    ObjExportResult Export(
        ObjDocument document,
        TextWriter writer,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exports an <see cref="ObjDocument"/> to standard 1-based Wavefront OBJ syntax.
/// Supports independent <c>v/vt/vn</c> indexing, optional N-gon preservation or Ear-Clipping triangulation,
/// configurable floating-point precision, and <c>o</c>/<c>g</c>/<c>s</c>/<c>usemtl</c> state grouping.
/// </summary>
public sealed class ObjExporter : IObjDocumentExporter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<ObjExportResult> ExportFileAsync(
        ObjDocument document,
        string objFilePath,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(objFilePath);
        options ??= ObjExportOptions.Default;

        if (File.Exists(objFilePath) && !options.AllowOverwrite)
        {
            throw new ObjException(
                ObjErrorCodes.ExportFailed,
                $"Target OBJ file '{objFilePath}' already exists and AllowOverwrite is false.");
        }

        string? dir = Path.GetDirectoryName(objFilePath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var fs = new FileStream(
            objFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 65536,
            useAsync: true);

        ObjExportResult result = await ExportAsync(document, fs, options, cancellationToken).ConfigureAwait(false);
        return new ObjExportResult
        {
            Succeeded = result.Succeeded,
            ObjFilePath = objFilePath,
            MtlFilePath = result.MtlFilePath,
            CopiedTexturePaths = result.CopiedTexturePaths,
            VertexCount = result.VertexCount,
            TextureCoordCount = result.TextureCoordCount,
            NormalCount = result.NormalCount,
            FaceCount = result.FaceCount,
            MaterialCount = result.MaterialCount,
            Issues = result.Issues,
            Elapsed = result.Elapsed
        };
    }

    public async Task<ObjExportResult> ExportAsync(
        ObjDocument document,
        Stream outputStream,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(outputStream);
        await Task.Yield();

        await using var writer = new StreamWriter(
            outputStream,
            Utf8NoBom,
            bufferSize: 65536,
            leaveOpen: true);

        ObjExportResult result = Export(document, writer, options, cancellationToken);
        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public ObjExportResult Export(
        ObjDocument document,
        TextWriter writer,
        ObjExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(writer);
        options ??= ObjExportOptions.Default;

        var sw = Stopwatch.StartNew();
        var issues = new List<ValidationIssue>();

        // 1. Header comments
        if (options.WriteComments)
        {
            writer.WriteLine("# NEXUS 3D - Wavefront OBJ Exporter");
            for (int i = 0; i < document.HeaderComments.Count; i++)
            {
                string comment = document.HeaderComments[i];
                if (comment.StartsWith("NEXUS 3D - Wavefront OBJ Exporter", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                writer.Write("# ");
                writer.WriteLine(comment);
            }
        }

        // 2. Material libraries (mtllib)
        if (!string.IsNullOrWhiteSpace(options.MtlFileName))
        {
            writer.Write("mtllib ");
            writer.WriteLine(options.MtlFileName);
        }
        else
        {
            for (int i = 0; i < document.MaterialLibraries.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(document.MaterialLibraries[i]))
                {
                    writer.Write("mtllib ");
                    writer.WriteLine(document.MaterialLibraries[i]);
                }
            }
        }

        // 3. Vertex positions (v x y z [w])
        int posCount = document.Positions.Count;
        for (int i = 0; i < posCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjVertex v = document.Positions[i];
            if (!v.IsFinite)
            {
                throw new ObjException(
                    ObjErrorCodes.InvalidVertex,
                    $"Cannot export non-finite vertex position at index {i}: ({v.X}, {v.Y}, {v.Z}, {v.W}).");
            }

            writer.Write("v ");
            writer.Write(options.FormatDouble(v.X));
            writer.Write(' ');
            writer.Write(options.FormatDouble(v.Y));
            writer.Write(' ');
            writer.Write(options.FormatDouble(v.Z));

            if (Math.Abs(v.W - 1.0) > MathUtilities.Epsilon)
            {
                writer.Write(' ');
                writer.Write(options.FormatDouble(v.W));
            }

            writer.WriteLine();
        }

        // 4. Texture coordinates (vt u [v] [w])
        int vtCount = options.WriteTexCoords ? document.TextureCoordinates.Count : 0;
        if (options.WriteTexCoords)
        {
            for (int i = 0; i < vtCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ObjTextureCoordinate vt = document.TextureCoordinates[i];
                if (!vt.IsFinite)
                {
                    throw new ObjException(
                        ObjErrorCodes.InvalidTexCoord,
                        $"Cannot export non-finite texture coordinate at index {i}: ({vt.U}, {vt.V}, {vt.W}).");
                }

                writer.Write("vt ");
                writer.Write(options.FormatDouble(vt.U));
                if (vt.Dimension >= 2)
                {
                    writer.Write(' ');
                    writer.Write(options.FormatDouble(vt.V));
                }

                if (vt.Dimension >= 3)
                {
                    writer.Write(' ');
                    writer.Write(options.FormatDouble(vt.W));
                }

                writer.WriteLine();
            }
        }

        // 5. Vertex normals (vn x y z)
        int vnCount = options.WriteNormals ? document.Normals.Count : 0;
        if (options.WriteNormals)
        {
            for (int i = 0; i < vnCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ObjNormal vn = document.Normals[i];
                if (!vn.IsFinite)
                {
                    throw new ObjException(
                        ObjErrorCodes.InvalidNormal,
                        $"Cannot export non-finite vertex normal at index {i}: ({vn.X}, {vn.Y}, {vn.Z}).");
                }

                writer.Write("vn ");
                writer.Write(options.FormatDouble(vn.X));
                writer.Write(' ');
                writer.Write(options.FormatDouble(vn.Y));
                writer.Write(' ');
                writer.Write(options.FormatDouble(vn.Z));
                writer.WriteLine();
            }
        }

        // 6. Parameter-space vertices (vp u [v] [w])
        for (int i = 0; i < document.ParameterVertices.Count; i++)
        {
            ObjParameterVertex vp = document.ParameterVertices[i];
            if (!vp.IsFinite)
            {
                throw new ObjException(
                    ObjErrorCodes.InvalidVertex,
                    $"Cannot export non-finite parameter vertex at index {i}.");
            }

            writer.Write("vp ");
            writer.Write(options.FormatDouble(vp.U));
            if (vp.Dimension >= 2)
            {
                writer.Write(' ');
                writer.Write(options.FormatDouble(vp.V));
            }

            if (vp.Dimension >= 3)
            {
                writer.Write(' ');
                writer.Write(options.FormatDouble(vp.W));
            }

            writer.WriteLine();
        }

        // 7. Faces (f), Polylines (l), and Points (p) with state tracking
        string? currentObject = null;
        IReadOnlyList<string> currentGroups = [];
        int? currentSmoothing = null;
        string? currentMaterial = null;
        bool hasAnySmoothingGroups = document.SmoothingGroups.Count > 0 || document.Faces.Any(f => f.SmoothingGroup != 0);

        int exportedFaceCount = 0;
        for (int i = 0; i < document.Faces.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjFace sourceFace = document.Faces[i];

            IReadOnlyList<ObjFace> facesToWrite = options.TriangulatePolygons && sourceFace.Vertices.Count > 3
                ? sourceFace.Triangulate(document.Positions)
                : [sourceFace];

            for (int fIdx = 0; fIdx < facesToWrite.Count; fIdx++)
            {
                ObjFace face = facesToWrite[fIdx];
                if (face.Vertices.Count < 3)
                {
                    issues.Add(new ValidationIssue(
                        ObjErrorCodes.InvalidFace,
                        $"Skipping face with fewer than 3 vertices at index {i}.",
                        ValidationSeverity.Warning));
                    continue;
                }

                EmitStateChanges(
                    writer,
                    face.ObjectName,
                    face.GroupNames,
                    face.SmoothingGroup,
                    face.MaterialName,
                    hasAnySmoothingGroups,
                    ref currentObject,
                    ref currentGroups,
                    ref currentSmoothing,
                    ref currentMaterial);

                writer.Write('f');
                for (int vIdx = 0; vIdx < face.Vertices.Count; vIdx++)
                {
                    ObjFaceVertex corner = face.Vertices[vIdx];
                    if (corner.PositionIndex < 0 || corner.PositionIndex >= posCount)
                    {
                        throw new ObjException(
                            ObjErrorCodes.IndexOutOfRange,
                            $"Face {i} vertex {vIdx} position index {corner.PositionIndex} is out of bounds (0..{posCount - 1}).");
                    }

                    int pos1 = corner.PositionIndex + 1;
                    int? uv1 = options.WriteTexCoords && corner.TextureCoordIndex.HasValue
                        ? corner.TextureCoordIndex.Value + 1
                        : null;
                    int? norm1 = options.WriteNormals && corner.NormalIndex.HasValue
                        ? corner.NormalIndex.Value + 1
                        : null;

                    if (uv1.HasValue && (uv1.Value < 1 || uv1.Value > vtCount))
                    {
                        throw new ObjException(
                            ObjErrorCodes.IndexOutOfRange,
                            $"Face {i} vertex {vIdx} UV index {uv1.Value} is out of bounds (1..{vtCount}).");
                    }

                    if (norm1.HasValue && (norm1.Value < 1 || norm1.Value > vnCount))
                    {
                        throw new ObjException(
                            ObjErrorCodes.IndexOutOfRange,
                            $"Face {i} vertex {vIdx} normal index {norm1.Value} is out of bounds (1..{vnCount}).");
                    }

                    writer.Write(' ');
                    writer.Write(pos1.ToString(CultureInfo.InvariantCulture));
                    if (uv1.HasValue && norm1.HasValue)
                    {
                        writer.Write('/');
                        writer.Write(uv1.Value.ToString(CultureInfo.InvariantCulture));
                        writer.Write('/');
                        writer.Write(norm1.Value.ToString(CultureInfo.InvariantCulture));
                    }
                    else if (uv1.HasValue)
                    {
                        writer.Write('/');
                        writer.Write(uv1.Value.ToString(CultureInfo.InvariantCulture));
                    }
                    else if (norm1.HasValue)
                    {
                        writer.Write("//");
                        writer.Write(norm1.Value.ToString(CultureInfo.InvariantCulture));
                    }
                }

                writer.WriteLine();
                exportedFaceCount++;
            }
        }

        // 8. Polylines (l)
        for (int i = 0; i < document.Polylines.Count; i++)
        {
            ObjPolyline line = document.Polylines[i];
            if (line.Vertices.Count < 2)
            {
                continue;
            }

            EmitStateChanges(
                writer,
                line.ObjectName,
                line.GroupNames,
                currentSmoothing ?? 0,
                line.MaterialName,
                emitSmoothing: false,
                ref currentObject,
                ref currentGroups,
                ref currentSmoothing,
                ref currentMaterial);

            writer.Write('l');
            for (int vIdx = 0; vIdx < line.Vertices.Count; vIdx++)
            {
                ObjPolylineVertex v = line.Vertices[vIdx];
                if (v.PositionIndex < 0 || v.PositionIndex >= posCount)
                {
                    throw new ObjException(
                        ObjErrorCodes.IndexOutOfRange,
                        $"Polyline {i} vertex {vIdx} position index {v.PositionIndex} is out of bounds.");
                }

                writer.Write(' ');
                writer.Write((v.PositionIndex + 1).ToString(CultureInfo.InvariantCulture));
                if (options.WriteTexCoords && v.TextureCoordIndex.HasValue)
                {
                    writer.Write('/');
                    writer.Write((v.TextureCoordIndex.Value + 1).ToString(CultureInfo.InvariantCulture));
                }
            }

            writer.WriteLine();
        }

        // 9. Points (p)
        for (int i = 0; i < document.Points.Count; i++)
        {
            ObjPointElement pt = document.Points[i];
            if (pt.PositionIndices.Count == 0)
            {
                continue;
            }

            EmitStateChanges(
                writer,
                pt.ObjectName,
                pt.GroupNames,
                currentSmoothing ?? 0,
                currentMaterial,
                emitSmoothing: false,
                ref currentObject,
                ref currentGroups,
                ref currentSmoothing,
                ref currentMaterial);

            writer.Write('p');
            for (int vIdx = 0; vIdx < pt.PositionIndices.Count; vIdx++)
            {
                int posIdx = pt.PositionIndices[vIdx];
                if (posIdx < 0 || posIdx >= posCount)
                {
                    throw new ObjException(
                        ObjErrorCodes.IndexOutOfRange,
                        $"Point element {i} position index {posIdx} is out of bounds.");
                }

                writer.Write(' ');
                writer.Write((posIdx + 1).ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteLine();
        }

        // 10. Unrecognized statements preserved from original document
        if (options.WriteUnrecognizedStatements)
        {
            for (int i = 0; i < document.UnrecognizedStatements.Count; i++)
            {
                writer.WriteLine(document.UnrecognizedStatements[i].RawLine);
            }
        }

        sw.Stop();
        return new ObjExportResult
        {
            Succeeded = true,
            VertexCount = posCount,
            TextureCoordCount = vtCount,
            NormalCount = vnCount,
            FaceCount = exportedFaceCount,
            Issues = issues,
            Elapsed = sw.Elapsed
        };
    }

    private static void EmitStateChanges(
        TextWriter writer,
        string? targetObject,
        IReadOnlyList<string> targetGroups,
        int targetSmoothing,
        string? targetMaterial,
        bool emitSmoothing,
        ref string? currentObject,
        ref IReadOnlyList<string> currentGroups,
        ref int? currentSmoothing,
        ref string? currentMaterial)
    {
        if (!string.Equals(targetObject, currentObject, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(targetObject))
            {
                writer.Write("o ");
                writer.WriteLine(targetObject);
            }

            currentObject = targetObject;
        }

        if (!GroupListsEqual(targetGroups, currentGroups))
        {
            if (targetGroups.Count > 0)
            {
                writer.Write("g ");
                writer.WriteLine(string.Join(' ', targetGroups));
            }
            else if (currentGroups.Count > 0)
            {
                writer.WriteLine("g default");
            }

            currentGroups = targetGroups;
        }

        if (emitSmoothing && currentSmoothing != targetSmoothing)
        {
            if (targetSmoothing <= 0)
            {
                writer.WriteLine("s off");
            }
            else
            {
                writer.Write("s ");
                writer.WriteLine(targetSmoothing.ToString(CultureInfo.InvariantCulture));
            }

            currentSmoothing = targetSmoothing;
        }

        if (!string.Equals(targetMaterial, currentMaterial, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(targetMaterial))
            {
                writer.Write("usemtl ");
                writer.WriteLine(targetMaterial);
            }

            currentMaterial = targetMaterial;
        }
    }

    private static bool GroupListsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}

