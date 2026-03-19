using Weaviate.Client.Managed.Schema;
using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Extensions;

/// <summary>
/// Extension methods for WeaviateClient to support ORM operations.
/// </summary>
public static class WeaviateClientExtensions
{
    /// <summary>
    /// Returns a new <see cref="ClientConfiguration"/> with the
    /// <c>X-Weaviate-Client-Integration</c> header set for the managed client.
    /// Use this when constructing <see cref="WeaviateContext"/> without dependency injection.
    /// </summary>
    /// <param name="config">The base configuration.</param>
    public static ClientConfiguration WithManagedIntegrationHeader(
        this ClientConfiguration config
    ) =>
        config.WithIntegration(
            WeaviateDefaults.IntegrationAgent(
                DependencyInjection.WeaviateManagedServiceCollectionExtensions.IntegrationName
            )
        );

    /// <summary>
    /// Creates a Weaviate collection from a C# class decorated with ORM attributes.
    /// The class must have a [WeaviateCollection] attribute or the class name will be used as the collection name.
    /// </summary>
    /// <typeparam name="T">The class type representing the collection schema.</typeparam>
    /// <param name="collections">The collections interface.</param>
    /// <param name="configure">Optional callback to modify the collection configuration before creation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A CollectionClient for the newly created collection.</returns>
    /// <example>
    /// <code>
    /// [WeaviateCollection("Articles")]
    /// public class Article
    /// {
    ///     [Property(DataType.Text)]
    ///     public string Title { get; set; }
    ///
    ///     [Vector&lt;Vectorizer.Text2VecOpenAI&gt;(Model = "ada-002")]
    ///     public float[]? Embedding { get; set; }
    /// }
    ///
    /// var collection = await client.Collections.CreateFromClass&lt;Article&gt;();
    /// // Or with configuration override:
    /// var collection = await client.Collections.CreateFromClass&lt;Article&gt;(
    ///     configure: config => config.Name = "CustomName");
    /// </code>
    /// </example>
    public static async Task<CollectionClient> CreateFromClass<T>(
        this CollectionsClient collections,
        Action<CollectionCreateParams>? configure = null,
        CancellationToken cancellationToken = default
    )
        where T : class
    {
        var config = CollectionSchemaBuilder.FromClass<T>();
        configure?.Invoke(config);
        return await collections.Create(config, cancellationToken);
    }
}
