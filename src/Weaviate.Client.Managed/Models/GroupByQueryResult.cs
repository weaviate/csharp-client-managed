namespace Weaviate.Client.Managed.Models;

/// <summary>
/// A single search result that belongs to a named group, returned by
/// <see cref="Query.GroupByQueryExecutor{T}.Execute"/>.
/// </summary>
public record GroupByQueryResult<T> : QueryResult<T>
{
    /// <summary>The name of the group this object was bucketed into.</summary>
    public required string BelongsToGroup { get; init; }
}

/// <summary>
/// A single bucket in a grouped search response.
/// Contains the objects that share the same property value and the
/// distance range of those objects from the query vector.
/// </summary>
public record GroupByGroup<T>
{
    /// <summary>The property value that identifies this group.</summary>
    public required string Name { get; init; }

    /// <summary>Objects in this group, ordered by relevance.</summary>
    public required IList<GroupByQueryResult<T>> Objects { get; init; }

    /// <summary>Distance of the closest object in the group to the query vector.</summary>
    public float MinDistance { get; init; }

    /// <summary>Distance of the furthest object in the group to the query vector.</summary>
    public float MaxDistance { get; init; }

    /// <summary>Number of objects in this group.</summary>
    public int Count => Objects.Count;
}

/// <summary>
/// The full response from a grouped search query. Provides both a flat view
/// of all matched objects (each tagged with <see cref="GroupByQueryResult{T}.BelongsToGroup"/>)
/// and a structured view keyed by group name.
/// </summary>
public record GroupByQueryResponse<T>
{
    /// <summary>
    /// All matched objects across every group, each tagged with the group they belong to.
    /// </summary>
    public required IList<GroupByQueryResult<T>> Objects { get; init; }

    /// <summary>
    /// Groups keyed by their name (the shared property value).
    /// Each group exposes <see cref="GroupByGroup{T}.MinDistance"/> /
    /// <see cref="GroupByGroup{T}.MaxDistance"/> for the bucket.
    /// </summary>
    public required IDictionary<string, GroupByGroup<T>> Groups { get; init; }
}
