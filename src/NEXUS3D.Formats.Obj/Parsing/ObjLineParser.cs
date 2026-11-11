using System.Globalization;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Geometry;

namespace NEXUS3D.Formats.Obj.Parsing;

/// <summary>
/// Mutable parser state tracking active <c>o</c> (object), <c>g</c> (groups), <c>s</c> (smoothing group),
/// and <c>usemtl</c> (material) assignments while streaming through an OBJ document.
/// </summary>
public sealed class ObjParserState
{
    public string? CurrentObjectName { get; set; }
    public List<string> CurrentGroupNames { get; set; } = ["default"];
    public int CurrentSmoothingGroup { get; set; }
    public string? CurrentMaterialName { get; set; }
}

/// <summary>
/// Parses individual logical OBJ statements (<c>v</c>, <c>vt</c>, <c>vn</c>, <c>vp</c>, <c>f</c>,
/// <c>l</c>, <c>p</c>, <c>o</c>, <c>g</c>, <c>s</c>, <c>mtllib</c>, <c>usemtl</c>) into an <see cref="ObjDocument"/>.
/// </summary>
public sealed class ObjLineParser
{
    public void ParseStatement(
        ObjLexedLine line,
        ObjDocument document,
        ObjParserState state,
        ObjParseOptions options)
    {
        if (line.IsCommentOnly)
        {
            if (!string.IsNullOrWhiteSpace(line.Comment) && !document.HasGeometry)
            {
                document.HeaderComments.Add(line.Comment);
            }
            return;
        }

        ReadOnlySpan<char> content = line.Content.AsSpan().Trim();
        if (content.IsEmpty)
        {
            return;
        }

        SplitFirstToken(content, out ReadOnlySpan<char> keywordSpan, out ReadOnlySpan<char> argsSpan);
        string keyword = keywordSpan.ToString();

        switch (keyword)
        {
            case "v":
                ParseVertex(argsSpan, line.LineNumber, document, options);
                break;

            case "vt":
                ParseTextureCoordinate(argsSpan, line.LineNumber, document);
                break;

            case "vn":
                ParseNormal(argsSpan, line.LineNumber, document, options);
                break;

            case "vp":
                ParseParameterVertex(argsSpan, line.LineNumber, document);
                break;

            case "f":
                ParseFace(argsSpan, line.LineNumber, document, state, options);
                break;

            case "l":
                ParsePolyline(argsSpan, line.LineNumber, document, state);
                break;

            case "p":
                ParsePoint(argsSpan, line.LineNumber, document, state);
                break;

            case "o":
                ParseObject(argsSpan, line.LineNumber, document, state);
                break;

            case "g":
                ParseGroup(argsSpan, document, state);
                break;

            case "s":
                ParseSmoothingGroup(argsSpan, line.LineNumber, document, state);
                break;

            case "mtllib":
                ParseMtlLib(argsSpan, line.LineNumber, document);
                break;

            case "usemtl":
                ParseUseMtl(argsSpan, line.LineNumber, state);
                break;

            default:
                if (options.PreserveUnrecognizedStatements)
                {
                    document.UnrecognizedStatements.Add(new ObjUnrecognizedStatement(line.LineNumber, keyword, line.Content));
                }

                document.Warnings.Add(new WarningRecord
                {
                    Code = ObjErrorCodes.UnsupportedStatement,
                    Message = $"Line {line.LineNumber}: Unrecognized or unsupported OBJ statement '{keyword}' was preserved as raw metadata.",
                    SourceComponent = "ObjLineParser"
                });
                break;
        }
    }

