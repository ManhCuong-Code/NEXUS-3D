using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Geometry;

namespace NEXUS3D.Formats.Obj.Parsing;

/// <summary>
/// Contract for parsing a Wavefront OBJ stream into an intermediate <see cref="ObjDocument"/>.
/// </summary>
public interface IObjDocumentParser
{
    Task<ObjDocument> ParseDocumentAsync(
        Stream source,
        ObjParseOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Full Wavefront OBJ document parser supporting vertices (<c>v</c> with optional <c>w</c>),
/// texture coordinates (<c>vt</c> 1D/2D/3D), normals (<c>vn</c>), parameter vertices (<c>vp</c>),
/// triangles/quads/N-gons (<c>f</c>) with positive and negative indices, polylines (<c>l</c>),
/// points (<c>p</c>), objects (<c>o</c>), groups (<c>g</c>), smoothing groups (<c>s</c>),
/// material references (<c>mtllib</c>, <c>usemtl</c>), and optional normal recalculation.
/// </summary>
public sealed class ObjParser : IObjDocumentParser
{
    private readonly ObjLexer _lexer = new();
    private readonly ObjLineParser _lineParser = new();

    public async Task<ObjDocument> ParseDocumentAsync(
        Stream source,
        ObjParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var effectiveOptions = options ?? ObjParseOptions.Default;

        var document = new ObjDocument
        {
            SourceFileName = !string.IsNullOrWhiteSpace(effectiveOptions.SourceFilePath)
                ? Path.GetFileName(effectiveOptions.SourceFilePath)
                : null
        };

        var state = new ObjParserState();
        bool anyLineRead = false;

        await foreach (var line in _lexer.TokenizeLinesAsync(
                           source,
                           effectiveOptions.MaxFileSizeBytes,
                           effectiveOptions.MaxLineLength,
                           cancellationToken).ConfigureAwait(false))
        {
            anyLineRead = true;
            _lineParser.ParseStatement(line, document, state, effectiveOptions);
        }

        if (!anyLineRead || !document.HasGeometry)
        {
            throw new ObjException(
                ObjErrorCodes.EmptyOrUnrecognizedFile,
                "OBJ stream is empty or contains no geometric elements (v, f, l, p).");
        }

        if (effectiveOptions.ForceRecalculateNormals ||
            (effectiveOptions.RecalculateMissingNormals && document.Normals.Count == 0))
        {
            RecalculateNormals(document, effectiveOptions.ForceRecalculateNormals);
        }

        return document;
    }

    /// <summary>
    /// Computes face and smooth-group vertex normals respecting polygon winding order when requested by options.
    /// Never overwrites existing file normals unless <paramref name="forceOverwrite"/> is true.
    /// </summary>
    public static void RecalculateNormals(ObjDocument document, bool forceOverwrite)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Faces.Count == 0)
        {
            return;
        }

        if (forceOverwrite)
        {
            document.Normals.Clear();
        }

        // Compute face normals first
        var faceNormals = new Vector3D[document.Faces.Count];
        for (int f = 0; f < document.Faces.Count; f++)
        {
            faceNormals[f] = document.Faces[f].ComputeFaceNormal(document.Positions);
        }

        // Accumulate smooth normals per (smoothingGroup > 0, positionIndex)
        var smoothAccumulators = new Dictionary<(int SmoothGroup, int PosIdx), Vector3D>();
        for (int f = 0; f < document.Faces.Count; f++)
        {
            var face = document.Faces[f];
            if (face.SmoothingGroup <= 0)
            {
                continue;
            }

            Vector3D fn = faceNormals[f];
            for (int v = 0; v < face.Vertices.Count; v++)
            {
                var key = (face.SmoothingGroup, face.Vertices[v].PositionIndex);
                smoothAccumulators[key] = smoothAccumulators.TryGetValue(key, out var acc) ? acc + fn : fn;
            }
        }

        var normalIndexLookup = new Dictionary<Vector3D, int>();
        for (int i = 0; i < document.Normals.Count; i++)
        {
            normalIndexLookup.TryAdd(document.Normals[i].ToVector3D(), i);
        }

        int GetOrAddNormal(Vector3D n)
        {
            Vector3D unit = n.LengthSquared() > MathUtilities.Epsilon ? n.Normalize() : Vector3D.UnitY;
            if (normalIndexLookup.TryGetValue(unit, out int idx))
            {
                return idx;
            }

            int newIdx = document.Normals.Count;
            document.Normals.Add(new ObjNormal(unit.X, unit.Y, unit.Z));
            normalIndexLookup[unit] = newIdx;
            return newIdx;
        }

        for (int f = 0; f < document.Faces.Count; f++)
        {
            var face = document.Faces[f];
            var updatedCorners = new ObjFaceVertex[face.Vertices.Count];
            bool changed = false;

            for (int c = 0; c < face.Vertices.Count; c++)
            {
                var corner = face.Vertices[c];
                if (!forceOverwrite && corner.NormalIndex.HasValue)
                {
                    updatedCorners[c] = corner;
                    continue;
                }

                Vector3D targetNormal = face.SmoothingGroup > 0 &&
                                        smoothAccumulators.TryGetValue((face.SmoothingGroup, corner.PositionIndex), out var smoothN)
                    ? smoothN
                    : faceNormals[f];

                int normIdx = GetOrAddNormal(targetNormal);
                updatedCorners[c] = new ObjFaceVertex(corner.PositionIndex, corner.TextureCoordIndex, normIdx);
                changed = true;
            }

            if (changed)
            {
                document.Faces[f] = new ObjFace
                {
                    Vertices = updatedCorners,
                    ObjectName = face.ObjectName,
                    GroupNames = face.GroupNames,
                    SmoothingGroup = face.SmoothingGroup,
                    MaterialName = face.MaterialName,
                    SourceLineNumber = face.SourceLineNumber
                };
            }
        }
    }
}
