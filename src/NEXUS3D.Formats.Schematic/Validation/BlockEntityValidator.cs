using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Schematic.BlockEntities;
using NEXUS3D.Formats.Schematic.Common;

namespace NEXUS3D.Formats.Schematic.Validation;

/// <summary>
/// Validates <see cref="SchematicBlockEntity"/> instances for non-empty IDs and bounds containment.
/// </summary>
public sealed class BlockEntityValidator
{
    public ValidationResult Validate(IReadOnlyList<SchematicBlockEntity> entities, SchematicDimensions dimensions)
    {
        var result = new ValidationResult();
        if (entities is null)
        {
            return result;
        }

        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                result.AddError(
                    SchematicErrorCodes.InvalidBlockEntity,
                    $"BlockEntity[{i}] has an empty Id.",
                    $"BlockEntities[{i}].Id");
            }

            if (dimensions.IsValid && !dimensions.Contains(entity.Position))
            {
                result.AddError(
                    SchematicErrorCodes.InvalidBlockEntity,
                    $"BlockEntity[{i}] ('{entity.Id}') at {entity.Position} lies outside schematic bounds ({dimensions.Width}x{dimensions.Height}x{dimensions.Length}).",
                    $"BlockEntities[{i}].Position");
            }
        }

        return result;
    }
}
