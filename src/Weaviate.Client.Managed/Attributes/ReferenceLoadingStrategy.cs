namespace Weaviate.Client.Managed.Attributes;

/// <summary>
/// Specifies how references are loaded in queries.
/// </summary>
public enum ReferenceLoadingStrategy
{
    /// <summary>
    /// References are not loaded unless explicitly requested via .WithReferences().
    /// This is the default behaviour.
    /// </summary>
    Explicit,

    /// <summary>
    /// References are automatically included in every query for this entity.
    /// </summary>
    Eager,
}
