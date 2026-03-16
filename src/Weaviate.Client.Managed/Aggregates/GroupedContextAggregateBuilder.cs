using System.Runtime.CompilerServices;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;

namespace Weaviate.Client.Managed.Aggregates;

/// <summary>
/// Builder for grouped aggregate queries at the context level.
/// Returned by <see cref="ContextAggregateBuilder{TProjection}.GroupBy"/> to provide
/// a type-safe API for grouped aggregations.
/// </summary>
/// <typeparam name="TProjection">The aggregation result type.</typeparam>
public sealed class GroupedContextAggregateBuilder<TProjection>
    where TProjection : class, new()
{
    private readonly CollectionClient _collection;
    private readonly IEnumerable<Aggregate.Metric> _metrics;
    private readonly Filter? _filter;
    private readonly Aggregate.GroupBy _groupBy;

    internal GroupedContextAggregateBuilder(
        CollectionClient collection,
        IEnumerable<Aggregate.Metric> metrics,
        Filter? filter,
        Aggregate.GroupBy groupBy
    )
    {
        _collection = collection;
        _metrics = metrics;
        _filter = filter;
        _groupBy = groupBy;
    }

    /// <summary>
    /// Executes the grouped aggregation query and returns typed results.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Typed grouped aggregate result.</returns>
    /// <example>
    /// <code>
    /// // Execute() is optional - directly awaitable
    /// var statsByCategory = await context.Aggregate&lt;ProductStats&gt;()
    ///     .GroupBy("category");
    ///
    /// foreach (var group in statsByCategory.Groups)
    /// {
    ///     Console.WriteLine($"Category: {group.GroupedBy.Value}");
    ///     Console.WriteLine($"  Count: {group.TotalCount}");
    ///     Console.WriteLine($"  Avg Price: {group.Properties.PriceMean:C}");
    /// }
    /// </code>
    /// </example>
    public async Task<AggregateGroupByResult<TProjection>> Execute(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _collection.Aggregate.OverAll(
            groupBy: _groupBy,
            totalCount: true,
            filters: _filter,
            returnMetrics: _metrics,
            cancellationToken: cancellationToken
        );

        return result.ToTyped<TProjection>();
    }

    /// <summary>
    /// Makes this grouped aggregate query directly awaitable without explicitly calling <see cref="Execute"/>.
    /// </summary>
    /// <returns>A task awaiter for the grouped aggregate result.</returns>
    public TaskAwaiter<AggregateGroupByResult<TProjection>> GetAwaiter()
    {
        return Execute(CancellationToken.None).GetAwaiter();
    }
}
