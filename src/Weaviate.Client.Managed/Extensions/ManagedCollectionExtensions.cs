using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Extensions;

/// <summary>
/// Extension methods for creating and working with managed collections.
/// </summary>
public static class ManagedCollectionExtensions
{
    /// <summary>
    /// Creates a new collection from a class definition with Managed attributes
    /// and returns a strongly-typed wrapper.
    /// </summary>
    /// <typeparam name="T">The model type with Managed attributes.</typeparam>
    /// <param name="collections">The collections client.</param>
    /// <param name="configure">A callback to be able to change the collection configuration before it is created.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A managed collection providing type-safe operations.</returns>
    /// <example>
    /// <code>
    /// [WeaviateCollection("Products")]
    /// public class Product
    /// {
    ///     [Property(DataType.Text)]
    ///     public string Name { get; set; }
    ///
    ///     [Property(DataType.Number)]
    ///     public decimal Price { get; set; }
    /// }
    ///
    /// // Create the collection with full type safety
    /// var products = await client.Collections.CreateManaged&lt;Product&gt;();
    ///
    /// // Or with configuration override:
    /// var products = await client.Collections.CreateManaged&lt;Product&gt;(
    ///     configure: config => config.Name = "CustomProducts");
    ///
    /// // Now you can use it without repeating the type parameter
    /// await products.Insert(new Product { Name = "Widget", Price = 9.99m });
    /// </code>
    /// </example>
    public static async Task<ManagedCollection<T>> CreateManaged<T>(
        this CollectionsClient collections,
        Action<CollectionCreateParams>? configure = null,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        var collection = await collections.CreateFromClass<T>(configure, cancellationToken);
        return new ManagedCollection<T>(collection);
    }

    /// <summary>
    /// Gets an existing collection and wraps it in a strongly-typed managed collection.
    /// </summary>
    /// <typeparam name="T">The model type with Managed attributes.</typeparam>
    /// <param name="collections">The collections client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A managed collection for the existing collection.</returns>
    /// <example>
    /// <code>
    /// // Get an existing collection
    /// var products = await client.Collections.Managed&lt;Product&gt;();
    ///
    /// // Use it with full type safety
    /// var results = await products.Query
    ///     .Where(p => p.Price > 10)
    ///     .Execute();
    /// </code>
    /// </example>
    public static async Task<ManagedCollection<T>> Managed<T>(
        this CollectionsClient collections,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        var resolvedName = Schema.CollectionSchemaBuilder.ResolveCollectionName(typeof(T));

        // Verify the collection exists
        var exists = await collections.Exists(resolvedName, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException(
                $"Collection '{resolvedName}' does not exist. "
                    + $"Use CreateManaged<{typeof(T).Name}>() to create it first."
            );
        }

        var collection = collections.Use(resolvedName);
        return new ManagedCollection<T>(collection);
    }

    /// <summary>
    /// Gets an existing collection by name and wraps it in a strongly-typed managed collection.
    /// The collection name is resolved from the [WeaviateCollection] attribute on type T.
    /// </summary>
    /// <typeparam name="T">The model type with Managed attributes.</typeparam>
    /// <param name="collections">The collections client.</param>
    /// <returns>A managed collection for the existing collection.</returns>
    /// <remarks>
    /// This is syntactic sugar for <c>collections.Use(name).AsManaged&lt;T&gt;()</c>.
    /// Does not verify the collection exists - use <see cref="Managed{T}"/> if you need existence checking.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Compact syntax - collection name inferred from [WeaviateCollection]
    /// var products = client.Collections.UseManaged&lt;Product&gt;();
    ///
    /// // Equivalent to the verbose form:
    /// var products = client.Collections.Use("Products").AsManaged&lt;Product&gt;();
    ///
    /// // Use it with type safety
    /// await products.Insert(new Product { Name = "Widget" });
    /// </code>
    /// </example>
    public static ManagedCollection<T> UseManaged<T>(this CollectionsClient collections)
        where T : class, new()
    {
        var resolvedName = Schema.CollectionSchemaBuilder.ResolveCollectionName(typeof(T));
        var collection = collections.Use(resolvedName);
        return new ManagedCollection<T>(collection);
    }

    /// <summary>
    /// Wraps an existing <see cref="CollectionClient"/> in a strongly-typed managed collection.
    /// </summary>
    /// <typeparam name="T">The model type with Managed attributes.</typeparam>
    /// <param name="collection">The collection client to wrap.</param>
    /// <returns>A managed collection wrapping the provided collection.</returns>
    /// <example>
    /// <code>
    /// // If you already have a CollectionClient
    /// var collection = client.Collections.Use("Products");
    ///
    /// // Convert it to a managed collection
    /// var products = collection.AsManaged&lt;Product&gt;();
    ///
    /// // Now use it with type safety
    /// await products.Insert(new Product { Name = "Widget" });
    /// </code>
    /// </example>
    public static ManagedCollection<T> AsManaged<T>(this CollectionClient collection)
        where T : class, new()
    {
        return new ManagedCollection<T>(collection);
    }
}
