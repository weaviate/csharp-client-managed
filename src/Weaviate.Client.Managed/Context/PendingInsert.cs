using System.Runtime.CompilerServices;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// A pending batch insert operation that can accumulate multiple batches and execute them
/// sequentially. Created by <see cref="CollectionSet{T}.Insert(T[])"/>.
/// </summary>
/// <remarks>
/// Chain multiple <see cref="Insert"/> calls to execute ordered InsertMany operations,
/// which is useful when inserting objects that reference other objects in the same collection:
/// the referenced objects must be inserted and indexed before the referencing objects.
/// </remarks>
/// <example>
/// <code>
/// // Single batch
/// var books = await context.Books.Insert(b1, b2, b3).Execute(ct);
///
/// // Two batches — b4 can reference b1/b2/b3 as same-collection cross-references
/// var allBooks = await context.Books
///     .Insert(b1, b2, b3)
///     .Insert(b4)
///     .Execute(ct);
/// </code>
/// </example>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class PendingInsert<T>
    where T : class, new()
{
    private readonly CollectionSet<T> _set;
    private readonly List<T[]> _batches;

    internal PendingInsert(CollectionSet<T> set, T[] initial)
    {
        _set = set;
        _batches = [initial];
    }

    /// <summary>
    /// Adds another batch of entities to be inserted after the previous batches.
    /// Each batch maps to a single InsertMany call, executed in order.
    /// </summary>
    /// <param name="entities">The entities to insert in this batch.</param>
    /// <returns>This instance for chaining.</returns>
    public PendingInsert<T> Insert(params T[] entities)
    {
        if (entities.Length > 0)
            _batches.Add(entities);
        return this;
    }

    /// <summary>
    /// Executes all accumulated batches in order, returning all inserted entities with
    /// their [WeaviateUUID] properties populated.
    /// Throws <see cref="Models.BatchInsertException{T}"/> if any batch has failures.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All successfully inserted entities.</returns>
    public async Task<T[]> Execute(CancellationToken cancellationToken = default)
    {
        var allInserted = new List<T>();

        foreach (var batch in _batches)
        {
            var inserted = await _set.ExecuteBatch(batch, cancellationToken).ConfigureAwait(false);
            allInserted.AddRange(inserted);
        }

        return allInserted.ToArray();
    }

    /// <summary>
    /// Enables direct <c>await</c> without an explicit <see cref="Execute"/> call,
    /// using the default <see cref="CancellationToken"/>.
    /// </summary>
    public TaskAwaiter<T[]> GetAwaiter() => Execute().GetAwaiter();
}
