using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Internal;
using Weaviate.Client.Models;
using ManagedPropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// A pending batch reference operation that accumulates cross-reference links
/// and executes them in a single <c>ReferenceAddMany</c> call.
/// Created by <see cref="CollectionSet{T}.AddReference{TTarget}(T, Expression{Func{T, object?}}, TTarget)"/>
/// and related overloads.
/// </summary>
/// <remarks>
/// Chain multiple <see cref="AddReference{TTarget}(T, Expression{Func{T, object?}}, TTarget)"/>
/// or <see cref="AddReference(T, Expression{Func{T, object?}}, Guid)"/> calls to accumulate
/// reference links, then call <c>.Execute(ct)</c> or directly <c>await</c> the instance.
/// All links are sent to Weaviate in a single batch via the batch references API.
/// </remarks>
/// <example>
/// <code>
/// // Add a single reference — directly awaitable
/// await context.Articles.AddReference(article, a => a.Category, category);
///
/// // Chained — all links sent in one ReferenceAddMany call
/// await context.Articles
///     .AddReference(article1, a => a.Category, techCategory)
///     .AddReference(article2, a => a.Category, scienceCategory)
///     .Execute(cancellationToken);
///
/// // Mix entity and ID targets
/// await context.Articles
///     .AddReference(article, a => a.Category, category)
///     .AddReference(article, a => a.AuthorId, existingAuthorId)
///     .Execute(cancellationToken);
/// </code>
/// </example>
/// <typeparam name="T">The source entity type.</typeparam>
public sealed class PendingReference<T>
    where T : class, new()
{
    private readonly CollectionSet<T> _set;
    private readonly List<DataReference> _refs = new();

    internal PendingReference(CollectionSet<T> set, DataReference initial)
    {
        _set = set;
        _refs.Add(initial);
    }

    /// <summary>
    /// Adds another reference link from <paramref name="from"/> to an entity target.
    /// </summary>
    /// <typeparam name="TTarget">The target entity type.</typeparam>
    /// <param name="from">The source entity (must have a populated <c>[WeaviateUUID]</c> property).</param>
    /// <param name="property">Expression selecting the reference property (e.g., <c>a => a.Category</c>).</param>
    /// <param name="to">The target entity (must have a populated <c>[WeaviateUUID]</c> property).</param>
    /// <returns>This instance for chaining.</returns>
    public PendingReference<T> AddReference<TTarget>(
        T from,
        Expression<Func<T, object?>> property,
        TTarget to
    )
        where TTarget : class, new()
    {
        _refs.Add(BuildRef(from, property, IdPropertyHelper.GetId(to)));
        return this;
    }

    /// <summary>
    /// Adds another reference link from <paramref name="from"/> to a target specified by ID.
    /// </summary>
    /// <param name="from">The source entity (must have a populated <c>[WeaviateUUID]</c> property).</param>
    /// <param name="property">Expression selecting the reference property (e.g., <c>a => a.AuthorId</c>).</param>
    /// <param name="toId">The UUID of the target object.</param>
    /// <returns>This instance for chaining.</returns>
    public PendingReference<T> AddReference(
        T from,
        Expression<Func<T, object?>> property,
        Guid toId
    )
    {
        _refs.Add(BuildRef(from, property, toId));
        return this;
    }

    /// <summary>
    /// Executes all accumulated reference additions in a single <c>ReferenceAddMany</c> call.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task Execute(CancellationToken cancellationToken = default) =>
        _set.ExecuteAddReferences(_refs.ToArray(), cancellationToken);

    /// <summary>
    /// Enables direct <c>await</c> without an explicit <see cref="Execute"/> call,
    /// using the default <see cref="CancellationToken"/>.
    /// </summary>
    public TaskAwaiter GetAwaiter() => Execute().GetAwaiter();

    private static DataReference BuildRef(T from, Expression<Func<T, object?>> property, Guid toId)
    {
        var camelName = ManagedPropertyHelper.ToCamelCase(
            ManagedPropertyHelper.GetPropertyName<T, object?>(property)
        );
        return new DataReference(IdPropertyHelper.GetId(from), camelName, [toId]);
    }
}
