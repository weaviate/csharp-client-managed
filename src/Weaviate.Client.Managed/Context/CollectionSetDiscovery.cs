using System.Reflection;
using Weaviate.Client.Managed.Attributes;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// Discovers CollectionSet&lt;T&gt; properties on context classes via reflection.
/// </summary>
internal static class CollectionSetDiscovery
{
    /// <summary>
    /// Discovers all CollectionSet&lt;T&gt; properties on a context type.
    /// </summary>
    /// <param name="contextType">The context type to scan.</param>
    /// <returns>Information about discovered collection sets.</returns>
    public static IEnumerable<CollectionSetInfo> DiscoverCollectionSets(Type contextType)
    {
        var properties = contextType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
        );

        foreach (var property in properties)
        {
            if (!IsCollectionSetProperty(property, out var entityType) || entityType is null)
                continue;

            // Validate that the entity type has WeaviateCollectionAttribute
            var collectionAttr = entityType.GetCustomAttribute<WeaviateCollectionAttribute>();
            var collectionName = collectionAttr?.Name ?? entityType.Name;

            yield return new CollectionSetInfo(property, entityType, collectionName);
        }
    }

    /// <summary>
    /// Checks if a property is a CollectionSet&lt;T&gt; and extracts the entity type.
    /// </summary>
    private static bool IsCollectionSetProperty(PropertyInfo property, out Type? entityType)
    {
        entityType = null;

        var propertyType = property.PropertyType;
        if (!propertyType.IsGenericType)
            return false;

        var genericTypeDef = propertyType.GetGenericTypeDefinition();
        if (genericTypeDef != typeof(CollectionSet<>))
            return false;

        entityType = propertyType.GetGenericArguments()[0];
        return true;
    }
}

/// <summary>
/// Information about a discovered CollectionSet property.
/// </summary>
/// <param name="Property">The property info.</param>
/// <param name="EntityType">The entity type (T in CollectionSet&lt;T&gt;).</param>
/// <param name="CollectionName">The Weaviate collection name.</param>
internal record CollectionSetInfo(PropertyInfo Property, Type EntityType, string CollectionName);
