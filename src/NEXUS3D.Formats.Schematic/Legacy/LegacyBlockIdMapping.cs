using System.Globalization;
using NEXUS3D.Domain.Voxels;
using NEXUS3D.Formats.Schematic.BlockStates;
using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Legacy;

/// <summary>
/// Contract for bidirectional mapping between Minecraft 1.12.2 / Alpha numeric Block ID + 4-bit Metadata
/// and modern USM <see cref="BlockState"/>.
/// </summary>
public interface ILegacyBlockIdMapping
{
    bool TryMapToBlockState(int blockId, byte metadata, out BlockState state, out bool isFallback);
    bool TryMapToLegacy(BlockState state, out int blockId, out byte metadata);
}

/// <summary>
/// Bidirectional registry mapping Legacy numeric Block IDs (0..4095) and 4-bit metadata (0..15)
/// to modern <see cref="BlockState"/> instances.
/// Unmapped legacy blocks are preserved losslessly as <c>legacy:block_&lt;id&gt;[meta=&lt;meta&gt;]</c>.
/// </summary>
public sealed class LegacyBlockIdMapping : ILegacyBlockIdMapping
{
    private readonly Dictionary<(int Id, byte Meta), BlockState> _legacyToModern = new();
    private readonly Dictionary<int, BlockState> _legacyIdDefaultToModern = new();
    private readonly Dictionary<string, (int Id, byte Meta)> _canonicalModernToLegacy = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (int Id, byte Meta)> _blockNameFallbackToLegacy = new(StringComparer.Ordinal);

    public LegacyBlockIdMapping()
    {
        RegisterDefaults();
    }

