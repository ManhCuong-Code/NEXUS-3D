namespace NEXUS3D.Domain.Validation;

/// <summary>
/// Severity level of a validation finding in the Universal Scene Model.
/// </summary>
public enum ValidationSeverity
{
    Information = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

/// <summary>
/// Represents a single validation issue with standardized USM error code, severity, property path, and target object ID.
/// </summary>
public sealed class ValidationIssue
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
    public string? PropertyPath { get; set; }
    public Guid? ObjectId { get; set; }

    public ValidationIssue()
    {
    }

    public ValidationIssue(
        string code,
        string message,
        ValidationSeverity severity = ValidationSeverity.Error,
        string? propertyPath = null,
        Guid? objectId = null)
    {
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
        Severity = severity;
        PropertyPath = propertyPath;
        ObjectId = objectId;
    }
}

/// <summary>
/// Aggregates validation issues across USM scene components without halting on the first non-fatal issue.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<ValidationIssue> _issues = [];

    public IReadOnlyList<ValidationIssue> Issues => _issues;

    public bool HasErrors =>
        _issues.Any(x =>
            x.Severity == ValidationSeverity.Error ||
            x.Severity == ValidationSeverity.Critical);

    public bool HasWarnings =>
        _issues.Any(x => x.Severity == ValidationSeverity.Warning);

    public bool IsValid => !HasErrors;

    public void Add(ValidationIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        _issues.Add(issue);
    }

    public void AddError(string code, string message, string? propertyPath = null, Guid? objectId = null) =>
        _issues.Add(new ValidationIssue(code, message, ValidationSeverity.Error, propertyPath, objectId));

    public void AddWarning(string code, string message, string? propertyPath = null, Guid? objectId = null) =>
        _issues.Add(new ValidationIssue(code, message, ValidationSeverity.Warning, propertyPath, objectId));

    public void AddInfo(string code, string message, string? propertyPath = null, Guid? objectId = null) =>
        _issues.Add(new ValidationIssue(code, message, ValidationSeverity.Information, propertyPath, objectId));

    public void AddRange(IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        _issues.AddRange(issues);
    }

    public void Merge(ValidationResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _issues.AddRange(other._issues);
    }

    public static ValidationResult Success() => new();

    public static ValidationResult Failure(IEnumerable<ValidationIssue> issues)
    {
        var result = new ValidationResult();
        result.AddRange(issues);
        return result;
    }
}

/// <summary>
/// Generic validator contract for USM domain components.
/// </summary>
public interface IValidator<in T>
{
    ValidationResult Validate(T value);
}

