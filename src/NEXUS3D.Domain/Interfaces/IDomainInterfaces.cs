namespace NEXUS3D.Domain.Interfaces;

/// <summary>
/// Marks a domain object that possesses a stable, globally unique identifier.
/// </summary>
public interface IIdentifiable
{
    Guid Id { get; }
}

/// <summary>
/// Supports creating an independent deep copy of a domain object.
/// </summary>
public interface IDeepCloneable<out T>
{
    T DeepClone();
}

/// <summary>
/// Base contract for named components participating in the Universal Scene Model.
/// </summary>
public interface ISceneComponent : IIdentifiable
{
    string Name { get; set; }
}

