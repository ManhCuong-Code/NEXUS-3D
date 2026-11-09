using System.Text;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Formats.Schematic.BlockStates;

/// <summary>
/// Formats a <see cref="BlockState"/> into a canonical, deterministic string with sorted properties.
/// Never drops unknown properties or alters namespaces.
/// </summary>
public static class BlockStateFormatter
{
    public static string Format(BlockState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        string ns = string.IsNullOrWhiteSpace(state.Namespace) || state.Namespace == "default"
            ? ResourceLocation.DefaultNamespace
            : state.Namespace;

        var sb = new StringBuilder();
        sb.Append(ns).Append(':').Append(state.Name);

        if (state.Properties.Count > 0)
        {
            sb.Append('[');
            bool first = true;
            foreach (var kvp in state.Properties.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (!first)
                {
                    sb.Append(',');
                }
                sb.Append(kvp.Key).Append('=').Append(kvp.Value);
                first = false;
            }
            sb.Append(']');
        }

        return sb.ToString();
    }
}

