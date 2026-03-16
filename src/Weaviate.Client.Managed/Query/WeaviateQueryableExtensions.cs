using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Context;
using Weaviate.Client.Managed.Models;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Query;

/// <summary>
/// Extension methods that add Weaviate-specific search operations to <see cref="IQueryable{T}"/>
/// sources, enabling LINQ query syntax with vector search.
/// </summary>
public static class WeaviateQueryableExtensions
{
    // ──────────────────────────────────────────────────────────────────────────
    // Internal helper
    // ──────────────────────────────────────────────────────────────────────────

    private static WeaviateQueryable<T> AsWeaviate<T>(IQueryable<T> source)
        where T : class, new()
    {
        if (source is WeaviateQueryable<T> wq)
            return wq;
        if (source is CollectionSet<T> cs)
            return cs.ToWeaviateQueryable();
        throw new InvalidOperationException(
            $"Weaviate extension methods can only be called on a WeaviateQueryable<{typeof(T).Name}> or CollectionSet<{typeof(T).Name}>."
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Search modes
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Performs a near-text (semantic) search. Can be used as a LINQ source:
    /// <code>from p in context.Products.NearText("mouse") where p.InStock select p</code>
    /// </summary>
    public static WeaviateQueryable<T> NearText<T>(
        this IQueryable<T> source,
        string text,
        Expression<Func<T, object>>? vector = null,
        float? certainty = null,
        float? distance = null
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        return wq.WithConfig(
            wq.Config with
            {
                SearchMode = WeaviateSearchMode.NearText,
                NearTextQuery = text,
                Certainty = certainty,
                Distance = distance,
            }
        );
    }

    /// <summary>
    /// Performs a near-vector search using an explicit vector.
    /// </summary>
    public static WeaviateQueryable<T> NearVector<T>(
        this IQueryable<T> source,
        float[] vectorValues,
        Expression<Func<T, object>>? vector = null,
        float? certainty = null,
        float? distance = null
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        return wq.WithConfig(
            wq.Config with
            {
                SearchMode = WeaviateSearchMode.NearVector,
                NearVector = vectorValues,
                Certainty = certainty,
                Distance = distance,
            }
        );
    }

    /// <summary>
    /// Performs a near-vector search without explicit vector data (for per-target combination via VectorTargets()).
    /// </summary>
    public static WeaviateQueryable<T> NearVector<T>(
        this IQueryable<T> source,
        float? certainty = null,
        float? distance = null
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        return wq.WithConfig(
            wq.Config with
            {
                SearchMode = WeaviateSearchMode.NearVector,
                NearVector = null,
                Certainty = certainty,
                Distance = distance,
            }
        );
    }

    /// <summary>
    /// Performs a near-object search using an existing object's vector.
    /// </summary>
    public static WeaviateQueryable<T> NearObject<T>(
        this IQueryable<T> source,
        Guid objectId,
        Expression<Func<T, object>>? vector = null,
        float? certainty = null,
        float? distance = null
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        return wq.WithConfig(
            wq.Config with
            {
                SearchMode = WeaviateSearchMode.NearObject,
                NearObjectId = objectId,
                Certainty = certainty,
                Distance = distance,
            }
        );
    }

    /// <summary>
    /// Performs a hybrid search combining BM25 keyword search with vector search.
    /// </summary>
    public static WeaviateQueryable<T> Hybrid<T>(
        this IQueryable<T> source,
        string query,
        Expression<Func<T, object>>? vector = null,
        float? alpha = null,
        HybridFusion? fusionType = null,
        float? maxVectorDistance = null
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        return wq.WithConfig(
            wq.Config with
            {
                SearchMode = WeaviateSearchMode.Hybrid,
                HybridQuery = query,
                HybridAlpha = alpha,
                FusionType = fusionType,
                MaxVectorDistance = maxVectorDistance,
            }
        );
    }

    /// <summary>
    /// Performs a BM25 keyword search.
    /// </summary>
    public static WeaviateQueryable<T> BM25<T>(
        this IQueryable<T> source,
        string query,
        Expression<Func<T, object>>[]? searchFields = null,
        BM25Operator? searchOperator = null
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        var fieldNames = searchFields
            ?.Select(f => PropertyHelper.ToCamelCase(PropertyHelper.GetPropertyName(f)))
            .ToArray();
        return wq.WithConfig(
            wq.Config with
            {
                SearchMode = WeaviateSearchMode.BM25,
                Bm25Query = query,
                Bm25SearchFields = fieldNames,
                Bm25Operator = searchOperator,
            }
        );
    }

    /// <summary>
    /// Performs a multi-modal near-media search (image, video, audio, etc.).
    /// </summary>
    public static WeaviateQueryable<T> NearMedia<T>(
        this IQueryable<T> source,
        NearMediaInput.FactoryFn media
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        return wq.WithConfig(
            wq.Config with
            {
                SearchMode = WeaviateSearchMode.NearMedia,
                NearMedia = media,
            }
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cancellation
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Embeds a <see cref="CancellationToken"/> into the query so that <c>await</c> on LINQ
    /// query expressions respects cancellation:
    /// <code>
    /// var results = await (
    ///     from p in context.Products.WithCancellation(ct)
    ///     where p.Price > 100
    ///     select p
    /// );
    /// </code>
    /// </summary>
    public static WeaviateQueryable<T> WithCancellation<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken
    )
        where T : class, new() => AsWeaviate(source).WithCancellationToken(cancellationToken);

    // ──────────────────────────────────────────────────────────────────────────
    // Result control
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Includes named vectors in the results.
    /// </summary>
    public static WeaviateQueryable<T> WithVectors<T>(
        this IQueryable<T> source,
        params Expression<Func<T, object>>[] vectors
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        var names = vectors
            .Select(v => PropertyHelper.ToCamelCase(PropertyHelper.GetPropertyName(v)))
            .ToArray();
        var merged = wq.Config.IncludeVectors.Union(names).ToArray();
        return wq.WithConfig(wq.Config with { IncludeVectors = merged });
    }

    /// <summary>
    /// Includes cross-references in the results (expanded and populated).
    /// </summary>
    public static WeaviateQueryable<T> WithReferences<T>(
        this IQueryable<T> source,
        params Expression<Func<T, object>>[] references
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        var names = references
            .Select(r => PropertyHelper.ToCamelCase(PropertyHelper.GetPropertyName(r)))
            .ToArray();
        var merged = wq.Config.IncludeReferences.Union(names).ToArray();
        return wq.WithConfig(wq.Config with { IncludeReferences = merged });
    }

    /// <summary>
    /// Includes query metadata (distance, certainty, score, etc.) in the results.
    /// Use <see cref="ToQueryResultsAsync{T}"/> to access the metadata.
    /// </summary>
    public static WeaviateQueryable<T> WithMetadata<T>(
        this IQueryable<T> source,
        MetadataQuery metadata
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        return wq.WithConfig(wq.Config with { Metadata = metadata });
    }

    /// <summary>
    /// Applies reranking to the results.
    /// </summary>
    public static WeaviateQueryable<T> Rerank<T>(
        this IQueryable<T> source,
        string property,
        string? query = null
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        var rerank = new Rerank { Property = property, Query = query };
        return wq.WithConfig(wq.Config with { Rerank = rerank });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Await support
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables <c>await</c> on any Weaviate <see cref="IQueryable{T}"/>, including the result of
    /// LINQ query expressions:
    /// <code>
    /// var results = await (from p in context.Products where p.Price > 100 select p);
    /// var results = await context.Products.NearText("mouse").Where(p => p.InStock);
    /// </code>
    /// </summary>
    public static TaskAwaiter<IEnumerable<T>> GetAwaiter<T>(this IQueryable<T> source)
        where T : class, new() =>
        AsWeaviate(source).ExecuteAsync(CancellationToken.None).GetAwaiter();

    // ──────────────────────────────────────────────────────────────────────────
    // Terminal methods
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Executes the query and returns <see cref="QueryResult{T}"/> wrappers that include
    /// UUID and optional metadata (score, distance, etc.).
    /// </summary>
    public static Task<IEnumerable<QueryResult<T>>> ToQueryResultsAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source);
        return wq.ExecuteQueryResultsAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the first element of the query, or <c>null</c> if no results are found.
    /// Applies <c>Take(1)</c> automatically.
    /// </summary>
    public static async Task<T?> FirstOrDefaultAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        var wq = AsWeaviate(source).WithOp(new PendingOp(PendingOpKind.Take, 1));
        var results = await wq.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// Returns the first element of the query.
    /// Throws <see cref="InvalidOperationException"/> if no results are found.
    /// Applies <c>Take(1)</c> automatically.
    /// </summary>
    public static async Task<T> FirstAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        var result = await FirstOrDefaultAsync(source, cancellationToken).ConfigureAwait(false);
        return result
            ?? throw new InvalidOperationException(
                $"Sequence contains no elements of type {typeof(T).Name}."
            );
    }
}