    public bool TryMapToBlockState(int blockId, byte metadata, out BlockState state, out bool isFallback)
    {
        if (blockId < 0 || blockId > 4095)
        {
            throw new SchematicException(
                SchematicErrorCodes.UnmappedLegacyId,
                $"Legacy Block ID {blockId} is outside the valid 12-bit range [0..4095].");
        }

        if (metadata > 15)
        {
            throw new SchematicException(
                SchematicErrorCodes.InvalidLegacyMetadata,
                $"Legacy block metadata {metadata} is outside the valid 4-bit nibble range [0..15].");
        }

        if (_legacyToModern.TryGetValue((blockId, metadata), out var exact))
        {
            state = exact.DeepClone();
            isFallback = false;
            return true;
        }

        // If metadata is 0 and we have a base ID mapping, use it directly
        if (metadata == 0 && _legacyIdDefaultToModern.TryGetValue(blockId, out var baseState))
        {
            state = baseState.DeepClone();
            isFallback = false;
            return true;
        }

        // Preserve unmapped ID + metadata losslessly as legacy:block_<id>[meta=<metadata>]
        // Per Stage 03 Section 7.6: never silently guess Stone or Air for unknown block IDs!
        state = new BlockState(
            "legacy",
            $"block_{blockId}",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["meta"] = metadata.ToString(CultureInfo.InvariantCulture)
            });
        isFallback = true;
        return false;
    }

    public bool TryMapToLegacy(BlockState state, out int blockId, out byte metadata)
    {
        ArgumentNullException.ThrowIfNull(state);

        // 1. Check lossless preserved legacy placeholder: legacy:block_<id>[meta=<m>]
        if (string.Equals(state.Namespace, "legacy", StringComparison.OrdinalIgnoreCase) &&
            state.Name.StartsWith("block_", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(state.Name.AsSpan("block_".Length), NumberStyles.None, CultureInfo.InvariantCulture, out int parsedId) &&
            parsedId is >= 0 and <= 4095)
        {
            byte parsedMeta = 0;
            if (state.Properties.TryGetValue("meta", out string? metaStr) &&
                byte.TryParse(metaStr, NumberStyles.None, CultureInfo.InvariantCulture, out byte m) &&
                m <= 15)
            {
                parsedMeta = m;
            }

            blockId = parsedId;
            metadata = parsedMeta;
            return true;
        }

        // 2. Check exact canonical BlockState match
        string canonical = BlockStateFormatter.Format(state);
        if (_canonicalModernToLegacy.TryGetValue(canonical, out var exactLegacy))
        {
            blockId = exactLegacy.Id;
            metadata = exactLegacy.Meta;
            return true;
        }

        // 3. Check block resource location without properties (e.g., minecraft:oak_stairs[facing=north,half=top] -> oak_stairs base ID)
        string fullId = $"{state.Namespace}:{state.Name}";
        if (_blockNameFallbackToLegacy.TryGetValue(fullId, out var fallbackLegacy))
        {
            blockId = fallbackLegacy.Id;
            metadata = DeriveDirectionalMetadataIfApplicable(state, fallbackLegacy.Meta);
            return true;
        }

        blockId = 0;
        metadata = 0;
        return false;
    }

    private static byte DeriveDirectionalMetadataIfApplicable(BlockState state, byte defaultMeta)
    {
        if (state.Properties.TryGetValue("axis", out string? axis))
        {
            return axis switch
            {
                "y" => defaultMeta,
                "x" => (byte)((defaultMeta & 0x03) | 0x04),
                "z" => (byte)((defaultMeta & 0x03) | 0x08),
                _ => defaultMeta
            };
        }

        return defaultMeta;
    }

    private void Register(int id, byte meta, string blockStateString, bool isPrimaryForBlockName = false)
    {
        var state = BlockStateParser.Parse(blockStateString);
        string canonical = BlockStateFormatter.Format(state);

        _legacyToModern[(id, meta)] = state;
        if (meta == 0 && !_legacyIdDefaultToModern.ContainsKey(id))
        {
            _legacyIdDefaultToModern[id] = state;
        }

        _canonicalModernToLegacy.TryAdd(canonical, (id, meta));

        string resourceId = $"{state.Namespace}:{state.Name}";
        if (isPrimaryForBlockName || !_blockNameFallbackToLegacy.ContainsKey(resourceId))
        {
            _blockNameFallbackToLegacy[resourceId] = (id, meta);
        }
    }

    private void RegisterDefaults()
    {
        Register(0, 0, "minecraft:air", true);
        Register(1, 0, "minecraft:stone", true);
        Register(1, 1, "minecraft:granite", true);
        Register(1, 2, "minecraft:polished_granite", true);
        Register(1, 3, "minecraft:diorite", true);
        Register(1, 4, "minecraft:polished_diorite", true);
        Register(1, 5, "minecraft:andesite", true);
        Register(1, 6, "minecraft:polished_andesite", true);
        Register(2, 0, "minecraft:grass_block[snowy=false]", true);
        Register(3, 0, "minecraft:dirt", true);
        Register(3, 1, "minecraft:coarse_dirt", true);
        Register(3, 2, "minecraft:podzol[snowy=false]", true);
        Register(4, 0, "minecraft:cobblestone", true);

        // Planks (ID 5)
        Register(5, 0, "minecraft:oak_planks", true);
        Register(5, 1, "minecraft:spruce_planks", true);
        Register(5, 2, "minecraft:birch_planks", true);
        Register(5, 3, "minecraft:jungle_planks", true);
        Register(5, 4, "minecraft:acacia_planks", true);
        Register(5, 5, "minecraft:dark_oak_planks", true);

        Register(6, 0, "minecraft:oak_sapling[stage=0]", true);
        Register(7, 0, "minecraft:bedrock", true);
        Register(8, 0, "minecraft:water[level=0]");
        Register(9, 0, "minecraft:water[level=0]", true);
        Register(10, 0, "minecraft:lava[level=0]");
        Register(11, 0, "minecraft:lava[level=0]", true);
        Register(12, 0, "minecraft:sand", true);
        Register(12, 1, "minecraft:red_sand", true);
        Register(13, 0, "minecraft:gravel", true);
        Register(14, 0, "minecraft:gold_ore", true);
        Register(15, 0, "minecraft:iron_ore", true);
        Register(16, 0, "minecraft:coal_ore", true);

        // Logs (ID 17)
        Register(17, 0, "minecraft:oak_log[axis=y]", true);
        Register(17, 1, "minecraft:spruce_log[axis=y]", true);
        Register(17, 2, "minecraft:birch_log[axis=y]", true);
        Register(17, 3, "minecraft:jungle_log[axis=y]", true);
        Register(17, 4, "minecraft:oak_log[axis=x]");
        Register(17, 5, "minecraft:spruce_log[axis=x]");
        Register(17, 6, "minecraft:birch_log[axis=x]");
        Register(17, 7, "minecraft:jungle_log[axis=x]");
        Register(17, 8, "minecraft:oak_log[axis=z]");
        Register(17, 9, "minecraft:spruce_log[axis=z]");
        Register(17, 10, "minecraft:birch_log[axis=z]");
        Register(17, 11, "minecraft:jungle_log[axis=z]");

        // Leaves (ID 18)
        Register(18, 0, "minecraft:oak_leaves[distance=7,persistent=true]", true);
        Register(18, 1, "minecraft:spruce_leaves[distance=7,persistent=true]", true);
        Register(18, 2, "minecraft:birch_leaves[distance=7,persistent=true]", true);
        Register(18, 3, "minecraft:jungle_leaves[distance=7,persistent=true]", true);

        Register(19, 0, "minecraft:sponge", true);
        Register(19, 1, "minecraft:wet_sponge", true);
        Register(20, 0, "minecraft:glass", true);
        Register(21, 0, "minecraft:lapis_ore", true);
        Register(22, 0, "minecraft:lapis_block", true);
        Register(23, 0, "minecraft:dispenser[facing=north,triggered=false]", true);
        Register(24, 0, "minecraft:sandstone", true);
        Register(24, 1, "minecraft:chiseled_sandstone", true);
        Register(24, 2, "minecraft:cut_sandstone", true);
        Register(25, 0, "minecraft:note_block", true);

        // Wool (ID 35)
        string[] colors =
        [
            "white", "orange", "magenta", "light_blue",
            "yellow", "lime", "pink", "gray",
            "light_gray", "cyan", "purple", "blue",
            "brown", "green", "red", "black"
        ];

        for (byte c = 0; c < 16; c++)
        {
            Register(35, c, $"minecraft:{colors[c]}_wool", true);
            Register(95, c, $"minecraft:{colors[c]}_stained_glass", true);
            Register(159, c, $"minecraft:{colors[c]}_terracotta", true);
            Register(160, c, $"minecraft:{colors[c]}_stained_glass_pane", true);
            Register(171, c, $"minecraft:{colors[c]}_carpet", true);
            Register(251, c, $"minecraft:{colors[c]}_concrete", true);
            Register(252, c, $"minecraft:{colors[c]}_concrete_powder", true);
        }

        Register(41, 0, "minecraft:gold_block", true);
        Register(42, 0, "minecraft:iron_block", true);
        Register(43, 0, "minecraft:smooth_stone_slab[type=double,waterlogged=false]");
        Register(44, 0, "minecraft:smooth_stone_slab[type=bottom,waterlogged=false]", true);
        Register(44, 5, "minecraft:stone_brick_slab[type=bottom,waterlogged=false]", true);
        Register(45, 0, "minecraft:bricks", true);
        Register(46, 0, "minecraft:tnt[unstable=false]", true);
        Register(47, 0, "minecraft:bookshelf", true);
        Register(48, 0, "minecraft:mossy_cobblestone", true);
        Register(49, 0, "minecraft:obsidian", true);
        Register(50, 0, "minecraft:torch", true);
        Register(52, 0, "minecraft:spawner", true);
        Register(53, 0, "minecraft:oak_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(53, 1, "minecraft:oak_stairs[facing=west,half=bottom,shape=straight,waterlogged=false]");
        Register(53, 2, "minecraft:oak_stairs[facing=south,half=bottom,shape=straight,waterlogged=false]");
        Register(53, 3, "minecraft:oak_stairs[facing=north,half=bottom,shape=straight,waterlogged=false]");
        Register(54, 0, "minecraft:chest[facing=north,type=single,waterlogged=false]");
        Register(54, 2, "minecraft:chest[facing=north,type=single,waterlogged=false]", true);
        Register(54, 3, "minecraft:chest[facing=south,type=single,waterlogged=false]");
        Register(54, 4, "minecraft:chest[facing=west,type=single,waterlogged=false]");
        Register(54, 5, "minecraft:chest[facing=east,type=single,waterlogged=false]");
        Register(56, 0, "minecraft:diamond_ore", true);
        Register(57, 0, "minecraft:diamond_block", true);
        Register(58, 0, "minecraft:crafting_table", true);
        Register(60, 0, "minecraft:farmland[moisture=0]", true);
        Register(61, 0, "minecraft:furnace[facing=north,lit=false]");
        Register(61, 2, "minecraft:furnace[facing=north,lit=false]", true);
        Register(62, 2, "minecraft:furnace[facing=north,lit=true]");
        Register(63, 0, "minecraft:oak_sign[rotation=0,waterlogged=false]", true);
        Register(64, 0, "minecraft:oak_door[facing=east,half=lower,hinge=left,open=false,powered=false]", true);
        Register(65, 2, "minecraft:ladder[facing=north,waterlogged=false]", true);
        Register(67, 0, "minecraft:cobblestone_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(68, 2, "minecraft:oak_wall_sign[facing=north,waterlogged=false]", true);
        Register(69, 0, "minecraft:lever[face=wall,facing=north,powered=false]", true);
        Register(70, 0, "minecraft:stone_pressure_plate[powered=false]", true);
        Register(72, 0, "minecraft:oak_pressure_plate[powered=false]", true);
        Register(73, 0, "minecraft:redstone_ore[lit=false]", true);
        Register(76, 0, "minecraft:redstone_torch[lit=true]", true);
        Register(77, 0, "minecraft:stone_button[face=wall,facing=north,powered=false]", true);
        Register(78, 0, "minecraft:snow[layers=1]", true);
        Register(79, 0, "minecraft:ice", true);
        Register(80, 0, "minecraft:snow_block", true);
        Register(81, 0, "minecraft:cactus[age=0]", true);
        Register(82, 0, "minecraft:clay", true);
        Register(85, 0, "minecraft:oak_fence[east=false,north=false,south=false,waterlogged=false,west=false]", true);
        Register(86, 0, "minecraft:carved_pumpkin[facing=south]", true);
        Register(87, 0, "minecraft:netherrack", true);
        Register(88, 0, "minecraft:soul_sand", true);
        Register(89, 0, "minecraft:glowstone", true);
        Register(91, 0, "minecraft:jack_o_lantern[facing=south]", true);
        Register(98, 0, "minecraft:stone_bricks", true);
        Register(98, 1, "minecraft:mossy_stone_bricks", true);
        Register(98, 2, "minecraft:cracked_stone_bricks", true);
        Register(98, 3, "minecraft:chiseled_stone_bricks", true);
        Register(101, 0, "minecraft:iron_bars[east=false,north=false,south=false,waterlogged=false,west=false]", true);
        Register(102, 0, "minecraft:glass_pane[east=false,north=false,south=false,waterlogged=false,west=false]", true);
        Register(103, 0, "minecraft:melon", true);
        Register(108, 0, "minecraft:brick_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(109, 0, "minecraft:stone_brick_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(110, 0, "minecraft:mycelium[snowy=false]", true);
        Register(112, 0, "minecraft:nether_bricks", true);
        Register(114, 0, "minecraft:nether_brick_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(121, 0, "minecraft:end_stone", true);
        Register(123, 0, "minecraft:redstone_lamp[lit=false]", true);
        Register(126, 0, "minecraft:oak_slab[type=bottom,waterlogged=false]", true);
        Register(128, 0, "minecraft:sandstone_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(129, 0, "minecraft:emerald_ore", true);
        Register(133, 0, "minecraft:emerald_block", true);
        Register(134, 0, "minecraft:spruce_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(135, 0, "minecraft:birch_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(136, 0, "minecraft:jungle_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(137, 0, "minecraft:command_block[conditional=false,facing=north]", true);
        Register(138, 0, "minecraft:beacon", true);
        Register(139, 0, "minecraft:cobblestone_wall[east=none,north=none,south=none,up=true,waterlogged=false,west=none]", true);
        Register(145, 0, "minecraft:anvil[facing=north]", true);
        Register(146, 0, "minecraft:trapped_chest[facing=north,type=single,waterlogged=false]", true);
        Register(152, 0, "minecraft:redstone_block", true);
        Register(153, 0, "minecraft:nether_quartz_ore", true);
        Register(154, 0, "minecraft:hopper[enabled=true,facing=down]", true);
        Register(155, 0, "minecraft:quartz_block", true);
        Register(155, 1, "minecraft:chiseled_quartz_block", true);
        Register(155, 2, "minecraft:quartz_pillar[axis=y]", true);
        Register(156, 0, "minecraft:quartz_stairs[facing=east,half=bottom,shape=straight,waterlogged=false]", true);
        Register(162, 0, "minecraft:acacia_log[axis=y]", true);
        Register(162, 1, "minecraft:dark_oak_log[axis=y]", true);
        Register(165, 0, "minecraft:slime_block", true);
        Register(166, 0, "minecraft:barrier", true);
        Register(168, 0, "minecraft:prismarine", true);
        Register(168, 1, "minecraft:prismarine_bricks", true);
        Register(168, 2, "minecraft:dark_prismarine", true);
        Register(169, 0, "minecraft:sea_lantern", true);
        Register(170, 0, "minecraft:hay_block[axis=y]", true);
        Register(172, 0, "minecraft:terracotta", true);
        Register(173, 0, "minecraft:coal_block", true);
        Register(174, 0, "minecraft:packed_ice", true);
        Register(179, 0, "minecraft:red_sandstone", true);
        Register(198, 0, "minecraft:end_rod[facing=up]", true);
        Register(201, 0, "minecraft:purpur_block", true);
        Register(202, 0, "minecraft:purpur_pillar[axis=y]", true);
        Register(206, 0, "minecraft:end_stone_bricks", true);
        Register(213, 0, "minecraft:magma_block", true);
        Register(214, 0, "minecraft:nether_wart_block", true);
        Register(215, 0, "minecraft:red_nether_bricks", true);
        Register(216, 0, "minecraft:bone_block[axis=y]", true);
    }
}
