namespace Weaviate.Client.Managed.Attributes;

/// <summary>
/// Marks a property as the Weaviate UUID property for the entity.
/// The property must be of type Guid or Guid?.
/// </summary>
/// <remarks>
/// By default, the context looks for a property named "UUID" (matching Weaviate convention).
/// Use this attribute to specify a different property as the ID.
/// </remarks>
/// <example>
/// <code>
/// [WeaviateCollection]
/// public class Book
/// {
///     [WeaviateUUID]
///     public Guid BookId { get; set; }  // Custom ID property name
///
///     public string Title { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class WeaviateUUIDAttribute : Attribute { }
