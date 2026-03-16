using System.Reflection;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Managed.Query;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;
using Weaviate.Client.Serialization;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Mapping;

/// <summary>
/// Maps WeaviateObject query results to projection types.
/// Handles property mapping (by convention and [MapFrom]),
/// vector injection ([Vector]), metadata injection ([MetadataProperty]),
/// and ID injection ([WeaviateUUID]).
/// </summary>
internal static class ProjectionMapper
{
    /// <summary>
    /// Maps a WeaviateObject&lt;T&gt; to a projection type TProjection.
    /// </summary>
    /// <typeparam name="T">The entity type queried.</typeparam>
    /// <typeparam name="TProjection">The projection type to map to.</typeparam>
    /// <param name="weaviateObject">The WeaviateObject from the query result.</param>
    /// <returns>A populated TProjection instance.</returns>
    public static TProjection MapToProjection<T, TProjection>(WeaviateObject<T> weaviateObject)
        where T : class, new()
        where TProjection : class, new()
    {
        var untyped = weaviateObject.ToUntyped();

        // Build properties dict with MapFrom aliases applied
        var processedProperties = ApplyMapFromMappings<TProjection>(untyped.Properties);

        // Use PropertyConverterRegistry for base property mapping (case-insensitive)
        var projection =
            PropertyConverterRegistry.Default.BuildConcreteTypeFromProperties(
                processedProperties,
                typeof(TProjection)
            ) as TProjection
            ?? new TProjection();

        // Inject [WeaviateUUID] properties
        InjectId(projection, untyped.UUID);

        // Inject [Vector] properties
        if (untyped.Vectors != null && untyped.Vectors.Count > 0)
        {
            InjectVectors(projection, untyped.Vectors);
        }

        // Inject [MetadataProperty] properties (reuse existing MetadataMapper)
        if (untyped.Metadata != null)
        {
            MetadataMapper.InjectMetadata(projection, untyped.Metadata);
        }

        // Inject [Reference] properties
        if (untyped.References != null && untyped.References.Count > 0)
        {
            InjectReferences(projection, untyped.References);
        }

        return projection;
    }

    /// <summary>
    /// Pre-processes the Properties dictionary to handle [MapFrom] mappings.
    /// Creates aliases so that PropertyConverterRegistry can find properties
    /// by the projection property name.
    /// </summary>
    private static IDictionary<string, object?> ApplyMapFromMappings<TProjection>(
        IDictionary<string, object?>? properties
    )
        where TProjection : class
    {
        if (properties == null || properties.Count == 0)
            return new Dictionary<string, object?>();

        // Copy properties to a new dict (case-insensitive for matching)
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in properties)
        {
            result[kvp.Key] = kvp.Value;
        }

