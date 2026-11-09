using NEXUS3D.Domain.Interfaces;

namespace NEXUS3D.Domain.Metadata;

public enum DataOriginKind
{
    Original = 0,
    Imported = 1,
    Generated = 2,
    Modified = 3,
    Converted = 4,
    Derived = 5
}

/// <summary>
/// Tracks provenance and transformation lineage of scene data.
/// </summary>
public sealed class DataProvenance : IDeepCloneable<DataProvenance>
{
    public DataOriginKind Origin { get; set; } = DataOriginKind.Original;
    public string ActorModule { get; set; } = "NEXUS3D.Domain";
    public string OperationName { get; set; } = "Create";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }

    public DataProvenance()
    {
    }

    public DataProvenance(DataOriginKind origin, string actorModule, string operationName, string? notes = null)
    {
        Origin = origin;
        ActorModule = actorModule ?? "NEXUS3D.Domain";
        OperationName = operationName ?? "Operation";
        TimestampUtc = DateTimeOffset.UtcNow;
        Notes = notes;
    }

    public DataProvenance DeepClone() => new()
    {
        Origin = Origin,
        ActorModule = ActorModule,
        OperationName = OperationName,
        TimestampUtc = TimestampUtc,
        Notes = Notes
    };
}

/// <summary>
/// Records a non-fatal warning encountered during scene inspection, import, or manipulation.
/// </summary>
public sealed class WarningRecord : IDeepCloneable<WarningRecord>
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SourceComponent { get; set; } = string.Empty;
    public Guid? TargetObjectId { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    public WarningRecord DeepClone() => new()
    {
        Code = Code,
        Message = Message,
        SourceComponent = SourceComponent,
        TargetObjectId = TargetObjectId,
        TimestampUtc = TimestampUtc
    };
}

public enum DataFidelityCategory
{
    Supported = 0,
    PreservedRaw = 1,
    Approximated = 2,
    Unmapped = 3,
    Lost = 4
}

public sealed record LossReportItem(
    string FeatureName,
    DataFidelityCategory Category,
    string Description,
    Guid? AffectedObjectId = null);

/// <summary>
/// Strictly distinguishes Supported Data, Preserved Raw Data, Approximated Data, Unmapped Data, and Lost Data.
/// Preserved Raw Data is never conflated with Lost Data.
/// </summary>
public sealed class LossReport : IDeepCloneable<LossReport>
{
    public List<LossReportItem> Items { get; set; } = [];

    public IReadOnlyList<LossReportItem> SupportedItems =>
        Items.Where(i => i.Category == DataFidelityCategory.Supported).ToList();

    public IReadOnlyList<LossReportItem> PreservedRawItems =>
        Items.Where(i => i.Category == DataFidelityCategory.PreservedRaw).ToList();

    public IReadOnlyList<LossReportItem> ApproximatedItems =>
        Items.Where(i => i.Category == DataFidelityCategory.Approximated).ToList();

    public IReadOnlyList<LossReportItem> UnmappedItems =>
        Items.Where(i => i.Category == DataFidelityCategory.Unmapped).ToList();

    public IReadOnlyList<LossReportItem> LostItems =>
        Items.Where(i => i.Category == DataFidelityCategory.Lost).ToList();

    public bool HasDataLoss => Items.Any(i => i.Category == DataFidelityCategory.Lost);

    public void RecordSupported(string featureName, string description, Guid? objectId = null) =>
        Items.Add(new LossReportItem(featureName, DataFidelityCategory.Supported, description, objectId));

    public void RecordPreservedRaw(string featureName, string description, Guid? objectId = null) =>
        Items.Add(new LossReportItem(featureName, DataFidelityCategory.PreservedRaw, description, objectId));

    public void RecordApproximated(string featureName, string description, Guid? objectId = null) =>
        Items.Add(new LossReportItem(featureName, DataFidelityCategory.Approximated, description, objectId));

    public void RecordUnmapped(string featureName, string description, Guid? objectId = null) =>
        Items.Add(new LossReportItem(featureName, DataFidelityCategory.Unmapped, description, objectId));

    public void RecordLost(string featureName, string description, Guid? objectId = null) =>
        Items.Add(new LossReportItem(featureName, DataFidelityCategory.Lost, description, objectId));

    public LossReport DeepClone() => new()
    {
        Items = Items.ToList()
    };
}

