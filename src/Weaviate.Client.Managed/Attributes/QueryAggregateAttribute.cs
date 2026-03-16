namespace Weaviate.Client.Managed.Attributes;

/// <summary>
/// Marks a type as an aggregation result type for a specific collection entity type.
/// Similar to <see cref="QueryProjectionAttribute{TCollection}"/> but for aggregate operations.
/// </summary>
/// <typeparam name="TCollection">The collection entity type this aggregation applies to.</typeparam>
/// <example>
/// <code>
/// [QueryAggregate&lt;Product&gt;]
/// public class ProductStats
/// {
///     [Metrics(Metric.Number.Mean, Metric.Number.Sum)]
///     public Aggregate.Number Price { get; set; }
///
///     [Metrics(Metric.Integer.Count)]
///     public Aggregate.Integer Stock { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class QueryAggregateAttribute<TCollection> : Attribute
    where TCollection : class, new()
{
    /// <summary>
    /// Gets the collection entity type.
    /// </summary>
    public Type CollectionType => typeof(TCollection);
}
