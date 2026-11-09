using System.Text.Json;
using NEXUS3D.Domain.Metadata;
using NEXUS3D.Domain.Scene;
using NEXUS3D.Formats.Obj.Common;
using NEXUS3D.Formats.Obj.Geometry;
using NEXUS3D.Formats.Obj.Materials;

namespace NEXUS3D.Formats.Obj.Conversion;

/// <summary>
/// Serializable payload preserving Wavefront OBJ/MTL metadata and auxiliary elements inside <see cref="RawDataContainer"/>.
/// </summary>
public sealed class ObjPreservedPayload
{
    public List<string> MaterialLibraries { get; set; } = [];
    public List<string> ObjHeaderComments { get; set; } = [];
    public List<string> MtlHeaderComments { get; set; } = [];
    public List<ObjParameterVertex> ParameterVertices { get; set; } = [];
    public List<ObjPolyline> Polylines { get; set; } = [];
    public List<ObjPointElement> Points { get; set; } = [];
    public List<ObjUnrecognizedStatement> UnrecognizedStatements { get; set; } = [];
    public List<ObjVertex>? SharedPositions { get; set; }
    public List<ObjTextureCoordinate>? SharedTextureCoordinates { get; set; }
    public List<ObjNormal>? SharedNormals { get; set; }
}

/// <summary>
/// Preserves and restores Wavefront OBJ/MTL auxiliary statements (<c>vp</c>, <c>l</c>, <c>p</c>, comments, <c>mtllib</c>,
/// unrecognized lines, and shared index pools) in <see cref="Scene3D.Metadata"/>.
/// </summary>
public static class ObjDataPreserver
{
    public const string ContainerName = "wavefront.obj.preserved";
    public const string SourceFormat = "wavefront:obj";
    public const string SchemaVersion = "1.0";
    private const int MaxSharedPoolPreservationCount = 20_000;

    public static void Preserve(
        Scene3D scene,
        ObjDocument objDoc,
        MtlDocument? mtlDoc,
        LossReport lossReport)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(objDoc);
        ArgumentNullException.ThrowIfNull(lossReport);

        bool canStoreSharedPools =
            objDoc.Positions.Count + objDoc.TextureCoordinates.Count + objDoc.Normals.Count <= MaxSharedPoolPreservationCount;

        var payload = new ObjPreservedPayload
        {
            MaterialLibraries = [.. objDoc.MaterialLibraries],
            ObjHeaderComments = [.. objDoc.HeaderComments],
            MtlHeaderComments = mtlDoc is not null ? [.. mtlDoc.HeaderComments] : [],
            ParameterVertices = [.. objDoc.ParameterVertices],
            Polylines = [.. objDoc.Polylines],
            Points = [.. objDoc.Points],
            UnrecognizedStatements = [.. objDoc.UnrecognizedStatements],
            SharedPositions = canStoreSharedPools ? [.. objDoc.Positions] : null,
            SharedTextureCoordinates = canStoreSharedPools ? [.. objDoc.TextureCoordinates] : null,
            SharedNormals = canStoreSharedPools ? [.. objDoc.Normals] : null
        };

        JsonElement jsonElement = JsonSerializer.SerializeToElement(payload);
        var container = new RawDataContainer
        {
            Name = ContainerName,
            SourceFormat = SourceFormat,
            PayloadKind = RawPayloadKind.Json,
            SchemaVersion = SchemaVersion,
            JsonPayload = jsonElement
        };

        if (container.IsWithinSizeLimit())
        {
            scene.Metadata.PreservedRawData.RemoveAll(c => string.Equals(c.Name, ContainerName, StringComparison.Ordinal));
            scene.Metadata.PreservedRawData.Add(container);
        }

        if (objDoc.ParameterVertices.Count > 0)
        {
            lossReport.RecordPreservedRaw(
                "ParameterVertices (vp)",
                $"Preserved {objDoc.ParameterVertices.Count} parameter-space vertices (vp) in RawDataContainer.");
        }

        if (objDoc.Polylines.Count > 0)
        {
            lossReport.RecordPreservedRaw(
                "Polylines (l)",
                $"Preserved {objDoc.Polylines.Count} polyline elements (l) in RawDataContainer.");
        }

        if (objDoc.Points.Count > 0)
        {
            lossReport.RecordPreservedRaw(
                "Points (p)",
                $"Preserved {objDoc.Points.Count} point elements (p) in RawDataContainer.");
        }

        if (objDoc.UnrecognizedStatements.Count > 0)
        {
            lossReport.RecordPreservedRaw(
                "UnrecognizedStatements",
                $"Preserved {objDoc.UnrecognizedStatements.Count} custom/unrecognized OBJ lines in RawDataContainer.");
        }
    }

    public static ObjPreservedPayload? TryRestore(Scene3D scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        RawDataContainer? container = scene.Metadata.PreservedRawData
            .FirstOrDefault(c => string.Equals(c.Name, ContainerName, StringComparison.Ordinal));

        if (container?.JsonPayload is not { ValueKind: JsonValueKind.Object } json)
        {
            return null;
        }

        try
        {
            return json.Deserialize<ObjPreservedPayload>();
        }
        catch (Exception)
        {
            return null;
        }
    }
}

