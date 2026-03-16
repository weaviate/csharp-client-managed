using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Query;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed;

/// <summary>
/// Typed fluent builder for aggregate queries that knows both the model type and result type.
/// Automatically extracts metrics from the result type and doesn't require repeating the type in Execute().
/// </summary>
/// <typeparam name="TModel">The model type being aggregated.</typeparam>
/// <typeparam name="TResult">The result type containing aggregate metrics.</typeparam>
public sealed class TypedAggregateBuilder<TModel, TResult>
    where TModel : class, new()
    where TResult : class, new()
{
    private readonly CollectionClient _collection;
    private readonly IEnumerable<Aggregate.Metric> _metrics;
    private Filter? _filter;

    internal TypedAggregateBuilder(
        CollectionClient collection,
        Filter? initialFilter,
        Aggregate.GroupBy? initialGroupBy
    )
    {
        _collection = collection;
        _filter = initialFilter;

        // Extract metrics immediately for fail-fast behavior
        _metrics = MetricsExtractor.FromType<TResult>();
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
    ///     .WithMetrics&lt;ProductStats&gt;()
    ///     .Where(p => p.Price > 10)
    ///     .Where(p => p.InStock)
    ///     .Execute();
    /// </code>
    /// </example>
    public TypedAggregateBuilder<TModel, TResult> Where(Expression<Func<TModel, bool>> predicate)
    {
        var filter = ExpressionToFilterConverter.Convert(predicate);
        _filter = _filter == null ? filter : Filter.AllOf(_filter, filter);
        return this;
    }

    /// <summary>
    /// Groups the aggregation results by a property.
    /// Returns a <see cref="GroupedAggregateBuilder{TModel, TResult}"/> that can be directly awaited.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">The property to group by.</param>
    /// <param name="limit">Maximum number of groups to return.</param>
    /// <returns>A grouped aggregate builder that can be awaited directly.</returns>
    /// <example>
    /// <code>
    /// // Execute() is optional - directly awaitable
    /// var statsByCategory = await products.Aggregate
    ///     .WithMetrics&lt;ProductStats&gt;()
    ///     .GroupBy(p => p.Category, limit: 10);
    ///
    /// foreach (var group in statsByCategory.Groups)
    /// {
    ///     Console.WriteLine($"{group.GroupedBy.Value}: {group.Properties.PriceMean:C}");
    /// }
    /// </code>
    /// </example>
    public GroupedAggregateBuilder<TModel, TResult> GroupBy<TProp>(
        Expression<Func<TModel, TProp>> property,
        uint? limit = null
    )
    {
        var propName = PropertyHelper.GetPropertyName(property);
        var camelName = PropertyHelper.ToCamelCase(propName);
        var groupBy = new Aggregate.GroupBy(camelName, limit);
        return new GroupedAggregateBuilder<TModel, TResult>(
            _collection,
            _metrics,
            _filter,
            groupBy
        );
    }

    /// <summary>
    /// Executes the aggregation query and returns typed results.
    /// The result type was already specified in WithMetrics, so no type parameter is needed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Typed aggregate result.</returns>
    /// <example>
    /// <code>
    /// // Execute() is optional - directly awaitable
    /// var stats = await products.Aggregate
    ///     .WithMetrics&lt;ProductStats&gt;();
    ///
    /// Console.WriteLine($"Average: {stats.Properties.PriceMean:C}");
    /// Console.WriteLine($"Total: {stats.TotalCount}");
    /// </code>
    /// </example>
    public async Task<AggregateResult<TResult>> Execute(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _collection.Aggregate.OverAll(
            totalCount: true,
            filters: _filter,
            returnMetrics: _metrics,
            cancellationToken: cancellationToken
        );

        return result.ToTyped<TResult>();
    }

    /// <summary>
    /// Makes this aggregate query directly awaitable without explicitly calling <see cref="Execute"/>.
    /// </summary>
    /// <returns>A task awaiter for the aggregate result.</returns>
    public TaskAwaiter<AggregateResult<TResult>> GetAwaiter()
    {
        return Execute(CancellationToken.None).GetAwaiter();
    }
}
