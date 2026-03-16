namespace Weaviate.Client.Managed.Context;

/// <summary>
/// Base record for batch operations.
/// </summary>
/// <param name="EntityType">The entity type this operation applies to.</param>
internal abstract record BatchOperation(Type EntityType);

/// <summary>
/// Represents a batch insert operation.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <param name="Entities">The entities to insert.</param>
internal record InsertOperation<T>(T[] Entities) : BatchOperation(typeof(T))
    where T : class, new();

/// <summary>
/// Represents a batch update operation.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <param name="Entities">The entities to update.</param>
internal record UpdateOperation<T>(T[] Entities) : BatchOperation(typeof(T))
    where T : class, new();

/// <summary>
/// Represents a batch delete operation.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <param name="Ids">The IDs of entities to delete.</param>
internal record DeleteOperation<T>(Guid[] Ids) : BatchOperation(typeof(T))
    where T : class, new();
