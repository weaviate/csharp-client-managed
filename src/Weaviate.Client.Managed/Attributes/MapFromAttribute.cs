namespace Weaviate.Client.Managed.Attributes;

/// <summary>
/// Specifies the source property name for a projection property when it differs
/// from the projection property name.
/// </summary>
/// <example>
/// <code>
/// [QueryProjection&lt;Article&gt;]
/// public class ArticleProjection
/// {
///     [MapFrom(nameof(Article.WordCount))]
///     public int Words { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class MapFromAttribute : Attribute
{
    /// <summary>
    /// The source property name in PascalCase.
    /// This will be converted to camelCase for Weaviate property lookup.
    /// </summary>
    public string SourcePropertyName { get; }

    /// <summary>
    /// Creates a new MapFrom attribute.
    /// </summary>
    /// <param name="sourcePropertyName">The source property name to map from (PascalCase).</param>
    public MapFromAttribute(string sourcePropertyName)
    {
        ArgumentNullException.ThrowIfNull(sourcePropertyName);
        SourcePropertyName = sourcePropertyName;
    }
}
