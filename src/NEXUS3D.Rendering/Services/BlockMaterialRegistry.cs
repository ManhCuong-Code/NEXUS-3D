using System.Numerics;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Rendering.Services;

/// <summary>
/// Transparency classification for Minecraft and voxel blocks.
/// </summary>
public enum BlockTransparencyKind
{
    Opaque = 0,
    Transparent = 1,
    Translucent = 2,
    Cutout = 3
}

/// <summary>
/// Detailed material attributes for a recognized voxel block.
/// </summary>
public sealed record BlockMaterialInfo(
    string FullIdentifier,
    string DisplayName,
    Vector4 Color,
    float Opacity = 1.0f,
    float Roughness = 0.8f,
    float Metallic = 0.0f,
    bool IsTransparent = false,
    bool IsEmissive = false,
    BlockTransparencyKind Transparency = BlockTransparencyKind.Opaque);

/// <summary>
/// Authoritative registry mapping standard Minecraft block types and custom voxel identifiers
/// to high-fidelity representative colors, opacity, and material properties.
/// </summary>
public static class BlockMaterialRegistry
{
    private static readonly Dictionary<string, BlockMaterialInfo> Registry = new(StringComparer.OrdinalIgnoreCase);

    static BlockMaterialRegistry()
    {
        // 1. Natural & Earth Blocks
        Register("minecraft:stone", "Stone", new Vector4(0.49f, 0.49f, 0.49f, 1.0f), roughness: 0.9f);
        Register("minecraft:granite", "Granite", new Vector4(0.60f, 0.43f, 0.37f, 1.0f), roughness: 0.85f);
        Register("minecraft:polished_granite", "Polished Granite", new Vector4(0.60f, 0.43f, 0.37f, 1.0f), roughness: 0.5f);
        Register("minecraft:diorite", "Diorite", new Vector4(0.74f, 0.74f, 0.75f, 1.0f), roughness: 0.85f);
        Register("minecraft:polished_diorite", "Polished Diorite", new Vector4(0.74f, 0.74f, 0.75f, 1.0f), roughness: 0.5f);
        Register("minecraft:andesite", "Andesite", new Vector4(0.53f, 0.53f, 0.54f, 1.0f), roughness: 0.85f);
        Register("minecraft:polished_andesite", "Polished Andesite", new Vector4(0.53f, 0.53f, 0.54f, 1.0f), roughness: 0.5f);
        Register("minecraft:deepslate", "Deepslate", new Vector4(0.27f, 0.27f, 0.29f, 1.0f), roughness: 0.9f);
        Register("minecraft:cobbled_deepslate", "Cobbled Deepslate", new Vector4(0.30f, 0.30f, 0.32f, 1.0f), roughness: 0.95f);
        Register("minecraft:grass_block", "Grass Block", new Vector4(0.37f, 0.62f, 0.21f, 1.0f), roughness: 0.95f);
        Register("minecraft:dirt", "Dirt", new Vector4(0.53f, 0.38f, 0.26f, 1.0f), roughness: 0.95f);
        Register("minecraft:coarse_dirt", "Coarse Dirt", new Vector4(0.47f, 0.33f, 0.23f, 1.0f), roughness: 0.95f);
        Register("minecraft:podzol", "Podzol", new Vector4(0.35f, 0.25f, 0.11f, 1.0f), roughness: 0.95f);
        Register("minecraft:mycelium", "Mycelium", new Vector4(0.44f, 0.38f, 0.41f, 1.0f), roughness: 0.95f);
        Register("minecraft:mud", "Mud", new Vector4(0.24f, 0.24f, 0.24f, 1.0f), roughness: 0.9f);
        Register("minecraft:sand", "Sand", new Vector4(0.86f, 0.81f, 0.61f, 1.0f), roughness: 0.95f);
        Register("minecraft:red_sand", "Red Sand", new Vector4(0.75f, 0.40f, 0.13f, 1.0f), roughness: 0.95f);
        Register("minecraft:gravel", "Gravel", new Vector4(0.53f, 0.49f, 0.49f, 1.0f), roughness: 0.95f);
        Register("minecraft:clay", "Clay", new Vector4(0.63f, 0.65f, 0.70f, 1.0f), roughness: 0.8f);
        Register("minecraft:bedrock", "Bedrock", new Vector4(0.20f, 0.20f, 0.20f, 1.0f), roughness: 0.95f);
        Register("minecraft:cobblestone", "Cobblestone", new Vector4(0.43f, 0.43f, 0.43f, 1.0f), roughness: 0.95f);
        Register("minecraft:mossy_cobblestone", "Mossy Cobblestone", new Vector4(0.35f, 0.42f, 0.31f, 1.0f), roughness: 0.95f);
        Register("minecraft:obsidian", "Obsidian", new Vector4(0.08f, 0.06f, 0.13f, 1.0f), roughness: 0.3f);
        Register("minecraft:crying_obsidian", "Crying Obsidian", new Vector4(0.14f, 0.07f, 0.24f, 1.0f), roughness: 0.3f, isEmissive: true);

        // 2. Wood & Planks
        Register("minecraft:oak_planks", "Oak Planks", new Vector4(0.64f, 0.51f, 0.31f, 1.0f), roughness: 0.75f);
        Register("minecraft:oak_log", "Oak Log", new Vector4(0.40f, 0.32f, 0.19f, 1.0f), roughness: 0.85f);
        Register("minecraft:oak_wood", "Oak Wood", new Vector4(0.40f, 0.32f, 0.19f, 1.0f), roughness: 0.85f);
        Register("minecraft:spruce_planks", "Spruce Planks", new Vector4(0.45f, 0.33f, 0.19f, 1.0f), roughness: 0.75f);
        Register("minecraft:spruce_log", "Spruce Log", new Vector4(0.24f, 0.17f, 0.10f, 1.0f), roughness: 0.85f);
        Register("minecraft:birch_planks", "Birch Planks", new Vector4(0.84f, 0.81f, 0.62f, 1.0f), roughness: 0.75f);
        Register("minecraft:birch_log", "Birch Log", new Vector4(0.87f, 0.87f, 0.87f, 1.0f), roughness: 0.85f);
        Register("minecraft:jungle_planks", "Jungle Planks", new Vector4(0.63f, 0.45f, 0.31f, 1.0f), roughness: 0.75f);
        Register("minecraft:jungle_log", "Jungle Log", new Vector4(0.35f, 0.28f, 0.12f, 1.0f), roughness: 0.85f);
        Register("minecraft:acacia_planks", "Acacia Planks", new Vector4(0.66f, 0.35f, 0.20f, 1.0f), roughness: 0.75f);
        Register("minecraft:acacia_log", "Acacia Log", new Vector4(0.42f, 0.40f, 0.38f, 1.0f), roughness: 0.85f);
        Register("minecraft:dark_oak_planks", "Dark Oak Planks", new Vector4(0.26f, 0.17f, 0.08f, 1.0f), roughness: 0.75f);
        Register("minecraft:dark_oak_log", "Dark Oak Log", new Vector4(0.18f, 0.12f, 0.06f, 1.0f), roughness: 0.85f);
        Register("minecraft:mangrove_planks", "Mangrove Planks", new Vector4(0.46f, 0.21f, 0.19f, 1.0f), roughness: 0.75f);
        Register("minecraft:cherry_planks", "Cherry Planks", new Vector4(0.89f, 0.70f, 0.68f, 1.0f), roughness: 0.75f);
        Register("minecraft:bamboo_planks", "Bamboo Planks", new Vector4(0.75f, 0.68f, 0.36f, 1.0f), roughness: 0.75f);
        Register("minecraft:crimson_planks", "Crimson Planks", new Vector4(0.40f, 0.19f, 0.27f, 1.0f), roughness: 0.75f);
        Register("minecraft:warped_planks", "Warped Planks", new Vector4(0.17f, 0.41f, 0.39f, 1.0f), roughness: 0.75f);

        // 3. Foliage & Leaves (Cutout transparency)
        Register("minecraft:oak_leaves", "Oak Leaves", new Vector4(0.23f, 0.43f, 0.14f, 1.0f), roughness: 0.9f, transparency: BlockTransparencyKind.Cutout);
        Register("minecraft:leaves", "Leaves", new Vector4(0.23f, 0.43f, 0.14f, 1.0f), roughness: 0.9f, transparency: BlockTransparencyKind.Cutout);
        Register("minecraft:birch_leaves", "Birch Leaves", new Vector4(0.50f, 0.64f, 0.34f, 1.0f), roughness: 0.9f, transparency: BlockTransparencyKind.Cutout);
        Register("minecraft:spruce_leaves", "Spruce Leaves", new Vector4(0.24f, 0.37f, 0.24f, 1.0f), roughness: 0.9f, transparency: BlockTransparencyKind.Cutout);
        Register("minecraft:jungle_leaves", "Jungle Leaves", new Vector4(0.19f, 0.47f, 0.11f, 1.0f), roughness: 0.9f, transparency: BlockTransparencyKind.Cutout);
        Register("minecraft:acacia_leaves", "Acacia Leaves", new Vector4(0.29f, 0.46f, 0.11f, 1.0f), roughness: 0.9f, transparency: BlockTransparencyKind.Cutout);
        Register("minecraft:dark_oak_leaves", "Dark Oak Leaves", new Vector4(0.16f, 0.31f, 0.08f, 1.0f), roughness: 0.9f, transparency: BlockTransparencyKind.Cutout);
        Register("minecraft:cherry_leaves", "Cherry Leaves", new Vector4(0.94f, 0.71f, 0.76f, 1.0f), roughness: 0.9f, transparency: BlockTransparencyKind.Cutout);
        Register("minecraft:azalea_leaves", "Azalea Leaves", new Vector4(0.35f, 0.47f, 0.18f, 1.0f), roughness: 0.9f, transparency: BlockTransparencyKind.Cutout);

        // 4. Transparent & Fluid Blocks
        Register("minecraft:glass", "Glass", new Vector4(0.78f, 0.90f, 0.96f, 0.45f), opacity: 0.45f, roughness: 0.1f, isTransparent: true, transparency: BlockTransparencyKind.Transparent);
        Register("minecraft:tinted_glass", "Tinted Glass", new Vector4(0.27f, 0.24f, 0.29f, 0.70f), opacity: 0.70f, roughness: 0.1f, isTransparent: true, transparency: BlockTransparencyKind.Transparent);
        Register("minecraft:water", "Water", new Vector4(0.17f, 0.38f, 0.86f, 0.65f), opacity: 0.65f, roughness: 0.05f, isTransparent: true, transparency: BlockTransparencyKind.Translucent);
        Register("minecraft:lava", "Lava", new Vector4(0.92f, 0.39f, 0.05f, 1.0f), roughness: 0.2f, isEmissive: true);
        Register("minecraft:ice", "Ice", new Vector4(0.57f, 0.73f, 0.96f, 0.70f), opacity: 0.70f, roughness: 0.1f, isTransparent: true, transparency: BlockTransparencyKind.Translucent);
        Register("minecraft:packed_ice", "Packed Ice", new Vector4(0.55f, 0.71f, 0.96f, 1.0f), roughness: 0.15f);
        Register("minecraft:blue_ice", "Blue Ice", new Vector4(0.45f, 0.65f, 0.99f, 1.0f), roughness: 0.1f);

        // 5. Ores & Minerals
        Register("minecraft:gold_ore", "Gold Ore", new Vector4(0.56f, 0.55f, 0.49f, 1.0f), roughness: 0.85f);
        Register("minecraft:iron_ore", "Iron Ore", new Vector4(0.53f, 0.50f, 0.47f, 1.0f), roughness: 0.85f);
        Register("minecraft:coal_ore", "Coal Ore", new Vector4(0.41f, 0.41f, 0.41f, 1.0f), roughness: 0.85f);
        Register("minecraft:copper_ore", "Copper Ore", new Vector4(0.49f, 0.46f, 0.43f, 1.0f), roughness: 0.85f);
        Register("minecraft:diamond_ore", "Diamond Ore", new Vector4(0.47f, 0.55f, 0.56f, 1.0f), roughness: 0.85f);
        Register("minecraft:lapis_ore", "Lapis Ore", new Vector4(0.39f, 0.43f, 0.55f, 1.0f), roughness: 0.85f);
        Register("minecraft:emerald_ore", "Emerald Ore", new Vector4(0.43f, 0.55f, 0.45f, 1.0f), roughness: 0.85f);
        Register("minecraft:gold_block", "Gold Block", new Vector4(0.98f, 0.83f, 0.22f, 1.0f), roughness: 0.3f, metallic: 0.9f);
        Register("minecraft:iron_block", "Iron Block", new Vector4(0.86f, 0.86f, 0.86f, 1.0f), roughness: 0.4f, metallic: 0.8f);
        Register("minecraft:diamond_block", "Diamond Block", new Vector4(0.36f, 0.93f, 0.90f, 1.0f), roughness: 0.2f);
        Register("minecraft:emerald_block", "Emerald Block", new Vector4(0.16f, 0.80f, 0.34f, 1.0f), roughness: 0.25f);
        Register("minecraft:netherite_block", "Netherite Block", new Vector4(0.27f, 0.23f, 0.24f, 1.0f), roughness: 0.4f, metallic: 0.7f);
        Register("minecraft:copper_block", "Copper Block", new Vector4(0.75f, 0.42f, 0.31f, 1.0f), roughness: 0.4f, metallic: 0.8f);
        Register("minecraft:redstone_block", "Redstone Block", new Vector4(0.67f, 0.08f, 0.06f, 1.0f), roughness: 0.6f, isEmissive: true);
        Register("minecraft:lapis_block", "Lapis Block", new Vector4(0.12f, 0.26f, 0.55f, 1.0f), roughness: 0.6f);
        Register("minecraft:coal_block", "Coal Block", new Vector4(0.09f, 0.09f, 0.09f, 1.0f), roughness: 0.9f);

        // 6. Masonry, Bricks & Utility
        Register("minecraft:bricks", "Bricks", new Vector4(0.59f, 0.26f, 0.21f, 1.0f), roughness: 0.85f);
        Register("minecraft:stone_bricks", "Stone Bricks", new Vector4(0.48f, 0.48f, 0.48f, 1.0f), roughness: 0.85f);
        Register("minecraft:mossy_stone_bricks", "Mossy Stone Bricks", new Vector4(0.41f, 0.45f, 0.37f, 1.0f), roughness: 0.85f);
        Register("minecraft:cracked_stone_bricks", "Cracked Stone Bricks", new Vector4(0.45f, 0.45f, 0.45f, 1.0f), roughness: 0.9f);
        Register("minecraft:bookshelf", "Bookshelf", new Vector4(0.57f, 0.43f, 0.25f, 1.0f), roughness: 0.8f);
        Register("minecraft:crafting_table", "Crafting Table", new Vector4(0.55f, 0.37f, 0.22f, 1.0f), roughness: 0.8f);
        Register("minecraft:furnace", "Furnace", new Vector4(0.41f, 0.41f, 0.41f, 1.0f), roughness: 0.85f);
        Register("minecraft:chest", "Chest", new Vector4(0.63f, 0.45f, 0.18f, 1.0f), roughness: 0.75f);
        Register("minecraft:barrel", "Barrel", new Vector4(0.53f, 0.37f, 0.20f, 1.0f), roughness: 0.8f);
        Register("minecraft:tnt", "TNT", new Vector4(0.75f, 0.19f, 0.15f, 1.0f), roughness: 0.7f);
        Register("minecraft:sponge", "Sponge", new Vector4(0.76f, 0.76f, 0.30f, 1.0f), roughness: 0.95f);
        Register("minecraft:wet_sponge", "Wet Sponge", new Vector4(0.67f, 0.67f, 0.25f, 1.0f), roughness: 0.95f);
        Register("minecraft:glowstone", "Glowstone", new Vector4(0.85f, 0.67f, 0.40f, 1.0f), roughness: 0.5f, isEmissive: true);
        Register("minecraft:sea_lantern", "Sea Lantern", new Vector4(0.67f, 0.79f, 0.76f, 1.0f), roughness: 0.3f, isEmissive: true);

        // 7. Wool & Concrete (Full 16-Dye Minecraft Colors)
        RegisterDyeColor("white", new Vector4(0.91f, 0.93f, 0.93f, 1.0f));
        RegisterDyeColor("orange", new Vector4(0.94f, 0.46f, 0.07f, 1.0f));
        RegisterDyeColor("magenta", new Vector4(0.74f, 0.27f, 0.70f, 1.0f));
        RegisterDyeColor("light_blue", new Vector4(0.23f, 0.69f, 0.85f, 1.0f));
        RegisterDyeColor("yellow", new Vector4(0.97f, 0.78f, 0.15f, 1.0f));
        RegisterDyeColor("lime", new Vector4(0.44f, 0.73f, 0.10f, 1.0f));
        RegisterDyeColor("pink", new Vector4(0.93f, 0.55f, 0.67f, 1.0f));
        RegisterDyeColor("gray", new Vector4(0.24f, 0.27f, 0.28f, 1.0f));
        RegisterDyeColor("light_gray", new Vector4(0.56f, 0.56f, 0.53f, 1.0f));
        RegisterDyeColor("cyan", new Vector4(0.08f, 0.54f, 0.57f, 1.0f));
        RegisterDyeColor("purple", new Vector4(0.47f, 0.16f, 0.67f, 1.0f));
        RegisterDyeColor("blue", new Vector4(0.21f, 0.22f, 0.62f, 1.0f));
        RegisterDyeColor("brown", new Vector4(0.45f, 0.28f, 0.16f, 1.0f));
        RegisterDyeColor("green", new Vector4(0.33f, 0.43f, 0.11f, 1.0f));
        RegisterDyeColor("red", new Vector4(0.63f, 0.15f, 0.13f, 1.0f));
        RegisterDyeColor("black", new Vector4(0.08f, 0.08f, 0.10f, 1.0f));
    }

