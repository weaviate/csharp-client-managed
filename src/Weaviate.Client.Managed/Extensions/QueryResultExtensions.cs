using Weaviate.Client.Managed.Models;

namespace Weaviate.Client.Managed.Extensions;

/// <summary>
/// Extension methods for working with <see cref="QueryResult{T}"/> collections.
/// </summary>
public static class QueryResultExtensions
{
    /// <summary>
    /// Extracts just the entity objects from query results, discarding metadata.
    /// Use this when you don't need access to query metadata (Score, Distance, etc.).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="results">The query results.</param>
    /// <returns>Enumerable of just the entity objects.</returns>
    /// <example>
    /// <code>
    /// var objects = (await collection.Query().Execute()).Objects();
    ///
    /// foreach (var article in objects)
    /// {
    ///     Console.WriteLine(article.Title);
    /// }
    /// </code>
    /// </example>
    public static IEnumerable<T> Objects<T>(this IEnumerable<QueryResult<T>> results) =>
        results.Select(r => r.Object);

    /// <summary>
    /// Filters results to only those with metadata.
    /// Useful when using optional metadata fields.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="results">The query results.</param>
    /// <returns>Query results that have non-null metadata.</returns>
    public static IEnumerable<QueryResult<T>> WithMetadata<T>(
        this IEnumerable<QueryResult<T>> results
    ) => results.Where(r => r.Metadata != null);

    /// <summary>
    /// Orders results by score (descending, highest first).
    /// Only includes results that have a score value.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="results">The query results.</param>
    /// <returns>Results ordered by score, descending.</returns>
    public static IEnumerable<QueryResult<T>> OrderByScore<T>(
        this IEnumerable<QueryResult<T>> results
    ) => results.Where(r => r.Metadata?.Score != null).OrderByDescending(r => r.Metadata!.Score);

    /// <summary>
    /// Orders results by distance (ascending, closest first).
    /// Only includes results that have a distance value.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="results">The query results.</param>
    /// <returns>Results ordered by distance, ascending.</returns>
    public static IEnumerable<QueryResult<T>> OrderByDistance<T>(
        this IEnumerable<QueryResult<T>> results
    ) => results.Where(r => r.Metadata?.Distance != null).OrderBy(r => r.Metadata!.Distance);
}
