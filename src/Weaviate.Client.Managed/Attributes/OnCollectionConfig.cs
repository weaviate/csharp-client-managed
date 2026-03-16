using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Attributes;

/// <summary>
/// Configuration object passed to <see cref="WeaviateCollectionAttribute.CollectionConfigMethod"/>.
/// Allows registering lifecycle callbacks for collection operations.
/// </summary>
/// <example>
/// <code>
/// [WeaviateCollection("Products", CollectionConfigMethod = nameof(OnConfig))]
/// public class Product
/// {
///     public static void OnConfig(OnCollectionConfig config)
///     {
///         config.OnCreate(createParams =>
///         {
///             // Modify collection creation params
///             return createParams;
///         });
///     }
/// }
/// </code>
/// </example>
public class OnCollectionConfig
{
    internal Func<CollectionCreateParams, CollectionCreateParams>? OnCreateCallback
    {
        get;
        private set;
    }

    /// <summary>
    /// Registers a callback that is invoked when the collection is being created.
    /// The callback receives the pre-built <see cref="CollectionCreateParams"/> and can modify or replace it.
    /// </summary>
    /// <param name="callback">A function that transforms the collection creation parameters.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public OnCollectionConfig OnCreate(
        Func<CollectionCreateParams, CollectionCreateParams> callback
    )
    {
        OnCreateCallback = callback;
        return this;
    }

    /// <summary>
    /// A global interceptor invoked on every collection creation.
    /// Applied AFTER the per-class <see cref="WeaviateCollectionAttribute.CollectionConfigMethod"/>
    /// and BEFORE any external configure lambda.
    /// Useful for test infrastructure to modify collection names or other parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Thread Safety:</strong> This static property is read from multiple threads
    /// during schema creation and migrations. Callers must ensure that any assignment
    /// to this property happens before schema operations begin, or that read/write
    /// access is properly synchronized.
    /// </para>
    /// <para>
    /// Because this is a static field, integration tests that rely on it
    /// should be serialized (e.g. via xUnit's <c>[Collection]</c> attribute).
    /// </para>
    /// </remarks>
    public static Func<CollectionCreateParams, CollectionCreateParams>? GlobalOnCreate { get; set; }
}