    private static void Register(
        string id,
        string name,
        Vector4 color,
        float opacity = 1.0f,
        float roughness = 0.8f,
        float metallic = 0.0f,
        bool isTransparent = false,
        bool isEmissive = false,
        BlockTransparencyKind? transparency = null)
    {
        var transKind = transparency ?? (opacity < 0.99f
            ? (isTransparent ? BlockTransparencyKind.Transparent : BlockTransparencyKind.Translucent)
            : BlockTransparencyKind.Opaque);

        var info = new BlockMaterialInfo(id, name, color, opacity, roughness, metallic, isTransparent, isEmissive, transKind);
        Registry[id] = info;

        string simpleName = id.Contains(':') ? id[(id.IndexOf(':') + 1)..] : id;
        Registry[simpleName] = info;
    }

    private static void RegisterDyeColor(string colorName, Vector4 col)
    {
        Register($"minecraft:{colorName}_wool", $"{Capitalize(colorName)} Wool", col, roughness: 0.95f);
        Register($"minecraft:{colorName}_concrete", $"{Capitalize(colorName)} Concrete", col, roughness: 0.7f);
        Register($"minecraft:{colorName}_terracotta", $"{Capitalize(colorName)} Terracotta", Vector4.Lerp(col, new Vector4(0.6f, 0.37f, 0.26f, 1.0f), 0.35f), roughness: 0.85f);
        Register($"minecraft:{colorName}_stained_glass", $"{Capitalize(colorName)} Stained Glass", new Vector4(col.X, col.Y, col.Z, 0.45f), opacity: 0.45f, roughness: 0.1f, isTransparent: true);
    }

