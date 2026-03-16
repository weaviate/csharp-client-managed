namespace Weaviate.Client.Managed.Attributes;

/// <summary>
/// Marks a type as a query projection for a specific collection entity type.
/// Query projections allow selecting a subset of properties with optional renaming,
/// metadata injection, and vector inclusion.
/// </summary>
/// <typeparam name="TCollection">The collection entity type this projection applies to.</typeparam>
/// <example>
/// <code>
/// [QueryProjection&lt;Article&gt;]
/// public class ArticleSummary
/// {
///     public string Title { get; set; } = "";
///
///     [MapFrom("WordCount")]
///     public int Words { get; set; }
///
///     [MetadataProperty]
///     public double? Score { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class QueryProjectionAttribute<TCollection> : Attribute
    where TCollection : class, new()
{
    /// <summary>
    /// Gets the collection entity type.
    /// </summary>
    public Type CollectionType => typeof(TCollection);

    /// <summary>
    /// The vector combination strategy to use when this projection targets multiple named vectors.
    /// When set, <c>CollectionSet&lt;T&gt;.Project&lt;TProjection&gt;()</c> automatically calls
    /// <c>.VectorTargets()</c> using the <see cref="VectorAttribute"/>-marked properties on the projection
    /// as the target vectors.
    /// </summary>
    /// <remarks>
    /// <c>ManualWeights</c> reads <see cref="VectorAttribute.Weight"/> from each vector property.
    /// A method-based <c>ConfigureVectorTargets</c> on the projection class takes precedence over
    /// this attribute.
    /// </remarks>
    public VectorCombination Combination { get; set; }
}