        // Add aliases for [MapFrom] properties
        var propsWithMapFrom = typeof(TProjection)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<MapFromAttribute>() != null);

        foreach (var prop in propsWithMapFrom)
        {
            var mapFrom = prop.GetCustomAttribute<MapFromAttribute>()!;
            var sourceName = PropertyHelper.ToCamelCase(mapFrom.SourcePropertyName);
            var targetName = PropertyHelper.ToCamelCase(prop.Name);

            if (
                sourceName != targetName
                && result.TryGetValue(sourceName, out var value)
                && !result.ContainsKey(targetName)
            )
            {
                result[targetName] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Injects the UUID into properties marked with [WeaviateUUID].
    /// </summary>
    private static void InjectId<TProjection>(TProjection projection, Guid? uuid)
        where TProjection : class
    {
        if (!uuid.HasValue)
            return;

        var idProps = typeof(TProjection)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<WeaviateUUIDAttribute>() != null && p.CanWrite);

        foreach (var prop in idProps)
        {
            if (prop.PropertyType == typeof(Guid))
            {
                prop.SetValue(projection, uuid.Value);
            }
            else if (prop.PropertyType == typeof(Guid?))
            {
                prop.SetValue(projection, uuid);
            }
        }
    }

    /// <summary>
    /// Injects vectors into properties marked with [Vector].
    /// </summary>
    private static void InjectVectors<TProjection>(TProjection projection, Vectors vectors)
        where TProjection : class
    {
        var vectorProps = typeof(TProjection)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<VectorAttribute>() != null && p.CanWrite);

        foreach (var prop in vectorProps)
        {
            var attr = prop.GetCustomAttribute<VectorAttribute>()!;
            var vectorName = attr.VectorName ?? PropertyHelper.ToCamelCase(prop.Name);

            if (!vectors.TryGetValue(vectorName, out var vectorValue))
                continue;

            try
            {
                if (prop.PropertyType == typeof(float[]))
                {
                    float[] floatArray = vectorValue;
                    prop.SetValue(projection, floatArray);
                }
                else if (prop.PropertyType == typeof(float[,]))
                {
                    float[,] multiVector = vectorValue;
                    prop.SetValue(projection, multiVector);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // If conversion fails, skip this vector
            }
        }
    }

    /// <summary>
    /// Gets the source property names (in camelCase) that the projection needs
    /// from the entity's properties. Used for auto-configuring returnProperties.
    /// Excludes [MetadataProperty], [Vector], [WeaviateUUID], and [Reference] properties.
    /// </summary>
    public static List<string> GetSourcePropertyNames<TProjection>()
        where TProjection : class
    {
        return typeof(TProjection)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<MetadataPropertyAttribute>() == null)
            .Where(p => p.GetCustomAttribute<VectorAttribute>() == null)
            .Where(p => p.GetCustomAttribute<WeaviateUUIDAttribute>() == null)
            .Where(p => p.GetCustomAttribute<ReferenceAttribute>() == null)
            .Select(p =>
            {
                var mapFrom = p.GetCustomAttribute<MapFromAttribute>();
                var sourceName = mapFrom?.SourcePropertyName ?? p.Name;
                return PropertyHelper.ToCamelCase(sourceName);
            })
            .ToList();
    }

    /// <summary>
    /// Gets the vector names (in camelCase) needed by the projection.
    /// </summary>
    public static List<string> GetVectorNames<TProjection>()
        where TProjection : class
    {
        return typeof(TProjection)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<VectorAttribute>() != null)
            .Select(p =>
            {
                var attr = p.GetCustomAttribute<VectorAttribute>()!;
                return attr.VectorName ?? PropertyHelper.ToCamelCase(p.Name);
            })
            .ToList();
    }

    /// <summary>
    /// Gets the MetadataOptions flags needed by the projection's [MetadataProperty] properties.
    /// </summary>
    public static MetadataOptions GetMetadataOptions<TProjection>()
        where TProjection : class
    {
        var options = MetadataOptions.None;

        var metadataProps = typeof(TProjection)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<MetadataPropertyAttribute>() != null);

        foreach (var prop in metadataProps)
        {
            var attr = prop.GetCustomAttribute<MetadataPropertyAttribute>()!;
            var fieldName = attr.MetadataField ?? prop.Name;

            options |= fieldName switch
            {
                "Score" => MetadataOptions.Score,
                "Distance" => MetadataOptions.Distance,
                "Certainty" => MetadataOptions.Certainty,
                "ExplainScore" => MetadataOptions.ExplainScore,
                "IsConsistent" => MetadataOptions.IsConsistent,
                "CreationTime" => MetadataOptions.CreationTime,
                "LastUpdateTime" => MetadataOptions.LastUpdateTime,
                _ => MetadataOptions.None,
            };
        }

        return options;
    }

    /// <summary>
    /// Gets the reference names (in camelCase) needed by the projection.
    /// Discovers properties marked with [Reference].
    /// </summary>
    public static List<string> GetReferenceNames<TProjection>()
        where TProjection : class
    {
        return typeof(TProjection)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<ReferenceAttribute>() != null)
            .Select(p =>
            {
                var attr = p.GetCustomAttribute<ReferenceAttribute>()!;
                var sourceName = attr.SourceProperty ?? p.Name;
                return PropertyHelper.ToCamelCase(sourceName);
            })
            .ToList();
    }

    /// <summary>
    /// Injects references into properties marked with [Reference].
    /// </summary>
    private static void InjectReferences<TProjection>(
        TProjection projection,
        IDictionary<string, IList<WeaviateObject>> references
    )
        where TProjection : class
    {
        var refProps = typeof(TProjection)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<ReferenceAttribute>() != null && p.CanWrite);

        foreach (var prop in refProps)
        {
            var attr = prop.GetCustomAttribute<ReferenceAttribute>()!;
            var sourceName = PropertyHelper.ToCamelCase(attr.SourceProperty ?? prop.Name);

            if (!references.TryGetValue(sourceName, out var refList) || refList.Count == 0)
                continue;

            // Single object reference
            if (prop.PropertyType.IsClass && !IsGenericList(prop.PropertyType))
            {
                var firstRef = refList.FirstOrDefault();
                if (firstRef != null)
                {
                    var instance = ReferenceMapper.DeserializeWeaviateObject(
                        firstRef,
                        prop.PropertyType
                    );
                    if (instance != null)
                    {
                        prop.SetValue(projection, instance);
                    }
                }
            }
            // List<T> reference
            else if (IsGenericList(prop.PropertyType))
            {
                var elementType = prop.PropertyType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);
                var list = Activator.CreateInstance(listType) as System.Collections.IList;

                if (list != null)
                {
                    foreach (var refObj in refList)
                    {
                        var instance = ReferenceMapper.DeserializeWeaviateObject(
                            refObj,
                            elementType
                        );
                        if (instance != null)
                        {
                            list.Add(instance);
                        }
                    }

                    if (list.Count > 0)
                    {
                        prop.SetValue(projection, list);
                    }
                }
            }
            // Guid? (ID-only)
            else if (prop.PropertyType == typeof(Guid) || prop.PropertyType == typeof(Guid?))
            {
                var firstRef = refList.FirstOrDefault();
                if (firstRef?.UUID != null)
                {
                    prop.SetValue(projection, firstRef.UUID);
                }
            }
        }
    }

    /// <summary>
    /// Builds a <see cref="TargetVectorBuilder{TEntity}"/> configure delegate from
    /// <c>[QueryProjection&lt;TEntity&gt;(Combination = ...)]</c> on <typeparamref name="TProjection"/>
    /// and the <c>[Vector]</c>-marked properties.
    /// Returns <c>null</c> when no combination is configured or no [Vector] properties are found.
    /// </summary>
    public static Func<
        TargetVectorBuilder<TEntity>,
        TargetVectorBuilder<TEntity>
    >? GetVectorTargetConfig<TProjection, TEntity>()
        where TEntity : class, new()
    {
        var attr = typeof(TProjection).GetCustomAttribute<QueryProjectionAttribute<TEntity>>();
        if (attr == null || attr.Combination == VectorCombination.None)
            return null;

        var vectorProps = typeof(TProjection)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<VectorAttribute>() != null)
            .ToList();

        if (vectorProps.Count == 0)
            return null;

        var combination = attr.Combination;

        if (
            combination == VectorCombination.ManualWeights
            || combination == VectorCombination.RelativeScore
        )
        {
            var weights = vectorProps
                .Select(p =>
                {
                    var vAttr = p.GetCustomAttribute<VectorAttribute>()!;
                    var name = vAttr.VectorName ?? PropertyHelper.ToCamelCase(p.Name);
                    return (name, vAttr.Weight);
                })
                .ToArray();

            return b => b.SetWeightedNameOnly(combination, weights);
        }
        else
        {
            var names = vectorProps
                .Select(p =>
                {
                    var vAttr = p.GetCustomAttribute<VectorAttribute>()!;
                    return vAttr.VectorName ?? PropertyHelper.ToCamelCase(p.Name);
                })
                .ToArray();

            return b => b.SetNameOnly(combination, names);
        }
    }

    private static bool IsGenericList(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var genericDef = type.GetGenericTypeDefinition();
        return genericDef == typeof(List<>)
            || genericDef == typeof(IList<>)
            || genericDef == typeof(IEnumerable<>)
            || genericDef == typeof(ICollection<>);
    }
}