    private static void ParseVertex(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document,
        ObjParseOptions options)
    {
        if (document.Positions.Count >= options.MaxVertexCount)
        {
            throw new ObjException(
                ObjErrorCodes.LimitExceeded,
                $"Vertex count exceeds MaxVertexCount ({options.MaxVertexCount}).",
                lineNumber);
        }

        Span<double> values = stackalloc double[8];
        int count = ParseDoubles(args, values, lineNumber, ObjErrorCodes.InvalidVertex, "vertex 'v'");

        if (count < 3)
        {
            throw new ObjException(
                ObjErrorCodes.InvalidVertex,
                $"Vertex 'v' requires at least 3 coordinates (x y z [w] [r g b [a]]), but found {count}.",
                lineNumber);
        }

        double x = values[0];
        double y = values[1];
        double z = values[2];
        double w = 1.0;
        NEXUS3D.Domain.Mathematics.Vector4D? color = null;

        if (count == 4)
        {
            w = values[3];
        }
        else if (count == 6)
        {
            // v x y z r g b
            color = new NEXUS3D.Domain.Mathematics.Vector4D(values[3], values[4], values[5], 1.0);
        }
        else if (count == 7)
        {
            // v x y z r g b a
            color = new NEXUS3D.Domain.Mathematics.Vector4D(values[3], values[4], values[5], values[6]);
        }
        else if (count >= 8)
        {
            // v x y z w r g b a
            w = values[3];
            color = new NEXUS3D.Domain.Mathematics.Vector4D(values[4], values[5], values[6], values[7]);
        }

        var vertex = new ObjVertex(x, y, z, w, lineNumber, color);

        if (!vertex.IsFinite)
        {
            throw new ObjException(
                ObjErrorCodes.InvalidVertex,
                "Vertex 'v' coordinates must be finite and W cannot be zero.",
                lineNumber);
        }

        document.Positions.Add(vertex);
    }

    private static void ParseTextureCoordinate(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document)
    {
        Span<double> values = stackalloc double[3];
        int count = ParseDoubles(args, values, lineNumber, ObjErrorCodes.InvalidTexCoord, "texture coordinate 'vt'");

        if (count < 1 || count > 3)
        {
            throw new ObjException(
                ObjErrorCodes.InvalidTexCoord,
                $"Texture coordinate 'vt' requires 1, 2, or 3 components (u [v] [w]), but found {count}.",
                lineNumber);
        }

        double u = values[0];
        double v = count >= 2 ? values[1] : 0.0;
        double w = count == 3 ? values[2] : 0.0;

        var texCoord = new ObjTextureCoordinate(u, v, w, count, lineNumber);
        if (!texCoord.IsFinite)
        {
            throw new ObjException(
                ObjErrorCodes.InvalidTexCoord,
                "Texture coordinate 'vt' contains non-finite values.",
                lineNumber);
        }

        document.TextureCoordinates.Add(texCoord);
    }

    private static void ParseNormal(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document,
        ObjParseOptions options)
    {
        Span<double> values = stackalloc double[4];
        int count = ParseDoubles(args, values, lineNumber, ObjErrorCodes.InvalidNormal, "vertex normal 'vn'");

        if (count != 3)
        {
            throw new ObjException(
                ObjErrorCodes.InvalidNormal,
                $"Vertex normal 'vn' requires exactly 3 components (x y z), but found {count}.",
                lineNumber);
        }

        var normal = new ObjNormal(values[0], values[1], values[2], lineNumber);
        if (!normal.IsFinite)
        {
            throw new ObjException(
                ObjErrorCodes.InvalidNormal,
                "Vertex normal 'vn' contains NaN or Infinity.",
                lineNumber);
        }

        if (normal.IsZero)
        {
            if (options.StrictValidation)
            {
                throw new ObjException(
                    ObjErrorCodes.InvalidNormal,
                    "Vertex normal 'vn' cannot be the zero vector (0, 0, 0).",
                    lineNumber);
            }

            document.Warnings.Add(new WarningRecord
            {
                Code = ObjErrorCodes.InvalidNormal,
                Message = $"Line {lineNumber}: Zero-length normal vector encountered.",
                SourceComponent = "ObjLineParser"
            });
        }

        document.Normals.Add(normal);
    }

    private static void ParseParameterVertex(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document)
    {
        Span<double> values = stackalloc double[3];
        int count = ParseDoubles(args, values, lineNumber, ObjErrorCodes.InvalidVertex, "parameter vertex 'vp'");

        if (count < 1 || count > 3)
        {
            throw new ObjException(
                ObjErrorCodes.InvalidVertex,
                $"Parameter vertex 'vp' requires 1, 2, or 3 coordinates (u [v] [w]), but found {count}.",
                lineNumber);
        }

        double u = values[0];
        double v = count >= 2 ? values[1] : 0.0;
        double w = count == 3 ? values[2] : 1.0;

        var vp = new ObjParameterVertex(u, v, w, count, lineNumber);
        if (!vp.IsFinite)
        {
            throw new ObjException(
                ObjErrorCodes.InvalidVertex,
                "Parameter vertex 'vp' contains non-finite values.",
                lineNumber);
        }

        document.ParameterVertices.Add(vp);
    }

