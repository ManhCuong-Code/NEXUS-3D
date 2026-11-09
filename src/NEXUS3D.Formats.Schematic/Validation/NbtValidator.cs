using NEXUS3D.Domain.Validation;
using NEXUS3D.Formats.Schematic.Nbt;
using NEXUS3D.Formats.Schematic.Nbt.IO;
using NEXUS3D.Formats.Schematic.Nbt.Tags;

namespace NEXUS3D.Formats.Schematic.Validation;

/// <summary>
/// Validates <see cref="NbtDocument"/> structural integrity, nesting depth, and list homogeneity.
/// </summary>
public sealed class NbtValidator : IValidator<NbtDocument>
{
    private readonly int _maxDepth;

    public NbtValidator(int maxDepth = 128)
    {
        _maxDepth = maxDepth > 0 ? maxDepth : 128;
    }

    public ValidationResult Validate(NbtDocument? document)
    {
        var result = new ValidationResult();
        if (document is null || document.RootTag is null)
        {
            result.AddError(NbtErrorCodes.InvalidNbt, "NbtDocument or its RootTag cannot be null.", "RootTag");
            return result;
        }

        var stack = new Stack<(NbtTag Tag, int Depth, string Path)>();
        stack.Push((document.RootTag, 1, string.IsNullOrEmpty(document.RootTag.Name) ? "Root" : document.RootTag.Name));

        while (stack.Count > 0)
        {
            var (current, depth, path) = stack.Pop();
            if (depth > _maxDepth)
            {
                result.AddError(
                    NbtErrorCodes.LimitExceeded,
                    $"NBT tag at '{path}' exceeds maximum nesting depth ({_maxDepth}).",
                    path);
                continue;
            }

            switch (current)
            {
                case NbtCompoundTag comp:
                    foreach (var child in comp.GetAllTags())
                    {
                        if (child.Type == NbtTagType.End)
                        {
                            result.AddError(NbtErrorCodes.InvalidNbt, $"Compound '{path}' contains an explicit TAG_End entry.", path);
                        }
                        stack.Push((child, depth + 1, $"{path}.{child.Name}"));
                    }
                    break;

                case NbtListTag list:
                    for (int i = 0; i < list.Count; i++)
                    {
                        NbtTag item = list[i];
                        if (item.Type != list.ElementType)
                        {
                            result.AddError(
                                NbtErrorCodes.InvalidNbt,
                                $"List '{path}' item[{i}] has mismatched type '{item.Type}', expected '{list.ElementType}'.",
                                $"{path}[{i}]");
                        }
                        stack.Push((item, depth + 1, $"{path}[{i}]"));
                    }
                    break;

                case NbtFloatTag f when !float.IsFinite(f.Value):
                    result.AddWarning(NbtErrorCodes.InvalidNbt, $"Float tag '{path}' is NaN or Infinity.", path);
                    break;

                case NbtDoubleTag d when !double.IsFinite(d.Value):
                    result.AddWarning(NbtErrorCodes.InvalidNbt, $"Double tag '{path}' is NaN or Infinity.", path);
                    break;
            }
        }

        return result;
    }
}
