using Weaviate.Client.Managed.Context;
using Weaviate.Client.Models.Typed;

namespace Weaviate.Client.Managed.Mapping;

/// <summary>
/// Provides bidirectional mapping between C# objects and Weaviate objects.
/// Handles automatic extraction and injection of vectors, references, and metadata.
/// </summary>
public static class ManagedObjectMapper
{
    /// <summary>
    /// Populates a C# object from a WeaviateObject, including vectors, references, and metadata.
    /// Uses the typed WeaviateObject's existing deserialization, then injects additional properties.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="weaviateObject">The WeaviateObject to convert from.</param>
    /// <returns>A fully populated C# object with properties, vectors, references, and metadata.</returns>
    public static T FromWeaviateObject<T>(WeaviateObject<T> weaviateObject)
        where T : class, new()
    {
        // Get the typed object (this handles basic property deserialization via existing infrastructure)
        var obj = weaviateObject.Object;

        // Inject UUID if present
        if (weaviateObject.UUID.HasValue)
        {
            IdPropertyHelper.SetId(obj, weaviateObject.UUID.Value);
        }

        // Inject vectors if present
        if (weaviateObject.Vectors != null && weaviateObject.Vectors.Count > 0)
        {
            VectorMapper.InjectVectors(obj, weaviateObject.Vectors);
        }

        // Inject references if present
        if (weaviateObject.References != null && weaviateObject.References.Count > 0)
        {
            ReferenceMapper.InjectReferences(obj, weaviateObject.References);
        }

        // Inject metadata if present (for [MetadataProperty] attributes)
        if (weaviateObject.Metadata != null)
        {
            MetadataMapper.InjectMetadata(obj, weaviateObject.Metadata);
        }

        return obj;
    }

    /// <summary>
    /// Converts a collection of WeaviateObject&lt;T&gt; to a collection of C# objects with vectors and references.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="weaviateObjects">The collection of WeaviateObjects.</param>
    /// <returns>Collection of fully populated C# objects.</returns>
    public static IEnumerable<T> FromWeaviateObjects<T>(
        IEnumerable<WeaviateObject<T>> weaviateObjects
    )
        where T : class, new()
    {
        return weaviateObjects.Select(FromWeaviateObject);
    }

    /// <summary>
    /// Checks if a type has any vector, reference, or metadata properties that need special mapping.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <returns>True if the type has vectors, references, or metadata properties, false otherwise.</returns>
    public static bool RequiresMapping<T>()
        where T : class
    {
        return VectorMapper.HasVectorProperties<T>()
            || ReferenceMapper.GetReferencePropertyNames<T>().Count > 0
            || MetadataMapper.HasMetadataProperties<T>();
    }
}
