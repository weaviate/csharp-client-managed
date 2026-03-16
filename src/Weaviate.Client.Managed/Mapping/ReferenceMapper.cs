using System.Reflection;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Managed.Context;
using Weaviate.Client.Managed.Schema;
using Weaviate.Client.Models;
using Weaviate.Client.Serialization;

namespace Weaviate.Client.Managed.Mapping;

/// <summary>
/// Maps reference properties to and from Weaviate's References dictionary.
/// Handles automatic extraction and injection of cross-references.
/// </summary>
internal static class ReferenceMapper
{
    /// <summary>
    /// Extracts references from an object into a Weaviate-compatible format.
    /// Supports:
    /// - Single reference (T?)
    /// - ID-only reference (Guid?)
    /// - Multi-reference (List&lt;T&gt;)
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="obj">The object to extract references from.</param>
    /// <returns>A dictionary of references, or null if no references found.</returns>
    public static IDictionary<string, IList<WeaviateObject>>? ExtractReferences<T>(T obj)
        where T : class
    {
        var type = typeof(T);
        var refProps = type.GetProperties()
            .Where(p => p.GetCustomAttribute<ReferenceAttribute>() != null)
            .ToList();

        if (refProps.Count == 0)
            return null;

        var references = new Dictionary<string, IList<WeaviateObject>>();

        foreach (var prop in refProps)
        {
            var refAttr = prop.GetCustomAttribute<ReferenceAttribute>()!;
            var refName =
                refAttr.Name
                ?? Weaviate.Client.Managed.Internal.PropertyHelper.ToCamelCase(prop.Name);
            var value = prop.GetValue(obj);

            if (value == null)
                continue;

            // Handle Guid? (ID-only reference)
            if (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?))
            {
                var id = value is Guid guid ? guid : (Guid?)value;
                if (id.HasValue)
                {
                    references[refName] = new List<WeaviateObject>
                    {
                        new WeaviateObject
                        {
                            UUID = id.Value,
                            Collection = ResolveTargetCollection(prop, refAttr),
                        },
                    };
                }
            }
            // Handle List<Guid> (multi-ID reference)
            else if (IsGenericList(prop.PropertyType, typeof(Guid)))
            {
                var ids = value as IEnumerable<Guid>;
                if (ids != null)
                {
                    var refList = ids.Select(id => new WeaviateObject
                        {
                            UUID = id,
                            Collection = ResolveTargetCollection(prop, refAttr),
                        })
                        .ToList();

                    if (refList.Count > 0)
                    {
                        references[refName] = refList;
                    }
                }
            }
            // Handle single object reference (T?)
            else if (prop.PropertyType.IsClass && !IsGenericList(prop.PropertyType))
            {
                // For single object references, use IdPropertyHelper to find the ID property
                var idProp = IdPropertyHelper.GetIdProperty(prop.PropertyType);
                if (idProp != null)
                {
                    var id = idProp.GetValue(value) as Guid?;
                    if (id.HasValue)
                    {
                        references[refName] = new List<WeaviateObject>
                        {
                            new WeaviateObject
                            {
                                UUID = id.Value,
                                Collection = ResolveTargetCollection(prop, refAttr),
                            },
                        };
                    }
                }
            }
            // Handle List<T> (multi-object reference)
            else if (IsGenericList(prop.PropertyType))
            {
                var elementType = prop.PropertyType.GetGenericArguments()[0];
                var idProp = IdPropertyHelper.GetIdProperty(elementType);

                if (idProp != null)
                {
                    var list = value as System.Collections.IEnumerable;
                    if (list != null)
                    {
                        var refList = new List<WeaviateObject>();
                        foreach (var item in list)
                        {
                            var id = idProp.GetValue(item) as Guid?;
                            if (id.HasValue)
                            {
                                refList.Add(
                                    new WeaviateObject
                                    {
                                        UUID = id.Value,
                                        Collection = ResolveTargetCollection(prop, refAttr),
                                    }
                                );
                            }
                        }

                        if (refList.Count > 0)
                        {
                            references[refName] = refList;
                        }
                    }
                }
            }
        }

