using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Query;
using Weaviate.Client.Models;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Aggregates;

/// <summary>
/// Starting point for aggregate operations on a mapped collection.
/// Provides methods to configure filtering, grouping, and metrics before execution.
/// </summary>
/// <typeparam name="TModel">The model type being aggregated.</typeparam>
public sealed class AggregateStarter<TModel>
    where TModel : class, new()
{
    private readonly CollectionClient _collection;
    private Filter? _filter;

    internal AggregateStarter(CollectionClient collection)
    {
        _collection = collection;
    }

    /// <summary>
    /// Specifies which metrics to aggregate by automatically extracting them from a result type.
    /// Returns a typed builder that doesn't require specifying the type again in Execute().
    /// </summary>
    /// <typeparam name="TResult">The result type defining which metrics to collect.</typeparam>
    /// <returns>A typed aggregate builder.</returns>
    /// <example>
    /// <code>
    /// public class ProductStats
    /// {
    ///     public double? PriceMean { get; set; }
    ///     public long? QuantitySum { get; set; }
    /// }
    ///
    /// var stats = await products.Aggregate
    ///     .WithMetrics&lt;ProductStats&gt;()  // Type specified once
    ///     .Execute();                      // No type needed here
    /// </code>
    /// </example>
    public TypedAggregateBuilder<TModel, TResult> WithMetrics<TResult>()
        where TResult : class, new()
    {
        return new TypedAggregateBuilder<TModel, TResult>(_collection, _filter, null);
    }

    /// <summary>
    /// Specifies explicit metrics to aggregate using type-safe callbacks.
    /// Returns an untyped builder for manual metric specification.
    /// </summary>
    /// <param name="metricBuilders">Callback functions that build metrics using MetricsBuilder.</param>
    /// <returns>An untyped aggregate builder.</returns>
    /// <example>
    /// <code>
    /// // Type-safe with expression trees
    /// var stats = await products.Aggregate
    ///     .WithMetrics(
    ///         m => m.Property(p => p.Price, Metric.Number.Mean, Metric.Number.Min, Metric.Number.Max),
    ///         m => m.Property(p => p.Quantity, Metric.Integer.Sum)
    ///     );
    /// </code>
    /// </example>
    public ManagedCollectionAggregateBuilder<TModel> WithMetrics(
        params Func<MetricsBuilder<TModel>, Aggregate.Metric>[] metricBuilders
    )
    {
        var builder = new MetricsBuilder<TModel>();
        var metrics = metricBuilders.Select(mb => mb(builder)).ToArray();
        return new ManagedCollectionAggregateBuilder<TModel>(_collection, _filter, null, metrics);
    }

    /// <summary>
    /// Filters the objects to aggregate using a type-safe expression.
    /// Multiple Where calls are combined with AND logic.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>The starter for chaining.</returns>
    /// <example>
    /// <code>
    /// var stats = await products.Aggregate
    ///     .Where(p => p.Price > 10)
    ///     .Where(p => p.InStock)
    ///     .WithMetrics&lt;ProductStats&gt;()
    ///     .Execute();
    /// </code>
    /// </example>
    public AggregateStarter<TModel> Where(Expression<Func<TModel, bool>> predicate)
    {
        var filter = ExpressionToFilterConverter.Convert(predicate);
        _filter = _filter == null ? filter : Filter.AllOf(_filter, filter);
        return this;
    }

    /// <summary>
    /// Groups the aggregation results by a property.
    /// Returns a <see cref="GroupedAggregateStarter{TModel}"/> that can be directly awaited.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">The property to group by.</param>
    /// <param name="limit">Maximum number of groups to return.</param>
    /// <returns>A grouped aggregate starter.</returns>
    /// <example>
    /// <code>
    /// // Count per group - Execute() is optional
    /// var result = await products.Aggregate
    ///     .GroupBy(p => p.Category, limit: 10);
    ///
    /// // Or with metrics
    /// var statsByCategory = await products.Aggregate
    ///     .GroupBy(p => p.Category, limit: 10)
    ///     .WithMetrics&lt;ProductStats&gt;();
    /// </code>
    /// </example>
    public GroupedAggregateStarter<TModel> GroupBy<TProp>(
        Expression<Func<TModel, TProp>> property,
        uint? limit = null
    )
    {
        var propName = PropertyHelper.GetPropertyName(property);
        var camelName = PropertyHelper.ToCamelCase(propName);
        var groupBy = new Aggregate.GroupBy(camelName, limit);
        return new GroupedAggregateStarter<TModel>(_collection, _filter, groupBy);
    }

    /// <summary>
    /// Executes a count-only aggregation without any metrics.
    /// This method is optional - the starter is directly awaitable.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Untyped aggregate result with TotalCount.</returns>
    /// <example>
    /// <code>
    /// // Execute() is optional - directly awaitable
    /// var result = await products.Aggregate
    ///     .Where(p => p.InStock);
    ///
    /// Console.WriteLine($"Total in stock: {result.TotalCount}");
    /// </code>
    /// </example>
    public async Task<AggregateResult> Execute(CancellationToken cancellationToken = default)
    {
        return await _collection.Aggregate.OverAll(
            totalCount: true,
            filters: _filter,
            returnMetrics: Array.Empty<Aggregate.Metric>(),
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Makes this aggregate starter directly awaitable without explicitly calling <see cref="Execute"/>.
    /// </summary>
    /// <returns>A task awaiter for the aggregate result.</returns>
    public TaskAwaiter<AggregateResult> GetAwaiter()
    {
        return Execute(CancellationToken.None).GetAwaiter();
    }
}
