using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Query;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed;

/// <summary>
/// Fluent builder for aggregate queries with type-safe metric specification.
/// For typed aggregation (including grouping), use the AggregateStarter.WithMetrics&lt;TResult&gt;() method instead.
/// </summary>
/// <typeparam name="TModel">The model type being aggregated.</typeparam>
/// <example>
/// <code>
/// // Type-safe aggregation with callbacks
/// var stats = await products.Aggregate
///     .WithMetrics(
///         m => m.Property(p => p.Price, Metric.Number.Mean, Metric.Number.Min),
///         m => m.Property(p => p.Quantity, Metric.Integer.Sum)
///     );
///
/// // With filtering
/// var highValueStats = await products.Aggregate
///     .WithMetrics(m => m.Property(p => p.Price, Metric.Number.Mean))
///     .Where(p => p.Price > 100);
/// </code>
/// </example>
public sealed class ManagedCollectionAggregateBuilder<TModel>
    where TModel : class, new()
{
    private readonly CollectionClient _collection;
    private Filter? _filter;
    private IEnumerable<Aggregate.Metric>? _metrics;

    internal ManagedCollectionAggregateBuilder(
        CollectionClient collection,
        Filter? initialFilter = null,
        Aggregate.GroupBy? initialGroupBy = null,
        IEnumerable<Aggregate.Metric>? initialMetrics = null
    )
    {
        _collection = collection;
        _filter = initialFilter;
        _metrics = initialMetrics;
    }

    /// <summary>
    /// Filters the objects to aggregate using a type-safe expression.
    /// Multiple Where calls are combined with AND logic.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>The builder for chaining.</returns>
    /// <example>
    /// <code>
    /// var stats = await products.Aggregate
    ///     .WithMetrics(m => m.Property(p => p.Price, Metric.Number.Mean))
    ///     .Where(p => p.Price > 10)
    ///     .Where(p => p.InStock);
    /// </code>
    /// </example>
    public ManagedCollectionAggregateBuilder<TModel> Where(Expression<Func<TModel, bool>> predicate)
    {
        var filter = ExpressionToFilterConverter.Convert(predicate);
        _filter = _filter == null ? filter : Filter.AllOf(_filter, filter);
        return this;
    }

    /// <summary>
    /// Executes the aggregation query and returns typed results.
    /// </summary>
    /// <typeparam name="TResult">The result type to map aggregations to.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Typed aggregate result.</returns>
    /// <example>
    /// <code>
    /// var stats = await products.Aggregate()
    ///     .WithMetrics&lt;ProductStats&gt;()
    ///     .Execute&lt;ProductStats&gt;();
    ///
    /// Console.WriteLine($"Average: {stats.Properties.PriceMean:C}");
    /// Console.WriteLine($"Total: {stats.TotalCount}");
    /// </code>
    /// </example>
    public async Task<AggregateResult<TResult>> Execute<TResult>(
        CancellationToken cancellationToken = default
    )
        where TResult : class, new()
    {
        var result = await _collection.Aggregate.OverAll(
            totalCount: true,
            filters: _filter,
            returnMetrics: _metrics ?? Array.Empty<Aggregate.Metric>(),
            cancellationToken: cancellationToken
        );

        return result.ToTyped<TResult>();
    }

    /// <summary>
    /// Executes the aggregation query and returns the untyped result.
    /// Useful when you need access to the raw aggregate data.
    /// This method is optional - the builder is directly awaitable.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Untyped aggregate result.</returns>
    /// <example>
    /// <code>
    /// // Execute() is optional - directly awaitable
    /// var stats = await products.Aggregate
    ///     .WithMetrics(m => m.Property(p => p.Price, Metric.Number.Mean));
    /// </code>
    /// </example>
    public async Task<AggregateResult> Execute(CancellationToken cancellationToken = default)
    {
        return await _collection.Aggregate.OverAll(
            totalCount: true,
            filters: _filter,
            returnMetrics: _metrics ?? Array.Empty<Aggregate.Metric>(),
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Makes this aggregate builder directly awaitable without explicitly calling <see cref="Execute"/>.
    /// </summary>
    /// <returns>A task awaiter for the aggregate result.</returns>
    public TaskAwaiter<AggregateResult> GetAwaiter()
    {
        return Execute(CancellationToken.None).GetAwaiter();
    }
}