        return references.Count > 0 ? references : null;
    }

    /// <summary>
    /// Injects references from a References dictionary into an object's properties.
    /// Note: This only populates ID fields. Full object hydration requires additional queries.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="obj">The object to inject references into.</param>
    /// <param name="references">The References dictionary from Weaviate.</param>
    public static void InjectReferences<T>(
        T obj,
        IDictionary<string, IList<WeaviateObject>>? references
    )
        where T : class
    {
        InjectReferencesInternal(obj, references, typeof(T));
    }

    /// <summary>
    /// Internal non-generic implementation of reference injection.
    /// </summary>
    private static void InjectReferencesInternal(
        object obj,
        IDictionary<string, IList<WeaviateObject>>? references,
        Type type
    )
    {
        if (references == null || references.Count == 0)
            return;

        var refProps = type.GetProperties()
            .Where(p => p.GetCustomAttribute<ReferenceAttribute>() != null && p.CanWrite)
            .ToList();

        foreach (var prop in refProps)
        {
            var refAttr = prop.GetCustomAttribute<ReferenceAttribute>()!;
            var refName =
                refAttr.Name
                ?? Weaviate.Client.Managed.Internal.PropertyHelper.ToCamelCase(prop.Name);

            if (!references.TryGetValue(refName, out var refList) || refList.Count == 0)
                continue;

            // Handle Guid? (ID-only reference)
            if (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?))
            {
                var firstRef = refList.FirstOrDefault();
                if (firstRef?.UUID != null)
                {
                    prop.SetValue(obj, firstRef.UUID);
                }
            }
            // Handle List<Guid> (multi-ID reference)
            else if (IsGenericList(prop.PropertyType, typeof(Guid)))
            {
                var ids = refList.Where(r => r.UUID.HasValue).Select(r => r.UUID!.Value).ToList();
                prop.SetValue(obj, ids);
            }
            // Handle single object reference (T?)
            else if (prop.PropertyType.IsClass && !IsGenericList(prop.PropertyType))
            {
                var firstRef = refList.FirstOrDefault();
                if (firstRef != null)
                {
                    // Deserialize the referenced object from Properties
                    var instance = DeserializeWeaviateObject(firstRef, prop.PropertyType);
                    if (instance != null)
                    {
                        prop.SetValue(obj, instance);
                    }
                }
            }
            // Handle List<T> (multi-object reference)
            else if (IsGenericList(prop.PropertyType))
            {
                var elementType = prop.PropertyType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = Activator.CreateInstance(listType) as System.Collections.IList;

                if (list != null)
                {
                    foreach (var refObj in refList)
                    {
                        var instance = DeserializeWeaviateObject(refObj, elementType);
                        if (instance != null)
                        {
                            list.Add(instance);
                        }
                    }

                    if (list.Count > 0)
                    {
                        prop.SetValue(obj, list);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Deserializes a WeaviateObject into the target type using Weaviate.Client's internal serialization infrastructure.
    /// This ensures consistent behavior with the base client's property conversion system.
    /// </summary>
    /// <param name="weaviateObject">The WeaviateObject with Properties to deserialize.</param>
    /// <param name="targetType">The target type to deserialize into.</param>
    /// <returns>An instance of the target type with properties populated, or null if deserialization fails.</returns>
    internal static object? DeserializeWeaviateObject(
        WeaviateObject weaviateObject,
        Type targetType
    )
    {
        if (weaviateObject.Properties == null || weaviateObject.Properties.Count == 0)
        {
            // No properties to deserialize, just return an object with ID
            var instance = Activator.CreateInstance(targetType);
            SetIdProperty(instance, weaviateObject.UUID, targetType);
            return instance;
        }

        try
        {
            // Use Weaviate.Client's internal property deserialization infrastructure
            // This handles all type conversions (DateTime, Guid, GeoCoordinates, PhoneNumber, nested objects, etc.)
            var instance = PropertyConverterRegistry.Default.BuildConcreteTypeFromProperties(
                weaviateObject.Properties,
                targetType
            );

            if (instance == null)
                return null;

            // Set UUID/Id property
            SetIdProperty(instance, weaviateObject.UUID, targetType);

            // Recursively inject nested references if present
            if (weaviateObject.References != null && weaviateObject.References.Count > 0)
            {
                InjectReferencesInternal(instance, weaviateObject.References, targetType);
            }

            return instance;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Sets the ID property on an instance if the UUID is present.
    /// </summary>
    private static void SetIdProperty(object? instance, Guid? uuid, Type targetType)
    {
        if (instance == null || !uuid.HasValue)
            return;

        var idProp = targetType.GetProperty(
            "Id",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
        );

        if (idProp != null && idProp.CanWrite)
        {
            idProp.SetValue(instance, uuid.Value);
        }
    }

    /// <summary>
    /// Gets the names of all reference properties on a type (in camelCase).
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <returns>List of reference property names in camelCase.</returns>
    public static List<string> GetReferencePropertyNames<T>()
        where T : class
    {
        var type = typeof(T);
        return type.GetProperties()
            .Where(p => p.GetCustomAttribute<ReferenceAttribute>() != null)
            .Select(p =>
            {
                var attr = p.GetCustomAttribute<ReferenceAttribute>()!;
                return attr.Name
                    ?? Weaviate.Client.Managed.Internal.PropertyHelper.ToCamelCase(p.Name);
            })
            .ToList();
    }

    /// <summary>
    /// Resolves the Weaviate target collection name for a reference property.
    /// The target is inferred from the property type unless <see cref="ReferenceAttribute.Target"/>
    /// is explicitly set. For <c>Guid?</c>-typed properties, <see cref="ReferenceAttribute.Target"/>
    /// must be set or an <see cref="InvalidOperationException"/> is thrown.
    /// </summary>
    internal static string ResolveTargetCollection(PropertyInfo prop, ReferenceAttribute refAttr)
    {
        // Explicit Target= always wins
        if (refAttr.Target != null)
            return CollectionSchemaBuilder.ResolveCollectionName(refAttr.Target);

        var propType = prop.PropertyType;

        // Guid / Guid? — cannot infer target
        if (propType == typeof(Guid) || propType == typeof(Guid?))
        {
            throw new InvalidOperationException(
                $"[Reference] on Guid?-typed property '{prop.DeclaringType?.Name}.{prop.Name}' "
                    + "requires Target = typeof(TargetEntity) because the target collection cannot be inferred."
            );
        }

        // List<T> — extract element type
        if (IsGenericList(propType))
        {
            propType = propType.GetGenericArguments()[0];
        }

        // Nullable<T> is not a thing for classes in C#; property type for `Category?` is just `Category`
        return CollectionSchemaBuilder.ResolveCollectionName(propType);
    }

    /// <summary>
    /// Checks if a type is a generic List.
    /// </summary>
    private static bool IsGenericList(Type type, Type? elementType = null)
    {
        if (!type.IsGenericType)
            return false;

        var genericDef = type.GetGenericTypeDefinition();
        if (
            genericDef != typeof(List<>)
            && genericDef != typeof(IList<>)
            && genericDef != typeof(IEnumerable<>)
            && genericDef != typeof(ICollection<>)
        )
            return false;

        if (elementType != null)
        {
            var args = type.GetGenericArguments();
            return args.Length == 1 && args[0] == elementType;
        }

        return true;
    }
}
