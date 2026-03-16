using System.Dynamic;
using System.Reflection;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Managed.Context;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Mapping;

/// <summary>
/// Maps entity properties to a dictionary for Weaviate serialization.
/// Filters out non-property attributes like [WeaviateUUID], [Vector], [Reference], and [MetadataProperty].
/// </summary>
internal static class PropertyMapper
{
    /// <summary>
    /// Converts an object to an ExpandoObject containing only the properties that should be serialized to Weaviate.
    /// Excludes properties with [WeaviateUUID], [Vector*], [Reference], and [MetadataProperty] attributes.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="obj">The object to convert.</param>
    /// <returns>An ExpandoObject with only the serializable properties.</returns>
    public static ExpandoObject ToWeaviateProperties<T>(T obj)
        where T : class
    {
        return ToWeaviatePropertiesInternal(obj, typeof(T));
    }

    /// <summary>
    /// Non-generic version for internal use.
    /// </summary>
    internal static ExpandoObject ToWeaviatePropertiesInternal(object obj, Type type)
    {
        var expando = new ExpandoObject();
        var expandoDict = (IDictionary<string, object?>)expando;

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            // Skip properties that should not be serialized
            if (ShouldExcludeProperty(prop))
                continue;

            var value = prop.GetValue(obj);

            // Convert property name to camelCase for Weaviate
            var propertyName = PropertyHelper.ToCamelCase(prop.Name);

            expandoDict[propertyName] = value;
        }

        return expando;
    }

    /// <summary>
    /// Determines if a property should be excluded from Weaviate serialization.
    /// </summary>
    private static bool ShouldExcludeProperty(PropertyInfo prop)
    {
        // Exclude [WeaviateUUID] properties
        if (prop.GetCustomAttribute<WeaviateUUIDAttribute>() != null)
            return true;

        // Exclude [Vector*] properties (any attribute inheriting from VectorAttributeBase)
        if (prop.GetCustomAttribute<VectorAttributeBase>() != null)
            return true;

        // Exclude [Reference] properties
        if (prop.GetCustomAttribute<ReferenceAttribute>() != null)
            return true;

        // Exclude [MetadataProperty] properties
        if (prop.GetCustomAttribute<MetadataPropertyAttribute>() != null)
            return true;

        // Exclude [NestedType] properties - these are handled specially
        if (prop.GetCustomAttribute<NestedTypeAttribute>() != null)
            return true;

        // Check if the property is the ID property by convention (UUID or Id named properties that are Guid)
        // We need to be careful here: only exclude if it's the actual ID property
        // If the type has [WeaviateCollection(IdProperty = "X")], that property should be excluded
        // If the property is named "UUID" or "Id" and is of type Guid, it might be the ID property
        var declaringType = prop.DeclaringType;
        if (declaringType != null)
        {
            var collectionAttr = declaringType.GetCustomAttribute<WeaviateCollectionAttribute>();

            // If IdProperty is explicitly set, check if this is that property
            if (collectionAttr?.IdProperty != null)
            {
                if (
                    string.Equals(
                        prop.Name,
                        collectionAttr.IdProperty,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return true;
            }
            else
            {
                // Check convention: property named "UUID" or "Id" of type Guid
                if (IsIdPropertyByConvention(prop))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a property is an ID property by naming convention.
    /// </summary>
    private static bool IsIdPropertyByConvention(PropertyInfo prop)
    {
        // Check if it's a Guid or Guid? property
        if (prop.PropertyType != typeof(Guid) && prop.PropertyType != typeof(Guid?))
            return false;

        // Check for common ID property names
        var name = prop.Name;
        return string.Equals(name, "UUID", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the names of properties that will be serialized to Weaviate.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <returns>List of property names in camelCase that will be serialized.</returns>
    public static List<string> GetSerializablePropertyNames<T>()
        where T : class
    {
        var type = typeof(T);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        return properties
            .Where(p => !ShouldExcludeProperty(p))
            .Select(p => PropertyHelper.ToCamelCase(p.Name))
            .ToList();
    }
}
