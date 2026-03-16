using Weaviate.Client;

namespace Weaviate.Client.Managed.Models;

/// <summary>
/// An entity that failed to insert during a batch operation, with the associated error.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class BatchInsertError<T>
{
    /// <summary>The entity that failed to insert.</summary>
    public T Object { get; }

    /// <summary>The error returned by Weaviate.</summary>
    public WeaviateException Error { get; }

    internal BatchInsertError(T obj, WeaviateException error)
    {
        Object = obj;
        Error = error;
    }
}

/// <summary>
/// Thrown when one or more entities fail to insert during a batch insert operation.
/// Successfully inserted entities (with IDs assigned) are available via
/// <see cref="Inserted"/>.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class BatchInsertException<T> : Exception
{
    /// <summary>
    /// Entities that were successfully inserted before or alongside the failures.
    /// Their [WeaviateUUID] properties are populated.
    /// </summary>
    public IReadOnlyList<T> Inserted { get; }

    /// <summary>
    /// Entities that failed to insert, each paired with the error from Weaviate.
    /// </summary>
    public IReadOnlyList<BatchInsertError<T>> Errors { get; }

    internal BatchInsertException(List<T> inserted, List<BatchInsertError<T>> errors)
        : base(BuildMessage(errors))
    {
        Inserted = inserted;
        Errors = errors;
    }

    /// <inheritdoc/>
    public BatchInsertException()
        : base("Batch insert failed.")
    {
        Inserted = [];
        Errors = [];
    }

    /// <inheritdoc/>
    public BatchInsertException(string message)
        : base(message)
    {
        Inserted = [];
        Errors = [];
    }

    /// <inheritdoc/>
    public BatchInsertException(string message, Exception inner)
        : base(message, inner)
    {
        Inserted = [];
        Errors = [];
    }

    private static string BuildMessage(List<BatchInsertError<T>> errors)
    {
        var first = errors[0].Error.Message;
        return errors.Count == 1
            ? $"Batch insert failed for 1 entity: {first}"
            : $"Batch insert failed for {errors.Count} entities. First error: {first}";
    }
}
