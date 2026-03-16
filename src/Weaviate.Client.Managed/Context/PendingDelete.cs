using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Internal;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// A pending batch delete operation that accumulates entity IDs and executes them
/// in a single batch. Created by <see cref="CollectionSet{T}.Delete(T[])"/> and
/// <see cref="CollectionSet{T}.Delete(Guid[])"/>.
/// </summary>
/// <remarks>
/// Chain multiple <see cref="Delete(T[])"/> or <see cref="Delete(Guid[])"/> calls to accumulate
/// IDs, then call <c>.Execute(ct)</c> or directly <c>await</c> the instance.
/// All accumulated IDs are sent to Weaviate in a single batch delete using
/// <c>Filter.UUID.ContainsAny</c>.
/// </remarks>
/// <example>
/// <code>
/// // Simple batch — directly awaitable
/// await context.Products.Delete(p1, p2, p3);
///
/// // Chained accumulation — all deleted in one batch
/// await context.Products
///     .Delete(p1, p2)
///     .Delete(p3, p4)
///     .Execute(cancellationToken);
///
/// // Mix entities and raw IDs
/// await context.Products
///     .Delete(p1, p2)
///     .Delete(oldId1, oldId2)
///     .Execute(cancellationToken);
/// </code>
/// </example>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class PendingDelete<T>
    where T : class, new()
{
    private readonly CollectionSet<T> _set;
    private readonly List<Guid> _ids = new();

    internal PendingDelete(CollectionSet<T> set, Guid[] ids)
    {
        _set = set;
        _ids.AddRange(ids);
    }

    /// <summary>
    /// Adds more entities to delete. IDs are extracted from each entity's
    /// <c>[WeaviateUUID]</c> property.
    /// </summary>
    /// <param name="entities">The entities to delete.</param>
    /// <returns>This instance for chaining.</returns>
    public PendingDelete<T> Delete(params T[] entities)
    {
        foreach (var e in entities)
            _ids.Add(IdPropertyHelper.GetId(e));
        return this;
    }

    /// <summary>
    /// Adds more IDs to delete.
    /// </summary>
    /// <param name="ids">The IDs to delete.</param>
    /// <returns>This instance for chaining.</returns>
    public PendingDelete<T> Delete(params Guid[] ids)
    {
        _ids.AddRange(ids);
        return this;
    }

    /// <summary>
    /// Executes the batch delete for all accumulated IDs in a single server call.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task Execute(CancellationToken cancellationToken = default) =>
        _set.ExecuteDeleteBatch(_ids.ToArray(), cancellationToken);

    /// <summary>
    /// Enables direct <c>await</c> without an explicit <see cref="Execute"/> call,
    /// using the default <see cref="CancellationToken"/>.
    /// </summary>
    public TaskAwaiter GetAwaiter() => Execute().GetAwaiter();
}
