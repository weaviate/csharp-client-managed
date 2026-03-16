using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Query;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed;

/// <summary>
/// Builder for grouped aggregate queries. Returned by <see cref="TypedAggregateBuilder{TModel,TResult}.GroupBy"/>
/// to provide a type-safe API for grouped aggregations.
/// </summary>
/// <typeparam name="TModel">The model type being aggregated.</typeparam>
/// <typeparam name="TResult">The result type containing aggregate metrics.</typeparam>
public sealed class GroupedAggregateBuilder<TModel, TResult>
    where TModel : class, new()
    where TResult : class, new()
{
    private readonly CollectionClient _collection;
    private readonly IEnumerable<Aggregate.Metric> _metrics;
    private readonly Filter? _filter;
    private readonly Aggregate.GroupBy _groupBy;

    internal GroupedAggregateBuilder(
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
    /// var statsByCategory = await products.Aggregate
    ///     .WithMetrics&lt;ProductStats&gt;()
    ///     .GroupBy(p => p.Category);
    ///
    /// foreach (var group in statsByCategory.Groups)
    /// {
    ///     Console.WriteLine($"Category: {group.GroupedBy.Value}");
    ///     Console.WriteLine($"  Count: {group.TotalCount}");
    ///     Console.WriteLine($"  Avg Price: {group.Properties.PriceMean:C}");
    /// }
    /// </code>
    /// </example>
    public async Task<AggregateGroupByResult<TResult>> Execute(
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

        return result.ToTyped<TResult>();
    }

    /// <summary>
    /// Makes this grouped aggregate query directly awaitable without explicitly calling <see cref="Execute"/>.
    /// </summary>
    /// <returns>A task awaiter for the grouped aggregate result.</returns>
    public TaskAwaiter<AggregateGroupByResult<TResult>> GetAwaiter()
    {
        return Execute(CancellationToken.None).GetAwaiter();
    }
}
