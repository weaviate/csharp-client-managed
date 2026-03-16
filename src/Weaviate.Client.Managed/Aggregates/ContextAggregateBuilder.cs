using System.Runtime.CompilerServices;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;

namespace Weaviate.Client.Managed.Aggregates;

/// <summary>
/// Aggregate builder for context-level aggregation where the collection is inferred
/// from the [QueryAggregate&lt;T&gt;] attribute on the projection type.
/// Since the model type is only known at runtime, filtering uses raw <see cref="Filter"/>
/// instead of typed expressions.
/// </summary>
/// <typeparam name="TProjection">The aggregation result type.</typeparam>
public sealed class ContextAggregateBuilder<TProjection>
    where TProjection : class, new()
{
    private readonly CollectionClient _collection;
    private readonly IEnumerable<Aggregate.Metric> _metrics;
    private Filter? _filter;

    internal ContextAggregateBuilder(CollectionClient collection)
    {
        _collection = collection;
        _metrics = MetricsExtractor.FromType<TProjection>();
    }

    /// <summary>
    /// Filters the objects to aggregate using a raw filter.
    /// Multiple Where calls are combined with AND logic.
    /// </summary>
    /// <param name="filter">The filter to apply.</param>
    /// <returns>The builder for chaining.</returns>
    public ContextAggregateBuilder<TProjection> Where(Filter filter)
    {
        _filter = _filter == null ? filter : Filter.AllOf(_filter, filter);
        return this;
    }

    /// <summary>
    /// Groups the aggregation results by a property name.
    /// Returns a <see cref="GroupedContextAggregateBuilder{TProjection}"/> that can be directly awaited.
    /// </summary>
    /// <param name="propertyName">The camelCase property name to group by.</param>
    /// <param name="limit">Maximum number of groups to return.</param>
    /// <returns>A grouped aggregate builder that can be awaited directly.</returns>
    public GroupedContextAggregateBuilder<TProjection> GroupBy(
        string propertyName,
        uint? limit = null
    )
    {
        var groupBy = new Aggregate.GroupBy(propertyName, limit);
        return new GroupedContextAggregateBuilder<TProjection>(
            _collection,
            _metrics,
            _filter,
            groupBy
        );
    }

    /// <summary>
    /// Executes the aggregation query and returns typed results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Typed aggregate result.</returns>
    public async Task<AggregateResult<TProjection>> Execute(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _collection.Aggregate.OverAll(
            totalCount: true,
            filters: _filter,
            returnMetrics: _metrics,
            cancellationToken: cancellationToken
        );

        return result.ToTyped<TProjection>();
    }

    /// <summary>
    /// Makes this aggregate query directly awaitable without explicitly calling <see cref="Execute"/>.
    /// </summary>
    /// <returns>A task awaiter for the aggregate result.</returns>
    public TaskAwaiter<AggregateResult<TProjection>> GetAwaiter()
    {
        return Execute(CancellationToken.None).GetAwaiter();
    }
}