    private static string Capitalize(string str) =>
        string.IsNullOrEmpty(str) ? str : char.ToUpperInvariant(str[0]) + str[1..].Replace('_', ' ');

    /// <summary>
    /// Resolves comprehensive material metadata for any block state or block identifier.
    /// Never returns null or invisible color for valid blocks.
    /// </summary>
    public static BlockMaterialInfo GetBlockInfo(string blockIdentifier)
    {
        if (string.IsNullOrWhiteSpace(blockIdentifier))
        {
            return Registry["minecraft:stone"];
        }

        string cleanId = blockIdentifier.Trim();
        // Strip block state brackets if present, e.g. "minecraft:oak_stairs[facing=north]" -> "minecraft:oak_stairs"
        int bracketIdx = cleanId.IndexOf('[');
        if (bracketIdx > 0)
        {
            cleanId = cleanId[..bracketIdx];
        }

        // 1. Direct registry lookup
        if (Registry.TryGetValue(cleanId, out var directMatch))
        {
            return directMatch;
        }

        string simple = cleanId.Contains(':') ? cleanId[(cleanId.IndexOf(':') + 1)..] : cleanId;
        if (Registry.TryGetValue(simple, out var simpleMatch))
        {
            return simpleMatch;
        }

        // 2. Semantic keyword-based fallback matching
        string lower = simple.ToLowerInvariant();

        if (lower.Contains("leaves") || lower.Contains("vine") || lower.Contains("grass") || lower.Contains("foliage") || lower.Contains("sprout") || lower.Contains("bush"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.25f, 0.46f, 0.15f, 1.0f), Roughness: 0.9f, Transparency: BlockTransparencyKind.Cutout);
        }
        if (lower.Contains("wood") || lower.Contains("plank") || lower.Contains("log") || lower.Contains("stem") || lower.Contains("bark") || lower.Contains("fence") || lower.Contains("door") || lower.Contains("slab") && lower.Contains("oak"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.58f, 0.44f, 0.26f, 1.0f), Roughness: 0.8f, Transparency: BlockTransparencyKind.Opaque);
        }
        if (lower.Contains("glass"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.75f, 0.88f, 0.95f, 0.45f), Opacity: 0.45f, Roughness: 0.1f, IsTransparent: true, Transparency: BlockTransparencyKind.Transparent);
        }
        if (lower.Contains("water") || lower.Contains("ocean"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.17f, 0.38f, 0.86f, 0.65f), Opacity: 0.65f, Roughness: 0.05f, IsTransparent: true, Transparency: BlockTransparencyKind.Translucent);
        }
        if (lower.Contains("lava") || lower.Contains("fire"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.92f, 0.39f, 0.05f, 1.0f), Roughness: 0.2f, IsEmissive: true);
        }
        if (lower.Contains("stone") || lower.Contains("rock") || lower.Contains("cobble") || lower.Contains("slate") || lower.Contains("basalt") || lower.Contains("tuff"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.48f, 0.48f, 0.48f, 1.0f), Roughness: 0.9f);
        }
        if (lower.Contains("sand"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.85f, 0.80f, 0.60f, 1.0f), Roughness: 0.95f);
        }
        if (lower.Contains("dirt") || lower.Contains("soil") || lower.Contains("mud"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.52f, 0.37f, 0.25f, 1.0f), Roughness: 0.95f);
        }
        if (lower.Contains("brick"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.58f, 0.27f, 0.22f, 1.0f), Roughness: 0.85f);
        }
        if (lower.Contains("gold"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.96f, 0.81f, 0.21f, 1.0f), Roughness: 0.35f, Metallic: 0.9f);
        }
        if (lower.Contains("iron"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.85f, 0.85f, 0.85f, 1.0f), Roughness: 0.4f, Metallic: 0.8f);
        }
        if (lower.Contains("diamond"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.35f, 0.92f, 0.89f, 1.0f), Roughness: 0.2f);
        }
        if (lower.Contains("emerald"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.15f, 0.80f, 0.33f, 1.0f), Roughness: 0.25f);
        }
        if (lower.Contains("redstone"))
        {
            return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(0.67f, 0.08f, 0.06f, 1.0f), Roughness: 0.6f, IsEmissive: true);
        }

        // 3. Deterministic hash color fallback
        int hash = Math.Abs(cleanId.GetHashCode());
        float r = ((hash & 0xFF) / 255f) * 0.65f + 0.3f;
        float g = (((hash >> 8) & 0xFF) / 255f) * 0.65f + 0.3f;
        float b = (((hash >> 16) & 0xFF) / 255f) * 0.65f + 0.3f;

        return new BlockMaterialInfo(cleanId, Capitalize(simple), new Vector4(r, g, b, 1.0f), Roughness: 0.8f);
    }

    public static BlockMaterialInfo GetBlockInfo(BlockState state) =>
        GetBlockInfo(state?.GetFullIdentifier() ?? "minecraft:stone");
}
