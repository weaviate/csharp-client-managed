using System.Linq.Expressions;
using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Query;

/// <summary>
/// Generic configuration builder for queries.
/// Allows entities and projections to configure query options including filters and sorting.
/// </summary>
/// <typeparam name="T">The entity type being queried.</typeparam>
/// <remarks>
/// ConfigureSearch hooks can be defined at three levels with this precedence (highest to lowest):
/// <list type="number">
/// <item><description>Explicit query calls: <c>.Limit(27)</c> always takes precedence</description></item>
/// <item><description>Projection-level: <c>static void ConfigureSearch(QueryConfig&lt;T&gt; q)</c> on projection class</description></item>
/// <item><description>Entity-level: <c>static void ConfigureSearch(QueryConfig&lt;T&gt; q)</c> on entity class</description></item>
/// </list>
/// </remarks>
/// <example>
/// Entity-level configuration:
/// <code>
/// [WeaviateCollection("Products")]
/// public class Product
/// {
///     public static void ConfigureSearch(QueryConfig&lt;Product&gt; q)
///     {
///         q.Where(p => p.IsActive)
///          .OrderByDescending(p => p.CreatedAt)
///          .Limit(100u)
///          .WithMetadata(MetadataQuery.Score);
///     }
/// }
/// </code>
/// Projection-level configuration (overrides entity):
/// <code>
/// [QueryProjection&lt;Product&gt;]
/// public class ProductSummary
/// {
///     public string Name { get; set; }
///
///     public static void ConfigureSearch(QueryConfig&lt;Product&gt; q)
///     {
///         q.Where(p => p.InStock).Limit(10u); // Overrides entity settings
///     }
/// }
/// </code>
/// Explicit calls override everything:
/// <code>
/// // Uses ProductSummary.ConfigureSearch settings
/// var results = context.Products.Query&lt;ProductSummary&gt;().Execute();
///
/// // Explicit limit (27) overrides everything
/// var results2 = context.Products.Query&lt;ProductSummary&gt;().Limit(27u).Execute();
/// </code>
/// </example>
public class QueryConfig<T>
    where T : class, new()
{
    private readonly CollectionMapperQueryClient<T> _queryClient;

    internal QueryConfig(CollectionMapperQueryClient<T> queryClient)
    {
        _queryClient = queryClient;
    }

    /// <summary>
    /// Filter results using a type-safe lambda expression.
    /// Multiple Where calls are combined with AND logic.
    /// </summary>
    public QueryConfig<T> Where(Expression<Func<T, bool>> predicate)
    {
        _queryClient.Where(predicate);
        return this;
    }

    /// <summary>
    /// Limit the number of results returned.
    /// </summary>
    public QueryConfig<T> Limit(uint limit)
    {
        _queryClient.Limit(limit);
        return this;
    }

    /// <summary>
    /// Skip the first N results.
    /// </summary>
    public QueryConfig<T> Offset(uint offset)
    {
        _queryClient.Offset(offset);
        return this;
    }

    /// <summary>
    /// Sort results by a property in ascending order.
    /// </summary>
    public QueryConfig<T> OrderBy<TProp>(Expression<Func<T, TProp>> property)
    {
        _queryClient.OrderBy(property);
        return this;
    }

    /// <summary>
    /// Sort results by a property in descending order.
    /// </summary>
    public QueryConfig<T> OrderByDescending<TProp>(Expression<Func<T, TProp>> property)
    {
        _queryClient.OrderByDescending(property);
        return this;
    }

    /// <summary>
    /// Include specific metadata fields in results.
    /// </summary>
    public QueryConfig<T> WithMetadata(MetadataOptions options)
    {
        _queryClient.WithMetadata(options);
        return this;
    }

    /// <summary>
    /// Automatically determine limit based on groupBy task.
    /// </summary>
    public QueryConfig<T> AutoLimit(uint limit)
    {
        _queryClient.AutoLimit(limit);
        return this;
    }

    /// <summary>
    /// Cursor-based pagination: start after this UUID.
    /// </summary>
    public QueryConfig<T> After(Guid uuid)
    {
        _queryClient.After(uuid);
        return this;
    }
}
