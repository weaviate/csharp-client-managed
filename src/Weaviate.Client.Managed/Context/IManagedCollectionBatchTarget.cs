using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// Internal interface for batch operations on <see cref="ManagedCollection{T}"/>.
/// Used by <see cref="PendingBatch"/> to avoid runtime reflection.
/// </summary>
internal interface IManagedCollectionBatchTarget
{
    /// <summary>
    /// Inserts multiple entities in batch, including their references.
    /// </summary>
    /// <param name="entities">The entities to insert (type-erased as object).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch insert response with UUIDs.</returns>
    Task<BatchInsertResponse> InsertMany(
        IEnumerable<object> entities,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Inserts multiple entities without including their reference properties.
    /// Used during two-phase batch inserts (insert objects, then add references).
    /// </summary>
    /// <param name="entities">The entities to insert (type-erased as object).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InsertManyNoRefs(IEnumerable<object> entities, CancellationToken cancellationToken);

    /// <summary>
    /// Collects all cross-reference operations from the given entities.
    /// </summary>
    /// <param name="entities">The entities to extract references from (type-erased as object).</param>
    /// <returns>Array of reference operations.</returns>
    DataReference[] CollectReferences(IEnumerable<object> entities);

    /// <summary>
    /// Adds cross-references in batch.
    /// </summary>
    /// <param name="refs">The reference operations to perform.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddReferences(DataReference[] refs, CancellationToken cancellationToken);
}