    private static void ParseFace(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document,
        ObjParserState state,
        ObjParseOptions options)
    {
        if (document.Faces.Count >= options.MaxFaceCount)
        {
            throw new ObjException(
                ObjErrorCodes.LimitExceeded,
                $"Face count exceeds MaxFaceCount ({options.MaxFaceCount}).",
                lineNumber);
        }

        string[] tokens = SplitTokens(args);
        if (tokens.Length < 3)
        {
            throw new ObjException(
                ObjErrorCodes.InvalidFace,
                $"Face 'f' requires at least 3 vertex corners, but found {tokens.Length}.",
                lineNumber);
        }

        var corners = new ObjFaceVertex[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            corners[i] = ParseFaceCorner(
                tokens[i],
                lineNumber,
                document.Positions.Count,
                document.TextureCoordinates.Count,
                document.Normals.Count);
        }

        // Check for duplicate position indices within the same polygon face
        var seenPos = new HashSet<int>();
        for (int i = 0; i < corners.Length; i++)
        {
            if (!seenPos.Add(corners[i].PositionIndex))
            {
                if (options.StrictValidation)
                {
                    throw new ObjException(
                        ObjErrorCodes.DegeneratePolygon,
                        $"Face 'f' contains duplicate vertex position index {corners[i].PositionIndex + 1}.",
                        lineNumber);
                }

                document.Warnings.Add(new WarningRecord
                {
                    Code = ObjErrorCodes.DegeneratePolygon,
                    Message = $"Line {lineNumber}: Face contains duplicate vertex position index {corners[i].PositionIndex + 1}.",
                    SourceComponent = "ObjLineParser"
                });
                break;
            }
        }

        var face = new ObjFace
        {
            Vertices = corners,
            ObjectName = state.CurrentObjectName,
            GroupNames = state.CurrentGroupNames.ToArray(),
            SmoothingGroup = state.CurrentSmoothingGroup,
            MaterialName = state.CurrentMaterialName,
            SourceLineNumber = lineNumber
        };

        if (options.TriangulatePolygons && face.Vertices.Count > 3)
        {
            var tris = face.Triangulate(document.Positions);
            for (int t = 0; t < tris.Count; t++)
            {
                AddFaceToDocument(document, tris[t], state);
            }
        }
        else
        {
            AddFaceToDocument(document, face, state);
        }
    }

    private static void AddFaceToDocument(ObjDocument document, ObjFace face, ObjParserState state)
    {
        int faceIndex = document.Faces.Count;
        document.Faces.Add(face);

        if (!string.IsNullOrWhiteSpace(state.CurrentObjectName))
        {
            if (!document.Objects.TryGetValue(state.CurrentObjectName, out var obj))
            {
                obj = new ObjObject(state.CurrentObjectName);
                document.Objects[state.CurrentObjectName] = obj;
            }
            obj.FaceIndices.Add(faceIndex);
        }

        foreach (string groupName in state.CurrentGroupNames)
        {
            if (!document.Groups.TryGetValue(groupName, out var grp))
            {
                grp = new ObjGroup(groupName);
                document.Groups[groupName] = grp;
            }
            grp.FaceIndices.Add(faceIndex);
        }

        if (!document.SmoothingGroups.TryGetValue(state.CurrentSmoothingGroup, out var sg))
        {
            sg = new ObjSmoothingGroup(state.CurrentSmoothingGroup);
            document.SmoothingGroups[state.CurrentSmoothingGroup] = sg;
        }
        sg.FaceIndices.Add(faceIndex);
    }

    private static ObjFaceVertex ParseFaceCorner(
        string token,
        int lineNumber,
        int positionCount,
        int texCoordCount,
        int normalCount)
    {
        string[] parts = token.Split('/');
        if (parts.Length > 3 || string.IsNullOrEmpty(parts[0]))
        {
            throw new ObjException(
                ObjErrorCodes.InvalidFace,
                $"Invalid face corner syntax '{token}'. Expected 'v', 'v/vt', 'v//vn', or 'v/vt/vn'.",
                lineNumber);
        }

        int posIdx = ResolveObjIndex(parts[0], positionCount, "vertex position (v)", lineNumber);
        int? uvIdx = null;
        int? normIdx = null;

        if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
        {
            uvIdx = ResolveObjIndex(parts[1], texCoordCount, "texture coordinate (vt)", lineNumber);
        }

        if (parts.Length == 3 && !string.IsNullOrEmpty(parts[2]))
        {
            normIdx = ResolveObjIndex(parts[2], normalCount, "vertex normal (vn)", lineNumber);
        }

        return new ObjFaceVertex(posIdx, uvIdx, normIdx);
    }

