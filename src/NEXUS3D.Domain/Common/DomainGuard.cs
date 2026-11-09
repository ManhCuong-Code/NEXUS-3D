using System.Runtime.CompilerServices;

namespace NEXUS3D.Domain.Common;

/// <summary>
/// Domain-specific guard clauses for USM entities and mathematical primitives.
/// </summary>
public static class DomainGuard
{
    public static T NotNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(value, paramName);
        return value;
    }

    public static string NotNullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    public static Guid NotEmpty(
        Guid value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Identifier cannot be Guid.Empty.", paramName);
        }

        return value;
    }

    public static double Finite(
        double value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "Numeric value must be finite (not NaN or Infinity).");
        }

        return value;
    }
}

