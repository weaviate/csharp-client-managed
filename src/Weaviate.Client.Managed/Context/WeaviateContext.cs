using System.Linq.Expressions;
using System.Reflection;
using Weaviate.Client.Managed.Models;
using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// Base class for Weaviate database contexts. Provides an EF Core-like experience
/// for managing collections, executing queries, and performing data operations.
/// </summary>
/// <remarks>
/// Derive from this class to create your own context with strongly-typed collection sets.
/// <example>
/// <code>
/// public class BookstoreContext : WeaviateContext
/// {
///     public BookstoreContext(WeaviateClient client) : base(client) { }
///
///     public CollectionSet&lt;Book&gt; Books { get; set; } = null!;
///     public CollectionSet&lt;Author&gt; Authors { get; set; } = null!;
/// }
/// </code>
/// </example>
/// </remarks>
public abstract class WeaviateContext : IAsyncDisposable
{
    private readonly WeaviateClient _client;
    private Dictionary<Type, object> _collectionSets = new();
    private WeaviateContextOptions _options;
    private bool _initialized;
    private string? _tenant;
    private ConsistencyLevels? _consistencyLevel;

    /// <summary>
    /// Gets the underlying Weaviate client.
    /// </summary>
    public WeaviateClient Client => _client;

    /// <summary>
    /// Provides access to administrative operations (backup, RBAC, cluster, aliases, health).
    /// </summary>
    public WeaviateAdmin Admin => new(_client);

    /// <summary>
    /// Creates a new context with the specified Weaviate client.
    /// </summary>
    /// <param name="client">The Weaviate client to use for operations.</param>
    protected WeaviateContext(WeaviateClient client)
        : this(client, new WeaviateContextOptions()) { }

    /// <summary>
    /// Creates a new context with the specified Weaviate client and pre-configured options.
    /// Used by dependency injection to pass options configured at registration time.
    /// </summary>
    /// <param name="client">The Weaviate client to use for operations.</param>
    /// <param name="options">Pre-configured context options.</param>
    protected WeaviateContext(WeaviateClient client, WeaviateContextOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _options = options ?? new WeaviateContextOptions();

        // Apply configuration (OnConfiguring can override DI-provided options)
        var optionsBuilder = new WeaviateContextOptionsBuilder(_options);
        OnConfiguring(optionsBuilder);

        // Discover and initialize collection sets
        InitializeCollectionSets();
    }

    /// <summary>
    /// Gets the context options.
    /// </summary>
    internal WeaviateContextOptions Options => _options;

    /// <summary>
    /// Override this method to configure the context options.
    /// </summary>
    /// <param name="options">The options builder.</param>
    protected virtual void OnConfiguring(WeaviateContextOptionsBuilder options) { }

    /// <summary>
    /// Gets a collection set for the specified entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>The collection set for the entity type.</returns>
    public CollectionSet<T> Set<T>()
        where T : class, new()
    {
        if (_collectionSets.TryGetValue(typeof(T), out var existingSet))
        {
            return (CollectionSet<T>)existingSet;
        }

        // Create a new collection set if not already registered
        var collectionSet = new CollectionSet<T>(this);
        _collectionSets[typeof(T)] = collectionSet;
        return collectionSet;
    }

    #region Context-Level Operations

    /// <summary>
    /// Queries the collection for the specified type.
    /// The collection is determined from the [WeaviateCollection] or [QueryProjection] attribute.
    /// </summary>
    /// <typeparam name="T">The entity type or projection type.</typeparam>
    /// <returns>A query client for building and executing queries.</returns>
    public Query.CollectionMapperQueryClient<T> Query<T>()
        where T : class, new()
    {
        return Set<T>().Query();
    }

    /// <summary>
    /// Inserts a single entity into its collection and returns it with its generated ID assigned.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inserted entity with its [WeaviateUUID] property populated.</returns>
    /// <example>
    /// <code>
    /// var product = await context.Insert(new Product { Name = "Widget" });
    /// Console.WriteLine(product.Id); // Guid assigned by Weaviate
    /// </code>
    /// </example>
    public Task<T> Insert<T>(T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Set<T>().Insert(entity, cancellationToken);
    }

