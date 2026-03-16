using System.Collections;
using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Models;

/// <summary>
/// Wraps a generative query result with per-object generative AI data.
/// Extends <see cref="QueryResult{T}"/> with the generative reply for each object.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public record GenerativeQueryResult<T> : QueryResult<T>
{
    /// <summary>
    /// The generative AI result for this object (from SinglePrompt).
    /// Contains AI-generated text specific to this search result.
    /// </summary>
    public GenerativeResult? Generative { get; init; }
}

/// <summary>
/// Wraps the full generative query response, including both per-object and result-set level generative data.
/// Implements <see cref="IEnumerable{T}"/> over the individual results for convenient enumeration.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <example>
/// <code>
/// var response = await products.Query()
///     .NearText("wireless mouse")
///     .Generate(singlePrompt: "Describe this product")
///     .Execute();
///
/// // Enumerate per-object results
/// foreach (var r in response)
///     Console.WriteLine($"{r.Object.Name}: {r.Generative?[0]}");
///
/// // Access grouped task result
/// Console.WriteLine(response.Generative?[0]);
/// </code>
/// </example>
public record GenerativeQueryResponse<T> : IEnumerable<GenerativeQueryResult<T>>
{
    /// <summary>
    /// The individual results, each containing the mapped entity and its per-object generative data.
    /// </summary>
    public required IList<GenerativeQueryResult<T>> Results { get; init; }

    /// <summary>
    /// The result-set level generative AI result (from GroupedTask).
    /// Contains AI-generated text summarizing or operating on the entire result set.
    /// </summary>
    public GenerativeResult? Generative { get; init; }

    /// <inheritdoc />
    public IEnumerator<GenerativeQueryResult<T>> GetEnumerator() => Results.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
