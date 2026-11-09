namespace NEXUS3D.Formats.Obj.Geometry;

/// <summary>
/// Represents a single corner of an <see cref="ObjFace"/> with independent 0-based indices into
/// the document's <c>Positions</c> (<c>v</c>), <c>TextureCoordinates</c> (<c>vt</c>), and <c>Normals</c> (<c>vn</c>) arrays.
/// </summary>
public readonly record struct ObjFaceVertex(
    int PositionIndex,
    int? TextureCoordIndex = null,
    int? NormalIndex = null)
{
    public bool HasTextureCoord => TextureCoordIndex.HasValue;
    public bool HasNormal => NormalIndex.HasValue;
}

