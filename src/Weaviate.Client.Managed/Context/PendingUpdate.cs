using System.Runtime.CompilerServices;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// A pending batch update operation that accumulates entities and executes them
/// sequentially. Created by <see cref="CollectionSet{T}.Update(T[])"/>.
/// </summary>
/// <remarks>
/// Chain multiple <see cref="Update"/> calls to accumulate entities, then call
/// <c>.Execute(ct)</c> or directly <c>await</c> the instance.
/// Note: Weaviate has no batch-update endpoint, so each entity is updated individually.
/// </remarks>
/// <example>
/// <code>
/// // Simple batch — directly awaitable
/// await context.Products.Update(p1, p2, p3);
///
/// // Chained accumulation
/// var updated = await context.Products
///     .Update(p1, p2)
///     .Update(p3, p4)
///     .Execute(cancellationToken);
/// </code>
/// </example>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class PendingUpdate<T>
    where T : class, new()
{
    private readonly CollectionSet<T> _set;
    private readonly List<T> _entities = new();

    internal PendingUpdate(CollectionSet<T> set, T[] initial)
    {
        _set = set;
        _entities.AddRange(initial);
    }

    /// <summary>
    /// Adds more entities to update.
    /// </summary>
    /// <param name="entities">The entities to update (PATCH semantics).</param>
    /// <returns>This instance for chaining.</returns>
    public PendingUpdate<T> Update(params T[] entities)
    {
        _entities.AddRange(entities);
        return this;
    }

    /// <summary>
    /// Executes PATCH updates for all accumulated entities sequentially.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All updated entities.</returns>
    public async Task<T[]> Execute(CancellationToken cancellationToken = default)
    {
        foreach (var entity in _entities)
            await _set.Update(entity, cancellationToken).ConfigureAwait(false);
        return _entities.ToArray();
    }

    /// <summary>
    /// Enables direct <c>await</c> without an explicit <see cref="Execute"/> call,
    /// using the default <see cref="CancellationToken"/>.
    /// </summary>
    public TaskAwaiter<T[]> GetAwaiter() => Execute().GetAwaiter();
}
