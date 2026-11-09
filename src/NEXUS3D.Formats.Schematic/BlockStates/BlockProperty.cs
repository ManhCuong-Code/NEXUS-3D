namespace NEXUS3D.Formats.Schematic.BlockStates;

/// <summary>
/// Represents a single key-value property of a Minecraft block state (e.g., <c>facing=north</c>).
/// </summary>
public readonly record struct BlockProperty(string Key, string Value)
{
    public override string ToString() => $"{Key}={Value}";
}

