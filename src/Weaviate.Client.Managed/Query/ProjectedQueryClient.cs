using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Managed.Mapping;
using Weaviate.Client.Managed.Models;
using Weaviate.Client.Models;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Query;

/// <summary>
/// Fluent query builder for projected queries.
/// Wraps a <see cref="CollectionMapperQueryClient{T}"/> and forwards all query building methods,
/// but maps results to TProjection instead of T.
/// </summary>
/// <typeparam name="T">The entity type being queried.</typeparam>
/// <typeparam name="TProjection">The projection type for results.</typeparam>
/// <example>
/// <code>
/// var results = await collection.Query
///     .Where(a => a.WordCount > 100)
///     .NearText("technology")
///     .Project&lt;ArticleSummary&gt;()
///     .Limit(10)
///     .Execute();
/// </code>
/// </example>
public class ProjectedQueryClient<T, TProjection>
    where T : class, new()
    where TProjection : class, new()
{
    private readonly CollectionMapperQueryClient<T> _inner;

    // Discovered configure delegates from static methods on TProjection
    private readonly Func<NearTextConfig, NearTextConfig>? _nearTextConfigure;
    private readonly Func<NearVectorConfig, NearVectorConfig>? _nearVectorConfigure;
    private readonly Func<HybridConfig, HybridConfig>? _hybridConfigure;
    private readonly Func<NearObjectConfig, NearObjectConfig>? _nearObjectConfigure;
    private readonly Func<NearMediaConfig, NearMediaConfig>? _nearMediaConfigure;

    internal ProjectedQueryClient(CollectionMapperQueryClient<T> inner)
    {
        _inner = inner;
        _nearTextConfigure = DiscoverConfigureMethod<NearTextConfig>("ConfigureNearText");
        _nearVectorConfigure = DiscoverConfigureMethod<NearVectorConfig>("ConfigureNearVector");
        _hybridConfigure = DiscoverConfigureMethod<HybridConfig>("ConfigureHybrid");
        _nearObjectConfigure = DiscoverConfigureMethod<NearObjectConfig>("ConfigureNearObject");
        _nearMediaConfigure = DiscoverConfigureMethod<NearMediaConfig>("ConfigureNearMedia");
        AutoConfigure();
        ApplyConfigureSearchHook();
    }

    /// <summary>
    /// If TProjection defines static ConfigureSearch(QueryConfig&lt;T&gt;), invoke it to allow projections to chain query options.
    /// This overrides entity-level ConfigureSearch but can be overridden by explicit query method calls.
    /// </summary>
    private void ApplyConfigureSearchHook()
    {
        var method = typeof(TProjection).GetMethod(
            "ConfigureSearch",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(QueryConfig<T>) },
            null
        );
        if (method != null && method.ReturnType == typeof(void))
        {
            var config = new QueryConfig<T>(_inner);
            method.Invoke(null, new object[] { config });
        }
    }

    /// <summary>
    /// Auto-configures the underlying query based on TProjection's attributes.
    /// Sets return properties, vectors, and metadata to match what the projection needs.
    /// </summary>
    private void AutoConfigure()
    {
        // Configure return properties (only fetch what the projection needs)
        var sourcePropertyNames = ProjectionMapper.GetSourcePropertyNames<TProjection>();
        if (sourcePropertyNames.Count > 0)
        {
            _inner.SetReturnProperties(sourcePropertyNames);
        }

        // Configure vectors to include
        var vectorNames = ProjectionMapper.GetVectorNames<TProjection>();
        if (vectorNames.Count > 0)
        {
            _inner.AddIncludeVectors(vectorNames);
        }

        // Configure metadata to include
        var metadataOptions = ProjectionMapper.GetMetadataOptions<TProjection>();
        if (metadataOptions != MetadataOptions.None)
        {
            _inner.WithMetadata(metadataOptions);
        }

        // Configure references to include (from [Reference] attributes)
        var referenceNames = ProjectionMapper.GetReferenceNames<TProjection>();
        if (referenceNames.Count > 0)
        {
            _inner.AddIncludeReferences(referenceNames);
        }

        // Configure vector targeting:
        // ConfigureVectorTargets() static method on TProjection takes precedence over
        // [QueryProjection<T>(Combination = ...)] attribute.
        var dynamicTargetsFn = DiscoverVectorTargetsMethod();
        if (dynamicTargetsFn != null)
        {
            _inner.VectorTargets(dynamicTargetsFn);
        }
        else
        {
            var attributeTargetsFn = ProjectionMapper.GetVectorTargetConfig<TProjection, T>();
            if (attributeTargetsFn != null)
            {
                _inner.VectorTargets(attributeTargetsFn);
            }
        }
    }

    /// <summary>
    /// Discovers a static configure method on TProjection with signature:
    /// <c>static TConfig MethodName(TConfig)</c>.
    /// Returns null if the method does not exist or has an incompatible signature.
    /// </summary>
    private static Func<TConfig, TConfig>? DiscoverConfigureMethod<TConfig>(string methodName)
    {
        var method = typeof(TProjection).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(TConfig) },
            null
        );
        if (method == null || method.ReturnType != typeof(TConfig))
            return null;
        return config => (TConfig)method.Invoke(null, new object?[] { config })!;
    }

    /// <summary>
    /// Discovers a static <c>ConfigureVectorTargets</c> method on TProjection with signature:
    /// <c>static TargetVectorBuilder&lt;T&gt; ConfigureVectorTargets(TargetVectorBuilder&lt;T&gt;)</c>.
    /// Returns null if absent or signature doesn't match.
    /// </summary>
    private static Func<
        TargetVectorBuilder<T>,
        TargetVectorBuilder<T>
    >? DiscoverVectorTargetsMethod()
    {
        var builderType = typeof(TargetVectorBuilder<T>);
        var method = typeof(TProjection).GetMethod(
            "ConfigureVectorTargets",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { builderType },
            null
        );
        if (method == null || method.ReturnType != builderType)
            return null;
        return builder => (TargetVectorBuilder<T>)method.Invoke(null, new object[] { builder })!;
    }

    #region Filter Methods

    /// <summary>
    /// Filters results using a type-safe lambda expression on the entity type.
    /// Multiple Where calls are combined with AND logic.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> Where(Expression<Func<T, bool>> predicate)
    {
        _inner.Where(predicate);
        return this;
    }

    #endregion

    #region Vector Search Methods

    /// <summary>
    /// Performs a near text search using text-to-vector conversion.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> NearText(
        string text,
        Expression<Func<T, object>>? vector = null,
        float? certainty = null,
        float? distance = null
    )
    {
        _inner.NearText(text, vector, certainty, distance);
        return this;
    }

    /// <summary>
    /// Performs a near vector search using a provided vector.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> NearVector(
        float[] vectorValues,
        Expression<Func<T, object>>? vector = null,
        float? certainty = null,
        float? distance = null
    )
    {
        _inner.NearVector(vectorValues, vector, certainty, distance);
        return this;
    }

    /// <summary>
    /// Performs a near object search using an existing object's vector.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> NearObject(
        Guid objectId,
        Expression<Func<T, object>>? vector = null,
        float? certainty = null,
        float? distance = null
    )
    {
        _inner.NearObject(objectId, vector, certainty, distance);
        return this;
    }

    /// <summary>
    /// Performs a hybrid search combining BM25 keyword search with vector search.
    /// At least one of query or vector must be provided.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> Hybrid(
        string? query,
        Expression<Func<T, object>>? vector = null,
        float? alpha = null,
        HybridFusion? fusionType = null,
        float? maxVectorDistance = null
    )
    {
        _inner.Hybrid(query, vector, alpha, fusionType, maxVectorDistance);
        return this;
    }

    /// <summary>
    /// Performs a BM25 keyword search.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> BM25(
        string query,
        Expression<Func<T, object>>[]? searchFields = null,
        BM25Operator? searchOperator = null
    )
    {
        _inner.BM25(query, searchFields, searchOperator);
        return this;
    }

    /// <summary>
    /// Performs a multi-modal near media search (image, video, audio, etc.).
    /// </summary>
    public ProjectedQueryClient<T, TProjection> NearMedia(NearMediaInput.FactoryFn media)
    {
        _inner.NearMedia(media);
        return this;
    }

    #endregion

    #region Result Control Methods

    /// <summary>
    /// Limits the number of results returned.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> Limit(uint limit)
    {
        _inner.Limit(limit);
        return this;
    }

    /// <summary>
    /// Skips the first N results (pagination offset).
    /// </summary>
    public ProjectedQueryClient<T, TProjection> Offset(uint offset)
    {
        _inner.Offset(offset);
        return this;
    }

    /// <summary>
    /// Sets the autocut limit for automatic result limiting.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> AutoLimit(uint autoLimit)
    {
        _inner.AutoLimit(autoLimit);
        return this;
    }

    /// <summary>
    /// Sets the cursor for cursor-based pagination.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> After(Guid cursor)
    {
        _inner.After(cursor);
        return this;
    }

    /// <summary>
    /// Applies a reranker to the search results.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> Rerank(Rerank rerank)
    {
        _inner.Rerank(rerank);
        return this;
    }

    /// <summary>
    /// Applies a reranker to the search results using the specified property and optional query.
    /// </summary>
    /// <param name="property">The property to rerank on.</param>
    /// <param name="query">Optional query string to rerank against.</param>
    public ProjectedQueryClient<T, TProjection> Rerank(string property, string? query = null) =>
        Rerank(new Rerank { Property = property, Query = query });

    /// <summary>
    /// Sorts results by a property on the entity type, replacing any previous sort criteria.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> Sort<TProp>(
        Expression<Func<T, TProp>> property,
        bool descending = false
    )
    {
        _inner.Sort(property, descending);
        return this;
    }

    /// <summary>
    /// Sets the primary sort criterion (ascending), replacing any previous sort criteria.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> OrderBy<TProp>(Expression<Func<T, TProp>> property)
    {
        _inner.OrderBy(property);
        return this;
    }

    /// <summary>
    /// Sets the primary sort criterion (descending), replacing any previous sort criteria.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> OrderByDescending<TProp>(
        Expression<Func<T, TProp>> property
    )
    {
        _inner.OrderByDescending(property);
        return this;
    }

    /// <summary>
    /// Appends an ascending secondary sort criterion.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> ThenBy<TProp>(Expression<Func<T, TProp>> property)
    {
        _inner.ThenBy(property);
        return this;
    }

    /// <summary>
    /// Appends a descending secondary sort criterion.
    /// </summary>
    public ProjectedQueryClient<T, TProjection> ThenByDescending<TProp>(
        Expression<Func<T, TProp>> property
    )
    {
        _inner.ThenByDescending(property);
        return this;
    }

    #endregion

    #region Execution

    /// <summary>
    /// Executes the query and returns projected results.
    /// Each result contains the projected entity, its ID, and query metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Enumerable of QueryResult containing the projected entity.</returns>
    public async Task<IEnumerable<QueryResult<TProjection>>> Execute(
        CancellationToken cancellationToken = default
    )
    {
        // Apply any projection-defined search configuration overrides
        _inner.ApplyNearTextConfig(_nearTextConfigure);
        _inner.ApplyNearVectorConfig(_nearVectorConfigure);
        _inner.ApplyHybridConfig(_hybridConfigure);
        _inner.ApplyNearObjectConfig(_nearObjectConfigure);
        _inner.ApplyNearMediaConfig(_nearMediaConfigure);

        var result = await _inner.ExecuteQueryAsync(cancellationToken);

        return result.Objects.Select(wo => new QueryResult<TProjection>
        {
            UUID = wo.UUID,
            Object = ProjectionMapper.MapToProjection<T, TProjection>(wo),
            Metadata = wo.Metadata,
        });
    }

    /// <summary>
    /// Makes the query directly awaitable without calling <c>Execute()</c>.
    /// This is syntactic sugar that allows <c>await collection.Query&lt;TProjection&gt;()...</c>
    /// instead of <c>await collection.Query&lt;TProjection&gt;()...Execute()</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// // These are equivalent:
    /// var results = await collection.Query&lt;ArticleSummary&gt;().Limit(10).Execute();
    /// var results = await collection.Query&lt;ArticleSummary&gt;().Limit(10);
    /// </code>
    /// </example>
    public TaskAwaiter<IEnumerable<QueryResult<TProjection>>> GetAwaiter() =>
        Execute(CancellationToken.None).GetAwaiter();

    #endregion
}
