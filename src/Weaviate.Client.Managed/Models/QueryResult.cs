using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Models;

/// <summary>
/// Wraps a query result with its associated metadata.
/// Unlike <see cref="Weaviate.Client.Models.Typed.WeaviateObject{T}"/>, the Object property
/// has vectors, references, and metadata already injected into entity properties marked with
/// <c>[Vector]</c>, <c>[Reference]</c>, and <c>[MetadataProperty]</c> attributes.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public record QueryResult<T>
{
    /// <summary>
    /// The Weaviate object UUID.
    /// </summary>
    public Guid? UUID { get; init; }

    /// <summary>
    /// The deserialized entity with <c>[Vector]</c>, <c>[Reference]</c>, and <c>[MetadataProperty]</c>
    /// properties already populated from the query result.
    /// </summary>
    public required T Object { get; init; }

    /// <summary>
    /// Query metadata (Score, Distance, Certainty, etc.) if requested via <c>WithMetadata()</c>.
    /// This is the same <see cref="Weaviate.Client.Models.Metadata"/> type from the base client.
    /// </summary>
    public Metadata? Metadata { get; init; }
}
