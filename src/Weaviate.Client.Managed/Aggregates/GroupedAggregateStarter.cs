using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Query;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Aggregates;

/// <summary>
/// Starting point for grouped aggregate operations.
/// Returned by <see cref="AggregateStarter{TModel}.GroupBy"/> to provide a type-safe API.
/// </summary>
/// <typeparam name="TModel">The model type being aggregated.</typeparam>
public sealed class GroupedAggregateStarter<TModel>
    where TModel : class, new()
{
    private readonly CollectionClient _collection;
    private readonly Filter? _filter;
    private readonly Aggregate.GroupBy _groupBy;

    internal GroupedAggregateStarter(
        CollectionClient collection,
        Filter? filter,
        Aggregate.GroupBy groupBy
    )
    {
        _collection = collection;
        _filter = filter;
        _groupBy = groupBy;
    }

    /// <summary>
    /// Specifies which metrics to aggregate by automatically extracting them from a result type.
    /// Returns a grouped aggregate builder that can be directly awaited.
    /// </summary>
    /// <typeparam name="TResult">The result type defining which metrics to collect.</typeparam>
    /// <returns>A grouped aggregate builder.</returns>
    /// <example>
    /// <code>
    /// // Execute() is optional - directly awaitable
    /// var statsByCategory = await products.Aggregate
    ///     .GroupBy(p => p.Category)
    ///     .WithMetrics&lt;ProductStats&gt;();
    ///
    /// foreach (var group in statsByCategory.Groups)
    /// {
    ///     Console.WriteLine($"{group.GroupedBy.Value}: {group.Properties.PriceMean:C}");
    /// }
    /// </code>
    /// </example>
    public GroupedAggregateBuilder<TModel, TResult> WithMetrics<TResult>()
        where TResult : class, new()
    {
        var metrics = MetricsExtractor.FromType<TResult>();
        return new GroupedAggregateBuilder<TModel, TResult>(
            _collection,
            metrics,
            _filter,
            _groupBy
        );
    }

    /// <summary>
    /// Executes a count-only grouped aggregation without any metrics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Untyped grouped aggregate result with TotalCount per group.</returns>
    /// <example>
    /// <code>
    /// // Execute() is optional - directly awaitable
    /// var result = await products.Aggregate
    ///     .GroupBy(p => p.Category);
    ///
    /// foreach (var group in result.Groups)
    /// {
    ///     Console.WriteLine($"{group.GroupedBy.Value}: {group.TotalCount} items");
    /// }
    /// </code>
    /// </example>
    public async Task<AggregateGroupByResult> Execute(CancellationToken cancellationToken = default)
    {
        return await _collection.Aggregate.OverAll(
            groupBy: _groupBy,
            totalCount: true,
            filters: _filter,
            returnMetrics: Array.Empty<Aggregate.Metric>(),
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Makes this grouped aggregate query directly awaitable without explicitly calling <see cref="Execute"/>.
    /// </summary>
    /// <returns>A task awaiter for the grouped aggregate result.</returns>
    public TaskAwaiter<AggregateGroupByResult> GetAwaiter()
    {
        return Execute(CancellationToken.None).GetAwaiter();
    }
}
