using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Validation;

/// <summary>
/// Orchestrates full validation of a <see cref="SchematicDocument"/> across dimensions, palette, block data, and block entities.
/// </summary>
public sealed class SchematicValidator : IValidator<SchematicDocument>
{
    private readonly SchematicDimensionValidator _dimensionValidator;
    private readonly BlockDataValidator _blockDataValidator;
    private readonly BlockEntityValidator _blockEntityValidator;

    public SchematicValidator(
        SchematicDimensionValidator? dimensionValidator = null,
        BlockDataValidator? blockDataValidator = null,
        BlockEntityValidator? blockEntityValidator = null)
    {
        _dimensionValidator = dimensionValidator ?? new SchematicDimensionValidator();
        _blockDataValidator = blockDataValidator ?? new BlockDataValidator();
        _blockEntityValidator = blockEntityValidator ?? new BlockEntityValidator();
    }

    public ValidationResult Validate(SchematicDocument? document)
    {
        var result = new ValidationResult();
        if (document is null)
        {
            result.AddError(SchematicErrorCodes.UnrecognizedSchematic, "SchematicDocument cannot be null.", "Document");
            return result;
        }

        if (document.Format == SchematicFormatId.Unknown)
        {
            result.AddError(SchematicErrorCodes.UnrecognizedSchematic, "Schematic format is Unknown.", "Format");
        }

        result.Merge(_dimensionValidator.Validate(document.Dimensions));
        if (result.IsValid)
        {
            result.Merge(_blockDataValidator.Validate(document.Palette, document.BlockIndices, document.Dimensions));
            result.Merge(_blockEntityValidator.Validate(document.BlockEntities, document.Dimensions));
        }

        return result;
    }
}

