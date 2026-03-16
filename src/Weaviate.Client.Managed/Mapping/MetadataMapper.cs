using System.Reflection;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Mapping;

/// <summary>
/// Maps metadata values to properties marked with <see cref="MetadataPropertyAttribute"/>.
/// Handles automatic injection of query metadata (Score, Distance, etc.) into entity properties.
/// </summary>
internal static class MetadataMapper
{
    /// <summary>
    /// Injects metadata values into an object's properties marked with <see cref="MetadataPropertyAttribute"/>.
    /// Only populates properties that have a corresponding metadata value.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <param name="obj">The object to inject metadata into.</param>
    /// <param name="metadata">The Metadata from Weaviate.</param>
    public static void InjectMetadata<T>(T obj, Metadata? metadata)
        where T : class
    {
        if (metadata == null)
            return;

        var type = typeof(T);
        var metadataProps = type.GetProperties()
            .Where(p => p.GetCustomAttribute<MetadataPropertyAttribute>() != null && p.CanWrite)
            .ToList();

        if (metadataProps.Count == 0)
            return;

        foreach (var prop in metadataProps)
        {
            var attr = prop.GetCustomAttribute<MetadataPropertyAttribute>()!;
            var fieldName = attr.MetadataField ?? prop.Name;

            var value = GetMetadataValue(metadata, fieldName);

            if (value == null)
                continue;

            try
            {
                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var convertedValue = Convert.ChangeType(value, targetType);
                prop.SetValue(obj, convertedValue);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // If conversion fails, skip this property
                continue;
            }
        }
    }

    /// <summary>
    /// Gets the metadata value for a given field name.
    /// </summary>
    private static object? GetMetadataValue(Metadata metadata, string fieldName)
    {
        return fieldName switch
        {
            "Score" => metadata.Score,
            "Distance" => metadata.Distance,
            "Certainty" => metadata.Certainty,
            "ExplainScore" => metadata.ExplainScore,
            "IsConsistent" => metadata.IsConsistent,
            "RerankScore" => metadata.RerankScore,
            "CreationTime" => metadata.CreationTime,
            "LastUpdateTime" => metadata.LastUpdateTime,
            _ => null,
        };
    }

    /// <summary>
    /// Checks if a type has any metadata properties.
    /// </summary>
    /// <typeparam name="T">The object type.</typeparam>
    /// <returns>True if the type has metadata properties, false otherwise.</returns>
    public static bool HasMetadataProperties<T>()
        where T : class
    {
        var type = typeof(T);
        return type.GetProperties()
            .Any(p => p.GetCustomAttribute<MetadataPropertyAttribute>() != null);
    }
}
