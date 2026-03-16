using System.Collections.Concurrent;
using System.Reflection;
using Weaviate.Client.Managed.Attributes;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// Helper class for discovering and accessing ID properties on entity types.
/// </summary>
internal static class IdPropertyHelper
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> _idPropertyCache = new();

    /// <summary>
    /// Gets the ID (UUID) value from an entity.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>The UUID value, or Guid.Empty if not set.</returns>
    public static Guid GetId<T>(T entity)
        where T : class
    {
        var property = GetIdProperty(typeof(T));
        if (property == null)
        {
            throw new InvalidOperationException(
                $"Type {typeof(T).Name} does not have a valid UUID property. "
                    + "Add a property named 'UUID' of type Guid, use [WeaviateUUID] attribute, "
                    + "or specify IdProperty in [WeaviateCollection]."
            );
        }

        var value = property.GetValue(entity);
        return value switch
        {
            Guid guid => guid,
            null => Guid.Empty,
            _ => Guid.Empty,
        };
    }

    /// <summary>
    /// Sets the ID (UUID) value on an entity.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <param name="id">The UUID value to set.</param>
    public static void SetId<T>(T entity, Guid id)
        where T : class
    {
        var property = GetIdProperty(typeof(T));
        if (property == null)
            return;

        if (!property.CanWrite)
        {
            throw new InvalidOperationException(
                $"UUID property '{property.Name}' on type {typeof(T).Name} is read-only."
            );
        }

        if (property.PropertyType == typeof(Guid))
        {
            property.SetValue(entity, id);
        }
        else if (property.PropertyType == typeof(Guid?))
        {
            property.SetValue(entity, (Guid?)id);
        }
    }

    /// <summary>
    /// Checks if an entity has a valid ID set (not Guid.Empty).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity.</param>
    /// <returns>True if the entity has a valid ID.</returns>
    public static bool HasValidId<T>(T entity)
        where T : class
    {
        var property = GetIdProperty(typeof(T));
        if (property == null)
            return false;

        return GetId(entity) != Guid.Empty;
    }

    /// <summary>
    /// Gets the ID property for a type.
    /// </summary>
    /// <param name="type">The entity type.</param>
    /// <returns>The ID property, or null if none found.</returns>
    public static PropertyInfo? GetIdProperty(Type type)
    {
        return _idPropertyCache.GetOrAdd(type, FindIdProperty);
    }

    private static PropertyInfo? FindIdProperty(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // 1. Check for [WeaviateUUID] attribute
        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute<WeaviateUUIDAttribute>() != null)
            {
                ValidateIdProperty(prop, type);
                return prop;
            }
        }

        // 2. Check WeaviateCollectionAttribute.IdProperty
        var collectionAttr = type.GetCustomAttribute<WeaviateCollectionAttribute>();
        if (!string.IsNullOrEmpty(collectionAttr?.IdProperty))
        {
            var prop = type.GetProperty(
                collectionAttr.IdProperty,
                BindingFlags.Public | BindingFlags.Instance
            );
            if (prop != null)
            {
                ValidateIdProperty(prop, type);
                return prop;
            }

            throw new InvalidOperationException(
                $"IdProperty '{collectionAttr.IdProperty}' specified in [WeaviateCollection] "
                    + $"was not found on type {type.Name}."
            );
        }

        // 3. Convention: look for property named "UUID" (Weaviate convention)
        var uuidProp = properties.FirstOrDefault(p =>
            string.Equals(p.Name, "UUID", StringComparison.OrdinalIgnoreCase)
        );

        if (uuidProp != null)
        {
            ValidateIdProperty(uuidProp, type);
            return uuidProp;
        }

        // 4. Fallback: look for property named "Id"
        var idProp = properties.FirstOrDefault(p =>
            string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase)
        );

        if (
            idProp != null
            && (idProp.PropertyType == typeof(Guid) || idProp.PropertyType == typeof(Guid?))
        )
        {
            return idProp;
        }

        return null;
    }

    private static void ValidateIdProperty(PropertyInfo property, Type type)
    {
        if (property.PropertyType != typeof(Guid) && property.PropertyType != typeof(Guid?))
        {
            throw new InvalidOperationException(
                $"ID property '{property.Name}' on type {type.Name} must be of type Guid or Guid?, "
                    + $"but is {property.PropertyType.Name}."
            );
        }
    }
}
