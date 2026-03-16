namespace Weaviate.Client.Managed.Attributes;

/// <summary>
/// Defines a cross-reference to another Weaviate collection.
/// The property can be a single object, a Guid (ID only), or a List of objects.
/// </summary>
/// <remarks>
/// <para>
/// The target collection name is inferred at runtime from the property type
/// (e.g., <c>Category?</c> → <c>"Category"</c>, <c>List&lt;Article&gt;</c> → <c>"Article"</c>).
/// Override with <see cref="Name"/> if the collection name differs from the type name.
/// </para>
/// <para>
/// For <c>Guid?</c>-typed properties the target type cannot be inferred — <see cref="Target"/>
/// is required in that case.
/// </para>
/// <para>
/// On query projection types this attribute also serves as the projection marker, replacing the
/// former <c>[IncludeReference]</c> attribute. Use <see cref="SourceProperty"/> to map from a
/// differently-named property on the source entity.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Convention-based: name and target inferred from property type
/// [Reference]
/// public Category? Category { get; set; }
///
/// // Explicit name override
/// [Reference("category")]
/// public Category? PrimaryCategory { get; set; }
///
/// // Multi-reference
/// [Reference]
/// public List&lt;Article&gt;? RelatedArticles { get; set; }
///
/// // Guid?-typed: Target required
/// [Reference(Target = typeof(Author))]
/// public Guid? AuthorId { get; set; }
///
/// // Projection: match source reference by convention
/// [Reference]
/// public Category? Category { get; set; }
///
/// // Projection: explicit source override
/// [Reference(SourceProperty = "PrimaryCategory")]
/// public Category? Category { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class ReferenceAttribute : Attribute
{
    /// <summary>
    /// Optional Weaviate property name override (in camelCase).
    /// Defaults to the C# property name converted to camelCase.
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Explicit target type — only required for <c>Guid?</c>-typed properties where the
    /// target collection cannot be inferred from the property type.
    /// </summary>
    public Type? Target { get; set; }

    /// <summary>
    /// Gets or sets the reference description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the loading strategy for this reference.
    /// When set to <see cref="ReferenceLoadingStrategy.Eager"/>, the reference is
    /// automatically included in every query without needing <c>.WithReferences()</c>.
    /// Defaults to <see cref="ReferenceLoadingStrategy.Explicit"/>.
    /// </summary>
    public ReferenceLoadingStrategy Loading { get; set; } = ReferenceLoadingStrategy.Explicit;

    /// <summary>
    /// Gets or sets the name of the reference property on the source entity type (for projection use).
    /// If null, matches by the projection property name converted to camelCase.
    /// </summary>
    public string? SourceProperty { get; set; }

    /// <summary>
    /// Initializes a new instance with the property name used as the Weaviate property name.
    /// </summary>
    public ReferenceAttribute() { }

    /// <summary>
    /// Initializes a new instance with an explicit Weaviate property name override.
    /// </summary>
    /// <param name="name">The Weaviate property name (camelCase).</param>
    public ReferenceAttribute(string name)
    {
        Name = name;
    }
}