    private static void ParsePolyline(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document,
        ObjParserState state)
    {
        string[] tokens = SplitTokens(args);
        if (tokens.Length < 2)
        {
            throw new ObjException(
                ObjErrorCodes.SyntaxError,
                $"Polyline 'l' requires at least 2 vertices, but found {tokens.Length}.",
                lineNumber);
        }

        var verts = new ObjPolylineVertex[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            string[] parts = tokens[i].Split('/');
            if (parts.Length > 2 || string.IsNullOrEmpty(parts[0]))
            {
                throw new ObjException(
                    ObjErrorCodes.SyntaxError,
                    $"Invalid polyline vertex token '{tokens[i]}'.",
                    lineNumber);
            }

            int posIdx = ResolveObjIndex(parts[0], document.Positions.Count, "vertex position (v)", lineNumber);
            int? uvIdx = (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
                ? ResolveObjIndex(parts[1], document.TextureCoordinates.Count, "texture coordinate (vt)", lineNumber)
                : null;

            verts[i] = new ObjPolylineVertex(posIdx, uvIdx);
        }

        int polyIdx = document.Polylines.Count;
        document.Polylines.Add(new ObjPolyline
        {
            Vertices = verts,
            ObjectName = state.CurrentObjectName,
            GroupNames = state.CurrentGroupNames.ToArray(),
            MaterialName = state.CurrentMaterialName,
            SourceLineNumber = lineNumber
        });

        if (!string.IsNullOrWhiteSpace(state.CurrentObjectName))
        {
            if (!document.Objects.TryGetValue(state.CurrentObjectName, out var obj))
            {
                obj = new ObjObject(state.CurrentObjectName);
                document.Objects[state.CurrentObjectName] = obj;
            }
            obj.PolylineIndices.Add(polyIdx);
        }
    }

    private static void ParsePoint(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document,
        ObjParserState state)
    {
        string[] tokens = SplitTokens(args);
        if (tokens.Length < 1)
        {
            throw new ObjException(
                ObjErrorCodes.SyntaxError,
                "Point 'p' requires at least 1 vertex index.",
                lineNumber);
        }

        int[] indices = new int[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            indices[i] = ResolveObjIndex(tokens[i], document.Positions.Count, "vertex position (v)", lineNumber);
        }

        int ptIdx = document.Points.Count;
        document.Points.Add(new ObjPointElement
        {
            PositionIndices = indices,
            ObjectName = state.CurrentObjectName,
            GroupNames = state.CurrentGroupNames.ToArray(),
            SourceLineNumber = lineNumber
        });

        if (!string.IsNullOrWhiteSpace(state.CurrentObjectName))
        {
            if (!document.Objects.TryGetValue(state.CurrentObjectName, out var obj))
            {
                obj = new ObjObject(state.CurrentObjectName);
                document.Objects[state.CurrentObjectName] = obj;
            }
            obj.PointIndices.Add(ptIdx);
        }
    }

    private static void ParseObject(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document,
        ObjParserState state)
    {
        string name = args.Trim().ToString();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"Object_{lineNumber}";
        }

        state.CurrentObjectName = name;
        if (!document.Objects.ContainsKey(name))
        {
            document.Objects[name] = new ObjObject(name);
        }
    }

    private static void ParseGroup(
        ReadOnlySpan<char> args,
        ObjDocument document,
        ObjParserState state)
    {
        string[] names = SplitTokens(args);
        if (names.Length == 0)
        {
            names = ["default"];
        }

        state.CurrentGroupNames = names.Distinct(StringComparer.Ordinal).ToList();
        foreach (string name in state.CurrentGroupNames)
        {
            if (!document.Groups.ContainsKey(name))
            {
                document.Groups[name] = new ObjGroup(name);
            }
        }
    }