    /// <summary>
    /// Creates a pending batch insert for the given entities.
    /// Chain additional <c>.Insert()</c> calls for ordered multi-batch execution,
    /// then call <c>.Execute(ct)</c> to run. Can also be directly awaited.
    /// IDs are assigned back to entities' [WeaviateUUID] properties.
    /// Throws <see cref="Models.BatchInsertException{T}"/> if any entities fail to insert.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <returns>A pending insert that executes on <c>.Execute(ct)</c> or direct await.</returns>
    public PendingInsert<T> Insert<T>(params T[] entities)
        where T : class, new()
    {
        return Set<T>().Insert(entities);
    }

    /// <summary>
    /// Updates a single entity in its collection (PATCH semantics) and returns it.
    /// The entity's [WeaviateUUID] property is used to identify the object.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated entity.</returns>
    public Task<T> Update<T>(T entity, CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Set<T>().Update(entity, cancellationToken);
    }

    /// <summary>
    /// Creates a pending batch update for one or more entities (PATCH semantics).
    /// Chain additional <c>.Update()</c> calls, then call <c>.Execute(ct)</c> or directly await.
    /// The entity's [WeaviateUUID] property is used to identify each object.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to update.</param>
    /// <returns>A pending update that executes on <c>.Execute(ct)</c> or direct await.</returns>
    public PendingUpdate<T> Update<T>(params T[] entities)
        where T : class, new()
    {
        return Set<T>().Update(entities);
    }

    /// <summary>
    /// Creates a pending batch delete for the given entity IDs.
    /// Chain additional <c>.Delete()</c> calls, then call <c>.Execute(ct)</c> or directly await.
    /// All IDs are deleted in a single server call using <c>Filter.UUID.ContainsAny</c>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="ids">The IDs of the entities to delete.</param>
    /// <returns>A pending delete that executes on <c>.Execute(ct)</c> or direct await.</returns>
    public PendingDelete<T> Delete<T>(params Guid[] ids)
        where T : class, new()
    {
        return Set<T>().Delete(ids);
    }

    /// <summary>
    /// Creates a pending batch delete for the given entities.
    /// Chain additional <c>.Delete()</c> calls, then call <c>.Execute(ct)</c> or directly await.
    /// All IDs are deleted in a single server call using <c>Filter.UUID.ContainsAny</c>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to delete.</param>
    /// <returns>A pending delete that executes on <c>.Execute(ct)</c> or direct await.</returns>
    public PendingDelete<T> Delete<T>(params T[] entities)
        where T : class, new()
    {
        return Set<T>().Delete(entities);
    }

