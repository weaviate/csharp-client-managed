namespace Weaviate.Client.Managed.Attributes;

/// <summary>
/// Marks a property to receive query metadata values (Score, Distance, Certainty, etc.).
/// The property name must match a metadata field name, or use <see cref="MetadataField"/>
/// to specify the mapping explicitly.
/// </summary>
/// <remarks>
/// Supported metadata fields:
/// <list type="bullet">
///   <item><description><c>Score</c> - Hybrid/BM25 search score (double?)</description></item>
///   <item><description><c>Distance</c> - Vector distance (double?)</description></item>
///   <item><description><c>Certainty</c> - Vector certainty (double?)</description></item>
///   <item><description><c>ExplainScore</c> - Score explanation (string?)</description></item>
///   <item><description><c>IsConsistent</c> - Consistency flag (bool?)</description></item>
///   <item><description><c>RerankScore</c> - Reranker score (double?)</description></item>
///   <item><description><c>CreationTime</c> - Object creation time (DateTime?)</description></item>
///   <item><description><c>LastUpdateTime</c> - Object last update time (DateTime?)</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// [WeaviateCollection("Cat")]
/// public class Cat
/// {
///     [Property(DataType.Text)]
///     public string Name { get; set; }
///
///     [MetadataProperty]
///     public double? Score { get; set; }
///
///     [MetadataProperty]
///     public double? Distance { get; set; }
///
///     [MetadataProperty(MetadataField = "CreationTime")]
///     public DateTime? Created { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class MetadataPropertyAttribute : Attribute
{
    /// <summary>
    /// The metadata field to map to this property.
    /// If null, uses the property name as the field name.
    /// </summary>
    public string? MetadataField { get; set; }
}