    private static void ParseSmoothingGroup(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document,
        ObjParserState state)
    {
        string raw = args.Trim().ToString();
        if (string.IsNullOrWhiteSpace(raw) ||
            string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(raw, "0", StringComparison.Ordinal) ||
            string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase))
        {
            state.CurrentSmoothingGroup = 0;
        }
        else if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int groupId) && groupId >= 0)
        {
            state.CurrentSmoothingGroup = groupId;
        }
        else
        {
            throw new ObjException(
                ObjErrorCodes.SyntaxError,
                $"Invalid smoothing group value '{raw}'. Expected non-negative integer or 'off'.",
                lineNumber);
        }

        if (!document.SmoothingGroups.ContainsKey(state.CurrentSmoothingGroup))
        {
            document.SmoothingGroups[state.CurrentSmoothingGroup] = new ObjSmoothingGroup(state.CurrentSmoothingGroup);
        }
    }

    private static void ParseMtlLib(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjDocument document)
    {
        string raw = args.Trim().ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ObjException(
                ObjErrorCodes.SyntaxError,
                "Statement 'mtllib' requires a material library filename.",
                lineNumber);
        }

        // Support both single filename with spaces (e.g., "my materials.mtl") and multiple space-separated .mtl files
        if (raw.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) &&
            raw.IndexOf(".mtl ", StringComparison.OrdinalIgnoreCase) < 0)
        {
            if (!document.MaterialLibraries.Contains(raw, StringComparer.OrdinalIgnoreCase))
            {
                document.MaterialLibraries.Add(raw);
            }
            return;
        }

        string[] files = SplitTokens(args);
        foreach (string file in files)
        {
            if (!document.MaterialLibraries.Contains(file, StringComparer.OrdinalIgnoreCase))
            {
                document.MaterialLibraries.Add(file);
            }
        }
    }

    private static void ParseUseMtl(
        ReadOnlySpan<char> args,
        int lineNumber,
        ObjParserState state)
    {
        string name = args.Trim().ToString();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ObjException(
                ObjErrorCodes.SyntaxError,
                "Statement 'usemtl' requires a material name.",
                lineNumber);
        }

        state.CurrentMaterialName = name;
    }

    /// <summary>
    /// Resolves a 1-based positive or negative relative OBJ index into a 0-based array index.
    /// </summary>
    public static int ResolveObjIndex(string rawToken, int currentElementCount, string elementKind, int lineNumber)
    {
        if (!int.TryParse(rawToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rawIdx))
        {
            throw new ObjException(
                ObjErrorCodes.InvalidFace,
                $"Invalid {elementKind} index '{rawToken}'. Expected non-zero integer.",
                lineNumber);
        }

        if (rawIdx == 0)
        {
            throw new ObjException(
                ObjErrorCodes.IndexOutOfRange,
                $"OBJ {elementKind} index cannot be 0 (OBJ indices are 1-based or negative relative).",
                lineNumber);
        }

        int resolved = rawIdx > 0
            ? rawIdx - 1
            : currentElementCount + rawIdx;

        if (resolved < 0 || resolved >= currentElementCount)
        {
            throw new ObjException(
                ObjErrorCodes.IndexOutOfRange,
                $"OBJ {elementKind} index {rawIdx} is out of bounds (available count = {currentElementCount}).",
                lineNumber);
        }

        return resolved;
    }

    private static int ParseDoubles(
        ReadOnlySpan<char> args,
        Span<double> destination,
        int lineNumber,
        string errorCode,
        string contextName)
    {
        int count = 0;
        ReadOnlySpan<char> remaining = args.Trim();

        while (!remaining.IsEmpty)
        {
            SplitFirstToken(remaining, out ReadOnlySpan<char> token, out remaining);
            if (token.IsEmpty)
            {
                break;
            }

            if (count >= destination.Length)
            {
                return count + 1; // Signal caller that too many components were provided
            }

            if (!double.TryParse(token, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double val))
            {
                throw new ObjException(
                    errorCode,
                    $"Invalid numeric value '{token}' in {contextName}.",
                    lineNumber);
            }

            destination[count++] = val;
        }

        return count;
    }

    private static void SplitFirstToken(
        ReadOnlySpan<char> input,
        out ReadOnlySpan<char> firstToken,
        out ReadOnlySpan<char> rest)
    {
        ReadOnlySpan<char> trimmed = input.TrimStart();
        int i = 0;
        while (i < trimmed.Length && !char.IsWhiteSpace(trimmed[i]))
        {
            i++;
        }

        firstToken = trimmed[..i];
        rest = trimmed[i..].TrimStart();
    }

    private static string[] SplitTokens(ReadOnlySpan<char> input) =>
        input.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
}