    /// <summary>
    /// Deletes multiple entities using their UUID properties in a single batch operation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public Task DeleteMany<T>(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        return Set<T>().DeleteMany(entities, cancellationToken);
    }

    /// <summary>
    /// Creates a cross-collection batch insert that automatically orders operations
    /// by dependency. Referenced collections are inserted before the collections that
    /// reference them, regardless of the order <see cref="PendingBatch.Insert{T}"/> is called.
    /// </summary>
    /// <returns>A pending batch for accumulating cross-collection inserts.</returns>
    /// <example>
    /// <code>
    /// // Author is inserted before Book (detected via [Reference] on Book), even though
    /// // Book was added to the batch first.
    /// await context.Batch()
    ///     .Insert(book1, book2)
    ///     .Insert(author)
    ///     .Execute(ct);
    /// </code>
    /// </example>
    public PendingBatch Batch()
    {
        return new PendingBatch(this);
    }

    /// <summary>
    /// Returns the total count of objects in the collection for the specified type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of objects in the collection.</returns>
    public Task<ulong> Count<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        return Set<T>().Count(cancellationToken);
    }

    /// <summary>
    /// Iterates over all objects in the collection for the specified type.
    /// Uses cursor-based pagination internally for efficient iteration over large collections.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="after">Start iteration after this object ID (for resuming).</param>
    /// <param name="cacheSize">Number of objects to fetch per page (default 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of mapped entities.</returns>
    public IAsyncEnumerable<T> Iterator<T>(
        Guid? after = null,
        uint cacheSize = 100,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        return Set<T>().Iterator(after, cacheSize, cancellationToken);
    }

    #endregion

    #region Reference Operations

    /// <summary>
    /// Adds a cross-reference link from <paramref name="from"/> to an entity target and returns
    /// a <see cref="PendingReference{T}"/> for optional chaining.
    /// Can be directly awaited or chained with additional <c>.AddReference()</c> calls before
    /// calling <c>.Execute(ct)</c>. All accumulated links are sent in a single batch call.
    /// </summary>
    /// <typeparam name="T">The source entity type.</typeparam>
    /// <typeparam name="TTarget">The target entity type.</typeparam>
    /// <param name="from">The source entity (must have a populated <c>[WeaviateUUID]</c> property).</param>
    /// <param name="property">Expression selecting the reference property (e.g., <c>a => a.Category</c>).</param>
    /// <param name="to">The target entity (must have a populated <c>[WeaviateUUID]</c> property).</param>
    /// <returns>A pending reference that executes on <c>.Execute(ct)</c> or direct await.</returns>
    public PendingReference<T> AddReference<T, TTarget>(
        T from,
        Expression<Func<T, object?>> property,
        TTarget to
    )
        where T : class, new()
        where TTarget : class, new()
    {
        return Set<T>().AddReference(from, property, to);
    }

    /// <summary>
    /// Adds a cross-reference link from <paramref name="from"/> to a target by ID and returns
    /// a <see cref="PendingReference{T}"/> for optional chaining.
    /// Can be directly awaited or chained with additional <c>.AddReference()</c> calls before
    /// calling <c>.Execute(ct)</c>. All accumulated links are sent in a single batch call.
    /// </summary>
    /// <typeparam name="T">The source entity type.</typeparam>
    /// <param name="from">The source entity (must have a populated <c>[WeaviateUUID]</c> property).</param>
    /// <param name="property">Expression selecting the reference property.</param>
    /// <param name="toId">The UUID of the target object.</param>
    /// <returns>A pending reference that executes on <c>.Execute(ct)</c> or direct await.</returns>
    public PendingReference<T> AddReference<T>(
        T from,
        Expression<Func<T, object?>> property,
        Guid toId
    )
        where T : class, new()
    {
        return Set<T>().AddReference(from, property, toId);
    }

    /// <summary>
    /// Replaces all cross-reference links for a property on <paramref name="from"/> with
    /// the specified target IDs. Any existing links for that property are removed.
    /// </summary>
    /// <typeparam name="T">The source entity type.</typeparam>
    /// <param name="from">The source entity.</param>
    /// <param name="property">Expression selecting the reference property.</param>
    /// <param name="toIds">The UUIDs of the new target objects.</param>
    public Task ReplaceReferences<T>(
        T from,
        Expression<Func<T, object?>> property,
        params Guid[] toIds
    )
        where T : class, new()
    {
        return Set<T>().ReplaceReferences(from, property, default, toIds);
    }

    /// <summary>
    /// Removes a specific cross-reference link from <paramref name="from"/> to a target by ID.
    /// </summary>
    /// <typeparam name="T">The source entity type.</typeparam>
    /// <param name="from">The source entity.</param>
    /// <param name="property">Expression selecting the reference property.</param>
    /// <param name="toId">The UUID of the target object to unlink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeleteReference<T>(
        T from,
        Expression<Func<T, object?>> property,
        Guid toId,
        CancellationToken cancellationToken = default
    )
        where T : class, new()
    {
        return Set<T>().DeleteReference(from, property, toId, cancellationToken);
    }

    #endregion

    #region Aggregate Operations

    /// <summary>
    /// Creates an aggregation query for the collection inferred from the
    /// [QueryAggregate&lt;T&gt;] attribute on the projection type.
    /// </summary>
    /// <typeparam name="TProjection">The aggregation result type.</typeparam>
    /// <returns>A context aggregate builder.</returns>
    public Aggregates.ContextAggregateBuilder<TProjection> Aggregate<TProjection>()
        where TProjection : class, new()
    {
        var projectionType = typeof(TProjection);

        // Find the [QueryAggregate<T>] attribute (generic, so we match by open generic type)
        var aggregateForAttr = projectionType
            .GetCustomAttributes(inherit: false)
            .FirstOrDefault(a =>
                a.GetType().IsGenericType
                && a.GetType().GetGenericTypeDefinition()
                    == typeof(Attributes.QueryAggregateAttribute<>)
            );

        if (aggregateForAttr == null)
        {
            throw new InvalidOperationException(
                $"{projectionType.Name} must have an [QueryAggregate<T>] attribute "
                    + "to use context-level Aggregate<TProjection>()."
            );
        }

        // Extract TCollection from [QueryAggregate<TCollection>]
        var collectionType = aggregateForAttr.GetType().GetGenericArguments()[0];
        var collectionName = GetCollectionName(collectionType);
        var collection = _client.Collections.Use(collectionName);

        if (_tenant != null)
            collection = collection.WithTenant(_tenant);
        if (_consistencyLevel != null)
            collection = collection.WithConsistencyLevel(_consistencyLevel.Value);

        return new Aggregates.ContextAggregateBuilder<TProjection>(collection);
    }

    #endregion

    #region Migration Operations

    /// <summary>
    /// Gets pending migration plans for all collections in the context.
    /// Also detects orphaned collections (server collections not registered in the context).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of collection names to their migration plans.</returns>
    public async Task<Dictionary<string, Migrations.MigrationPlan>> GetPendingMigrations(
        CancellationToken cancellationToken = default
    )
    {
        var plans = new Dictionary<string, Migrations.MigrationPlan>();

        // Check registered collections for schema drift
        foreach (var (entityType, collectionSet) in _collectionSets)
        {
            var collectionName = GetCollectionName(entityType);
            var checkMethod = collectionSet.GetType().GetMethod("CheckMigrate");
            if (checkMethod != null)
            {
                var task =
                    (Task<Migrations.MigrationPlan>)
                        checkMethod.Invoke(collectionSet, [cancellationToken])!;
                var plan = await task.ConfigureAwait(false);
                plans[collectionName] = plan;
            }
        }

        // Detect orphaned collections on the server
        var registeredNames = _collectionSets
            .Keys.Select(GetCollectionName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await foreach (
            var serverCollection in _client
                .Collections.List(cancellationToken)
                .WithCancellation(cancellationToken)
        )
        {
            if (!registeredNames.Contains(serverCollection.Name))
            {
                plans[serverCollection.Name] = Migrations.MigrationPlan.ForOrphanedCollection(
                    serverCollection.Name
                );
            }
        }

        return plans;
    }

    /// <summary>
    /// Migrates all collections in the context to match their type definitions.
    /// </summary>
    /// <param name="allowBreakingChanges">If true, allows potentially destructive schema changes.</param>
    /// <param name="destructive">If true, deletes orphaned collections not registered in the context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task Migrate(
        bool allowBreakingChanges = false,
        bool destructive = false,
        CancellationToken cancellationToken = default
    )
    {
        // Apply schema migrations for registered collections
        foreach (var (_, collectionSet) in _collectionSets)
        {
            var migrateMethod = collectionSet.GetType().GetMethod("Migrate");
            if (migrateMethod != null)
            {
                var task =
                    (Task<Migrations.MigrationPlan>)
                        migrateMethod.Invoke(
                            collectionSet,
                            [true, allowBreakingChanges, cancellationToken]
                        )!;
                await task.ConfigureAwait(false);
            }
        }

        // Delete orphaned collections if destructive mode is enabled
        if (destructive)
        {
            var registeredNames = _collectionSets
                .Keys.Select(GetCollectionName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            await foreach (
                var serverCollection in _client
                    .Collections.List(cancellationToken)
                    .WithCancellation(cancellationToken)
            )
            {
                if (!registeredNames.Contains(serverCollection.Name))
                {
                    await _client
                        .Collections.Delete(serverCollection.Name, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    #endregion

    #region Scoping Operations

    /// <summary>
    /// Gets the tenant this context is scoped to, if any.
    /// </summary>
    public string? Tenant => _tenant;

    /// <summary>
    /// Gets the consistency level this context is scoped to, if any.
    /// </summary>
    public ConsistencyLevels? ConsistencyLevel => _consistencyLevel;

    /// <summary>
    /// Creates a new context scoped to the specified tenant.
    /// The returned context is the same derived type with the same collection set properties,
    /// but all operations are scoped to the given tenant.
    /// </summary>
    /// <param name="tenant">The tenant name.</param>
    /// <returns>A new context scoped to the specified tenant.</returns>
    public WeaviateContext ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenant);
        var clone = (WeaviateContext)MemberwiseClone();
        clone._tenant = tenant;
        // Copy options to avoid shared mutable state between parent and clone
        clone._options = new WeaviateContextOptions
        {
            AutoCreateCollections = _options.AutoCreateCollections,
            AutoMigrate = _options.AutoMigrate,
            AllowBreakingMigrations = _options.AllowBreakingMigrations,
        };
        clone.ReInitializeCollectionSets();
        return clone;
    }

    /// <summary>
    /// Creates a new context scoped to the specified consistency level.
    /// The returned context is the same derived type with the same collection set properties,
    /// but all operations use the given consistency level.
    /// </summary>
    /// <param name="consistencyLevel">The consistency level to use.</param>
    /// <returns>A new context scoped to the specified consistency level.</returns>
    public WeaviateContext WithConsistencyLevel(ConsistencyLevels consistencyLevel)
    {
        var clone = (WeaviateContext)MemberwiseClone();
        clone._consistencyLevel = consistencyLevel;
        // Copy options to avoid shared mutable state between parent and clone
        clone._options = new WeaviateContextOptions
        {
            AutoCreateCollections = _options.AutoCreateCollections,
            AutoMigrate = _options.AutoMigrate,
            AllowBreakingMigrations = _options.AllowBreakingMigrations,
        };
        clone.ReInitializeCollectionSets();
        return clone;
    }

    /// <summary>
    /// Re-initializes collection sets after cloning so they point to this context
    /// and create fresh (scoped) ManagedCollection instances.
    /// </summary>
    private void ReInitializeCollectionSets()
    {
        _collectionSets = new Dictionary<Type, object>();
        _initialized = false;
        InitializeCollectionSets();
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Gets the resolved collection name for an entity type.
    /// </summary>
    internal static string GetCollectionName(Type entityType)
    {
        return Schema.CollectionSchemaBuilder.ResolveCollectionName(entityType);
    }

    /// <summary>
    /// Gets the underlying managed collection for an entity type,
    /// applying any tenant or consistency level scoping.
    /// </summary>
    internal ManagedCollection<T> GetManagedCollection<T>()
        where T : class, new()
    {
        var collectionName = GetCollectionName(typeof(T));
        var collectionClient = _client.Collections.Use(collectionName);
        if (_tenant != null)
            collectionClient = collectionClient.WithTenant(_tenant);
        if (_consistencyLevel != null)
            collectionClient = collectionClient.WithConsistencyLevel(_consistencyLevel.Value);
        return new ManagedCollection<T>(collectionClient);
    }

    private void InitializeCollectionSets()
    {
        if (_initialized)
            return;

        var discoveredSets = CollectionSetDiscovery.DiscoverCollectionSets(GetType());

        foreach (var setInfo in discoveredSets)
        {
            // Create the CollectionSet<T> instance using internal constructor
            var collectionSetType = typeof(CollectionSet<>).MakeGenericType(setInfo.EntityType);
            var collectionSet = Activator.CreateInstance(
                collectionSetType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                [this],
                null
            )!;

            // Store in dictionary
            _collectionSets[setInfo.EntityType] = collectionSet;

            // Set the property on the derived context
            setInfo.Property.SetValue(this, collectionSet);
        }

        _initialized = true;
    }

    #endregion

    /// <summary>
    /// Disposes the context.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        // Currently no resources to dispose, but keeping for future use
        return ValueTask.CompletedTask;
    }
}
