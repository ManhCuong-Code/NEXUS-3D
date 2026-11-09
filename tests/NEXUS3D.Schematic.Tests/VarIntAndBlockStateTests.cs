using NEXUS3D.Formats.Schematic.BlockStates;
using NEXUS3D.Formats.Schematic.Common;
using NEXUS3D.Formats.Schematic.Palette;

namespace NEXUS3D.Schematic.Tests;

public sealed class VarIntAndBlockStateTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(127, 1)]
    [InlineData(128, 2)]
    [InlineData(255, 2)]
    [InlineData(256, 2)]
    [InlineData(16383, 2)]
    [InlineData(16384, 3)]
    [InlineData(65535, 3)]
    public void VarInt_EncodeAndDecode_EdgeCases_RoundTripsExactValueAndByteCount(int value, int expectedBytes)
    {
        byte[] encoded = VarIntEncoder.EncodeSingle(value);
        Assert.Equal(expectedBytes, encoded.Length);

        int offset = 0;
        int decoded = VarIntDecoder.ReadNext(encoded, ref offset);
        Assert.Equal(value, decoded);
        Assert.Equal(expectedBytes, offset);
    }

    [Fact]
    public void VarInt_TruncatedContinuationBit_ThrowsSch006()
    {
        byte[] truncated = [0x80, 0x80];
        var ex = Assert.Throws<SchematicException>(() =>
        {
            int offset = 0;
            _ = VarIntDecoder.ReadNext(truncated, ref offset);
        });
        Assert.Equal(SchematicErrorCodes.InvalidVarInt, ex.ErrorCode);
    }

    [Fact]
    public void VarInt_OverlongMoreThan5Bytes_ThrowsSch006()
    {
        byte[] overlong = [0x80, 0x80, 0x80, 0x80, 0x80, 0x01];
        var ex = Assert.Throws<SchematicException>(() =>
        {
            int offset = 0;
            _ = VarIntDecoder.ReadNext(overlong, ref offset);
        });
        Assert.Equal(SchematicErrorCodes.InvalidVarInt, ex.ErrorCode);
    }

    [Fact]
    public void VarInt_NegativeInput_ThrowsSch006()
    {
        var ex = Assert.Throws<SchematicException>(() => VarIntEncoder.EncodeSingle(-1));
        Assert.Equal(SchematicErrorCodes.InvalidVarInt, ex.ErrorCode);
    }

    [Fact]
    public void BlockStateParser_And_Formatter_CanonicalizeCorrectly()
    {
        // Default namespace 'minecraft' + sorted properties
        var state = BlockStateParser.Parse("oak_stairs[waterlogged=false,half=bottom,facing=north,shape=straight]");
        Assert.Equal("minecraft", state.Namespace);
        Assert.Equal("oak_stairs", state.Name);
        Assert.Equal(4, state.Properties.Count);
        Assert.Equal("north", state.Properties["facing"]);
        Assert.Equal("bottom", state.Properties["half"]);

        string formatted = BlockStateFormatter.Format(state);
        Assert.Equal("minecraft:oak_stairs[facing=north,half=bottom,shape=straight,waterlogged=false]", formatted);
    }

    [Fact]
    public void BlockStateParser_ModdedNamespace_PreservesNamespaceAndProperties()
    {
        var state = BlockStateParser.Parse("create:brass_casing[axis=y,waterlogged=false]");
        Assert.Equal("create", state.Namespace);
        Assert.Equal("brass_casing", state.Name);
        Assert.Equal("create:brass_casing[axis=y,waterlogged=false]", BlockStateFormatter.Format(state));
    }

    [Theory]
    [InlineData("")]
    [InlineData("minecraft:oak_stairs[facing=north")]
    [InlineData("minecraft:oak_stairs[=north]")]
    [InlineData("minecraft:oak_stairs[facing=]")]
    [InlineData("Invalid Uppercase:Block")]
    public void BlockStateParser_InvalidSyntax_ThrowsSch007(string raw)
    {
        var ex = Assert.Throws<SchematicException>(() => BlockStateParser.Parse(raw));
        Assert.Equal(SchematicErrorCodes.InvalidBlockState, ex.ErrorCode);
    }

    [Fact]
    public void SchematicIndexing_YzxFormula_RoundTripsAllCoordinates()
    {
        const int width = 7;
        const int height = 5;
        const int length = 9;

        int expectedLinear = 0;
        for (int y = 0; y < height; y++)
        {
            for (int z = 0; z < length; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = SchematicIndexing.ToIndex(x, y, z, width, length);
                    Assert.Equal(expectedLinear, index);
                    Assert.Equal(x + z * width + y * width * length, index);

                    var pos = SchematicIndexing.FromIndex(index, width, height, length);
                    Assert.Equal(x, pos.X);
                    Assert.Equal(y, pos.Y);
                    Assert.Equal(z, pos.Z);

                    expectedLinear++;
                }
            }
        }
    }
}
