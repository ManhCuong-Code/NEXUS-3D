using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.BlockStates;

/// <summary>
/// Deterministic character-by-character parser for Minecraft block state strings
/// such as <c>minecraft:oak_stairs[facing=north,half=bottom]</c> or <c>modid:custom_block[variant=blue]</c>.
/// </summary>
public static class BlockStateParser
{
    public static BlockState Parse(string stateString)
    {
        if (!TryParse(stateString, out var state, out string? errorMessage) || state is null)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidBlockState,
                errorMessage ?? $"Invalid block state string '{stateString}'.");
        }

        return state;
    }

    public static bool TryParse(string? stateString, out BlockState? result, out string? errorMessage)
    {
        result = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(stateString))
        {
            errorMessage = "Block state string cannot be null or whitespace.";
            return false;
        }

        string trimmed = stateString.Trim();
        int openBracket = trimmed.IndexOf('[');
        int closeBracket = trimmed.IndexOf(']');

        if (openBracket < 0 && closeBracket >= 0)
        {
            errorMessage = $"Unexpected closing bracket ']' in block state '{trimmed}'.";
            return false;
        }

        string idPart;
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        if (openBracket < 0)
        {
            idPart = trimmed;
        }
        else
        {
            if (closeBracket != trimmed.Length - 1 || closeBracket < openBracket)
            {
                errorMessage = $"Malformed property brackets in block state '{trimmed}'.";
                return false;
            }

            idPart = trimmed[..openBracket];
            string propsSegment = trimmed[(openBracket + 1)..closeBracket];

            if (propsSegment.Length > 0)
            {
                string[] pairs = propsSegment.Split(',');
                for (int i = 0; i < pairs.Length; i++)
                {
                    string pair = pairs[i].Trim();
                    if (pair.Length == 0)
                    {
                        errorMessage = $"Empty property entry in block state '{trimmed}'.";
                        return false;
                    }

                    int eqIdx = pair.IndexOf('=');
                    if (eqIdx <= 0 || eqIdx == pair.Length - 1 || pair.IndexOf('=', eqIdx + 1) >= 0)
                    {
                        errorMessage = $"Malformed property '{pair}' in block state '{trimmed}'.";
                        return false;
                    }

                    string key = pair[..eqIdx].Trim();
                    string val = pair[(eqIdx + 1)..].Trim();

                    if (key.Length == 0 || val.Length == 0 || !IsValidPropertyToken(key) || !IsValidPropertyToken(val))
                    {
                        errorMessage = $"Invalid property key or value '{pair}' in block state '{trimmed}'.";
                        return false;
                    }

                    if (!properties.TryAdd(key, val))
                    {
                        errorMessage = $"Duplicate property key '{key}' in block state '{trimmed}'.";
                        return false;
                    }
                }
            }
        }

        if (!ResourceLocation.TryParse(idPart, out var resourceLocation) || resourceLocation is null)
        {
            errorMessage = $"Invalid block identifier '{idPart}' in block state '{trimmed}'.";
            return false;
        }

        result = new BlockState(resourceLocation.Namespace, resourceLocation.Path, properties);
        return true;
    }

    private static bool IsValidPropertyToken(string token)
    {
        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            bool ok = (c >= 'a' && c <= 'z') ||
                      (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') ||
                      c == '_' || c == '-' || c == '.';
            if (!ok) return false;
        }
        return true;
    }
}

