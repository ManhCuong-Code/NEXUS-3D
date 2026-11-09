using System.Text;
using NEXUS3D.Domain.Materials;
using NEXUS3D.Domain.Mathematics;
using NEXUS3D.Formats.Obj.Exporting;
using NEXUS3D.Formats.Obj.Materials;

namespace NEXUS3D.Obj.Tests;

public sealed class MtlParserAndExporterTests
{
    [Fact]
    public async Task Parse_MultipleMaterialsWithAllStandardAndTextureStatements_Succeeds()
    {
        const string mtlText = """
            # MTL Header Comment
            newmtl BrickWall
            Ka 0.1 0.12 0.15
            Kd 0.8 0.35 0.25
            Ks 0.25 0.25 0.25
            Ke 0.05 0.02 0.0
            Tf 0.9 0.9 0.9
            Ns 128.5
            Ni 1.45
            d 0.92
            illum 2
            map_Ka -o 0.1 0.2 0.0 -s 2.0 2.0 1.0 textures/brick wall/ambient map.png
            map_Kd textures/brick wall/diffuse map.png
            map_Ks -clamp on textures/specular.tga
            map_Ke textures/emissive.png
            map_Ns -imfchan r textures/roughness.png
            map_d textures/opacity.png
            map_Bump -bm 0.75 textures/brick wall/normal bump.png
            disp textures/displacement.png
            decal textures/decal.png
            refl -type sphere textures/env_sphere.jpg
            norm textures/normal_directx.png
            Pr 0.45

            newmtl GlassPane
            Kd 0.9 0.95 1.0
            Tr 0.75
            Ni 1.52
            illum 4
            """;

        var parser = new MtlParser();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mtlText));
        MtlParseResult result = await parser.ParseAsync(stream, "test.mtl");

        Assert.False(result.HasErrors);
        Assert.Equal(2, result.Document.Materials.Count);

        MtlMaterial brick = result.Document.Materials[0];
        Assert.Equal("BrickWall", brick.Name);
        Assert.Equal(new MaterialColor(0.1, 0.12, 0.15, 1.0), brick.AmbientColor);
        Assert.Equal(new MaterialColor(0.8, 0.35, 0.25, 1.0), brick.DiffuseColor);
        Assert.Equal(new MaterialColor(0.25, 0.25, 0.25, 1.0), brick.SpecularColor);
        Assert.Equal(new MaterialColor(0.05, 0.02, 0.0, 1.0), brick.EmissiveColor);
        Assert.Equal(new MaterialColor(0.9, 0.9, 0.9, 1.0), brick.TransmissionFilter);
        Assert.Equal(128.5, brick.SpecularExponent);
        Assert.Equal(1.45, brick.OpticalDensity);
        Assert.Equal(0.92, brick.Dissolve);
        Assert.Equal(2, brick.IlluminationModel);

        // Verify texture options and paths with spaces
        Assert.NotNull(brick.MapKa);
        Assert.Equal(new Vector3D(0.1, 0.2, 0.0), brick.MapKa!.Offset);
        Assert.Equal(new Vector3D(2.0, 2.0, 1.0), brick.MapKa.Scale);
        Assert.Equal("textures/brick wall/ambient map.png", brick.MapKa.TexturePath);

        Assert.NotNull(brick.MapKd);
        Assert.Equal("textures/brick wall/diffuse map.png", brick.MapKd!.TexturePath);

        Assert.NotNull(brick.MapKs);
        Assert.Equal(true, brick.MapKs!.Clamp);
        Assert.Equal("textures/specular.tga", brick.MapKs.TexturePath);

        Assert.NotNull(brick.MapNs);
        Assert.Equal("r", brick.MapNs!.Channel);
        Assert.Equal("textures/roughness.png", brick.MapNs.TexturePath);

        Assert.NotNull(brick.MapBump);
        Assert.Equal(0.75, brick.MapBump!.BumpMultiplier);
        Assert.Equal("textures/brick wall/normal bump.png", brick.MapBump.TexturePath);

        Assert.NotNull(brick.Refl);
        Assert.Equal("sphere", brick.Refl!.ReflectionType);
        Assert.Equal("textures/env_sphere.jpg", brick.Refl.TexturePath);

        Assert.NotNull(brick.Norm);
        Assert.Equal("textures/normal_directx.png", brick.Norm!.TexturePath);

        Assert.Single(brick.CustomProperties);
        Assert.Equal("Pr", brick.CustomProperties[0].Keyword);
        Assert.Equal("0.45", brick.CustomProperties[0].RawValue);

        MtlMaterial glass = result.Document.Materials[1];
        Assert.Equal("GlassPane", glass.Name);
        Assert.Equal(0.75, glass.Transparency);
        Assert.Equal(0.25, glass.EffectiveOpacity, precision: 6);
    }

    [Fact]
    public async Task UsmMaterialRoundTrip_AndMtlExporter_PreservesAllPropertiesAndDisambiguatesDuplicates()
    {
        var mat1 = new MtlMaterial("Metal")
        {
            AmbientColor = new MaterialColor(0.1, 0.1, 0.1),
            DiffuseColor = new MaterialColor(0.6, 0.65, 0.7),
            SpecularColor = new MaterialColor(0.9, 0.9, 0.9),
            SpecularExponent = 300.0,
            OpticalDensity = 1.6,
            Dissolve = 0.85,
            IlluminationModel = 2,
            MapKd = new MtlTextureMapStatement("map_Kd", "textures/metal base.png")
            {
                Scale = new Vector3D(2, 2, 1)
            },
            MapBump = new MtlTextureMapStatement("map_Bump", "textures/metal bump.png")
            {
                BumpMultiplier = 0.4
            }
        };
        mat1.CustomProperties.Add(new MtlProperty(10, "Pm", "0.95"));

        // Map to USM MaterialData and back
        MaterialData usm = MtlParser.ToUsmMaterial(mat1);
        Assert.Equal("Metal", usm.Name);
        Assert.Equal(0.85, usm.Opacity, precision: 6);
        Assert.Equal(TransparencyMode.AlphaBlend, usm.TransparencyMode);

        MtlMaterial restored = MtlParser.FromUsmMaterial(usm);
        Assert.Equal("Metal", restored.Name);
        Assert.Equal(mat1.AmbientColor, restored.AmbientColor);
        Assert.Equal(mat1.DiffuseColor, restored.DiffuseColor);
        Assert.Equal(mat1.SpecularColor, restored.SpecularColor);
        Assert.Equal(300.0, restored.SpecularExponent);
        Assert.Equal(1.6, restored.OpticalDensity);
        Assert.Equal(0.85, restored.Dissolve);
        Assert.Equal(2, restored.IlluminationModel);
        Assert.NotNull(restored.MapKd);
        Assert.Equal("textures/metal base.png", restored.MapKd!.TexturePath);
        Assert.Equal(new Vector3D(2, 2, 1), restored.MapKd.Scale);
        Assert.NotNull(restored.MapBump);
        Assert.Equal(0.4, restored.MapBump!.BumpMultiplier);
        Assert.Single(restored.CustomProperties);
        Assert.Equal("Pm", restored.CustomProperties[0].Keyword);

        // Export document with duplicate material name to verify disambiguation
        var doc = new MtlDocument();
        doc.Materials.Add(restored);
        doc.Materials.Add(restored.DeepClone());

        var exporter = new MtlExporter();
        using var outStream = new MemoryStream();
        var exportIssues = await exporter.ExportAsync(doc, outStream, ObjExportOptions.Default);

        Assert.Single(exportIssues); // Disambiguated 'Metal' -> 'Metal_2'

        outStream.Position = 0;
        var reParsed = await new MtlParser().ParseAsync(outStream);
        Assert.Equal(2, reParsed.Document.Materials.Count);
        Assert.Equal("Metal", reParsed.Document.Materials[0].Name);
        Assert.Equal("Metal_2", reParsed.Document.Materials[1].Name);
    }
}

