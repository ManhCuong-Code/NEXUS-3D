using NEXUS3D.Domain.Common;
using NEXUS3D.Domain.Voxels;

namespace NEXUS3D.Domain.Validation;

/// <summary>
/// Validates <see cref="VoxelGrid"/> bounds, voxel sizes, palette indices, and block entity raw payloads.
/// </summary>
public sealed class VoxelGridValidator : IValidator<VoxelGrid>
{
    public ValidationResult Validate(VoxelGrid? grid)
    {
        var result = new ValidationResult();
        if (grid is null)
        {
            result.AddError(UsmErrorCodes.InvalidVoxelGrid, "VoxelGrid cannot be null.", "VoxelGrid");
            return result;
        }

        if (grid.Id == Guid.Empty)
        {
            result.AddError(UsmErrorCodes.InvalidVoxelGrid, "VoxelGrid ID cannot be Guid.Empty.", "VoxelGrid.Id");
        }

        if (string.IsNullOrWhiteSpace(grid.Name))
        {
            result.AddError(UsmErrorCodes.InvalidVoxelGrid, "VoxelGrid name cannot be empty.", "VoxelGrid.Name", grid.Id);
        }

        if (!grid.VoxelSize.IsValid)
        {
            result.AddError(
                UsmErrorCodes.InvalidVoxelGrid,
                $"VoxelSize ({grid.VoxelSize.X}, {grid.VoxelSize.Y}, {grid.VoxelSize.Z}) must be positive and finite.",
                "VoxelGrid.VoxelSize",
                grid.Id);
        }

        int paletteCount = grid.Palette.Count;
        int voxelCount = grid.GetVoxelCount();

        if (voxelCount > 0 && !grid.Bounds.IsValid)
        {
            result.AddError(
                UsmErrorCodes.InvalidVoxelGrid,
                "VoxelGrid contains voxels but Bounds are invalid or empty.",
                "VoxelGrid.Bounds",
                grid.Id);
        }

        foreach (var voxel in grid.Voxels)
        {
            if (voxel.PaletteIndex < 0 || (paletteCount > 0 && voxel.PaletteIndex >= paletteCount) || (paletteCount == 0 && voxel.State is null))
            {
                result.AddError(
                    UsmErrorCodes.InvalidPaletteIndex,
                    $"Voxel at {voxel.Position} has out-of-range PaletteIndex {voxel.PaletteIndex} (PaletteCount={paletteCount}).",
                    $"VoxelGrid.Voxels[{voxel.Position}].PaletteIndex",
                    grid.Id);
            }

            if (grid.Bounds.IsValid && !grid.Bounds.Contains(voxel.Position))
            {
                result.AddError(
                    UsmErrorCodes.InvalidVoxelGrid,
                    $"Voxel at {voxel.Position} lies outside declared VoxelGridBounds.",
                    $"VoxelGrid.Voxels[{voxel.Position}].Position",
                    grid.Id);
            }

            if (voxel.BlockEntity?.RawData is { } rawData && !rawData.IsValid())
            {
                result.AddError(
                    UsmErrorCodes.InvalidRawData,
                    $"Voxel at {voxel.Position} has an invalid or oversized BlockEntity RawDataContainer.",
                    $"VoxelGrid.Voxels[{voxel.Position}].BlockEntity.RawData",
                    grid.Id);
            }
        }

        return result;
    }
}

