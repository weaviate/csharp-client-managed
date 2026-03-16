using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Extensions;
using Weaviate.Client.Managed.Models;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Query;

internal enum PendingOpKind
{
    Where,
    OrderBy,
    OrderByDesc,
    ThenBy,
    ThenByDesc,
    Take,
    Skip,
}

internal sealed record PendingOp(PendingOpKind Kind, object Arg);

/// <summary>
/// An <see cref="IQueryable{T}"/> implementation for Weaviate collections that enables
/// standard LINQ query syntax and Weaviate-specific extension methods.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <example>
/// <code>
/// // LINQ query syntax
/// var results = await (
///     from p in context.Products
///     where p.Price > 100
///     orderby p.Name
///     select p
/// );
///
/// // With vector search as LINQ source
/// var results = await (
///     from p in context.Products.NearText("wireless mouse")
///     where p.InStock
///     select p
/// );
/// </code>
/// </example>
public sealed class WeaviateQueryable<T> : IQueryable<T>, IOrderedQueryable<T>
    where T : class, new()
{
    private readonly Func<CollectionMapperQueryClient<T>> _factory;
    private readonly IQueryProvider _provider;
    private readonly CancellationToken _cancellationToken;

    internal WeaviateQueryConfig Config { get; }
    internal IReadOnlyList<PendingOp> Ops { get; }

    internal WeaviateQueryable(
        WeaviateQueryConfig config,
        IReadOnlyList<PendingOp> ops,
        Func<CollectionMapperQueryClient<T>> factory,
        IQueryProvider provider,
        CancellationToken cancellationToken = default
    )
    {
        Config = config;
        Ops = ops;
        _factory = factory;
        _provider = provider;
        _cancellationToken = cancellationToken;
    }

    #region IQueryable<T>

    /// <inheritdoc />
    public Expression Expression => Expression.Constant(this);

    /// <inheritdoc />
    public Type ElementType => typeof(T);

    /// <inheritdoc />
    public IQueryProvider Provider => _provider;

    /// <summary>Throws — use <c>await</c> to execute the query.</summary>
    public IEnumerator<T> GetEnumerator() =>
        throw new InvalidOperationException(
            "WeaviateQueryable<T> cannot be enumerated synchronously. Use 'await' to execute the query."
        );

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region Awaitable

    /// <summary>
    /// Executes the query and returns an awaitable that yields <see cref="IEnumerable{T}"/>.
    /// Uses the cancellation token supplied via <c>WithCancellation()</c>, or <see cref="CancellationToken.None"/>.
    /// </summary>
    public TaskAwaiter<IEnumerable<T>> GetAwaiter() =>
        ExecuteAsync(_cancellationToken).GetAwaiter();

    #endregion

    #region Internal builders

    internal WeaviateQueryable<T> WithOp(PendingOp op) =>
        new(Config, [.. Ops, op], _factory, _provider, _cancellationToken);

    internal WeaviateQueryable<T> WithConfig(WeaviateQueryConfig config) =>
        new(config, Ops, _factory, _provider, _cancellationToken);

    internal WeaviateQueryable<T> WithCancellationToken(CancellationToken cancellationToken) =>
        new(Config, Ops, _factory, _provider, cancellationToken);

    #endregion

    #region Execution

    internal async Task<IEnumerable<T>> ExecuteAsync(CancellationToken ct)
    {
        var results = await ExecuteQueryResultsAsync(ct).ConfigureAwait(false);
        return results.Objects();
    }

    internal async Task<IEnumerable<QueryResult<T>>> ExecuteQueryResultsAsync(CancellationToken ct)
    {
        var client = _factory();
        ApplyOps(client);
        ApplyConfig(client);
        return await client.Execute(ct).ConfigureAwait(false);
    }

    private void ApplyOps(CollectionMapperQueryClient<T> client)
    {
        foreach (var op in Ops)
        {
            switch (op.Kind)
            {
                case PendingOpKind.Where:
                    client.Where((Expression<Func<T, bool>>)op.Arg);
                    break;

                case PendingOpKind.OrderBy:
                    InvokeGenericSortMethod(client, "OrderBy", (LambdaExpression)op.Arg);
                    break;

                case PendingOpKind.OrderByDesc:
                    InvokeGenericSortMethod(client, "OrderByDescending", (LambdaExpression)op.Arg);
                    break;

                case PendingOpKind.ThenBy:
                    InvokeGenericSortMethod(client, "ThenBy", (LambdaExpression)op.Arg);
                    break;

                case PendingOpKind.ThenByDesc:
                    InvokeGenericSortMethod(client, "ThenByDescending", (LambdaExpression)op.Arg);
                    break;

                case PendingOpKind.Take:
                    client.Limit((uint)(int)op.Arg);
                    break;

                case PendingOpKind.Skip:
                    client.Offset((uint)(int)op.Arg);
                    break;
            }
        }
    }

    private static void InvokeGenericSortMethod(
        CollectionMapperQueryClient<T> client,
        string methodName,
        LambdaExpression keySelector
    )
    {
        var method = typeof(CollectionMapperQueryClient<T>)
            .GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                [
                    typeof(Expression<>).MakeGenericType(
                        typeof(Func<,>).MakeGenericType(typeof(T), keySelector.ReturnType)
                    ),
                ],
                null
            )!
            .MakeGenericMethod(keySelector.ReturnType);

        method.Invoke(client, [keySelector]);
    }

    private void ApplyConfig(CollectionMapperQueryClient<T> client)
    {
        switch (Config.SearchMode)
        {
            case WeaviateSearchMode.NearText:
                client.NearText(
                    Config.NearTextQuery!,
                    certainty: Config.Certainty,
                    distance: Config.Distance
                );
                break;

            case WeaviateSearchMode.NearVector when Config.NearVector != null:
                client.NearVector(
                    Config.NearVector,
                    certainty: Config.Certainty,
                    distance: Config.Distance
                );
                break;

            case WeaviateSearchMode.NearVector:
                client.NearVector(certainty: Config.Certainty, distance: Config.Distance);
                break;

            case WeaviateSearchMode.NearObject:
                client.NearObject(
                    Config.NearObjectId!.Value,
                    certainty: Config.Certainty,
                    distance: Config.Distance
                );
                break;

            case WeaviateSearchMode.Hybrid:
                client.Hybrid(
                    Config.HybridQuery!,
                    alpha: Config.HybridAlpha,
                    fusionType: Config.FusionType,
                    maxVectorDistance: Config.MaxVectorDistance
                );
                break;

            case WeaviateSearchMode.BM25:
                client.BM25(Config.Bm25Query!, searchOperator: Config.Bm25Operator);
                break;

            case WeaviateSearchMode.NearMedia:
                client.NearMedia(Config.NearMedia!);
                break;
        }

        if (Config.IncludeVectors.Length > 0)
            client.AddIncludeVectors(Config.IncludeVectors);

        if (Config.IncludeReferences.Length > 0)
            client.AddIncludeReferences(Config.IncludeReferences);

        if (Config.Metadata != null)
            client.WithMetadata(Config.Metadata);

        if (Config.Rerank != null)
            client.Rerank(Config.Rerank);
    }

    #endregion
}
