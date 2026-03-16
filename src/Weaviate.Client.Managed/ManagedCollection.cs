using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Aggregates;
using Weaviate.Client.Typed;

namespace Weaviate.Client.Managed;

/// <summary>
/// A strongly-typed wrapper around <see cref="CollectionClient"/> that provides
/// type-safe operations leveraging the Managed attribute system.
/// </summary>
/// <typeparam name="T">The model type with Managed attributes.</typeparam>
/// <example>
/// <code>
/// [WeaviateCollection("Products")]
/// public class Product
/// {
///     [Property(DataType.Number)]
///     public decimal Price { get; set; }
///
///     [Property(DataType.Text)]
///     public string Name { get; set; }
/// }
///
/// // Create a managed collection
/// var products = await client.Collections.CreateManaged&lt;Product&gt;();
///
/// // Use type-safe operations without repeating the type
/// await products.Insert(new Product { Name = "Widget", Price = 9.99m });
///
/// var results = await products.Query()
///     .Where(p => p.Price > 5)
///     .Limit(10)
///     .Execute();
///
/// var stats = await products.Aggregate
///     .WithMetrics&lt;ProductStats&gt;()
///     .Execute();
/// </code>
/// </example>
public sealed class ManagedCollection<T> : Context.IManagedCollectionBatchTarget
    where T : class, new()
{
    /// <summary>
    /// The underlying Weaviate collection client.
    /// </summary>
    public CollectionClient Inner { get; }

    /// <summary>
    /// The name of the collection.
    /// </summary>
    public string Name => Inner.Name;

    /// <summary>
    /// The tenant this collection is scoped to, if any.
    /// </summary>
    public string? Tenant => Inner.Tenant;

    /// <summary>
    /// Creates a new managed collection wrapper.
    /// </summary>
    /// <param name="collection">The underlying collection client.</param>
    internal ManagedCollection(CollectionClient collection)
    {
        Inner = collection;
    }

    #region Data Operations

    /// <summary>
    /// Inserts a single object into the collection.
    /// </summary>
    /// <param name="obj">The object to insert.</param>
    /// <param name="id">Optional UUID for the object. If not provided, one will be generated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The UUID of the inserted object.</returns>
    public Task<Guid> Insert(T obj, Guid? id = null, CancellationToken cancellationToken = default)
    {
        return Inner.Data.Insert(obj, id, cancellationToken);
    }

    /// <summary>
    /// Inserts multiple objects into the collection in a batch operation.
    /// </summary>
    /// <param name="objects">The objects to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch insert response containing UUIDs and any errors.</returns>
    public Task<BatchInsertResponse> InsertMany(
        IEnumerable<T> objects,
        CancellationToken cancellationToken = default
    )
    {
        return Inner.Data.InsertMany(objects, cancellationToken);
    }

    /// <summary>
    /// Inserts objects using pre-assigned UUIDs, without including their reference properties.
    /// References should be added separately via <see cref="AddReferences"/> after all objects
    /// are inserted.
    /// </summary>
    internal Task<BatchInsertResponse> InsertManyNoRefs(
        IEnumerable<T> objects,
        CancellationToken cancellationToken = default
    )
    {
        var requests = objects.Select(obj =>
        {
            var id = IdPropertyHelper.GetId(obj);
            var properties = PropertyMapper.ToWeaviateProperties(obj);
            var vectors = VectorMapper.ExtractVectors(obj);
            return new BatchInsertRequest(
                properties,
                id == Guid.Empty ? null : id,
                vectors,
                References: null
            );
        });
        return Inner.Data.InsertMany(requests, cancellationToken);
    }

    /// <summary>
    /// Collects all cross-reference operations (as <see cref="DataReference"/> records) for the
    /// given objects by reading their <c>[Reference]</c> properties.
    /// Used by <see cref="Context.PendingBatch"/> to build the Phase 2 AddReference payload.
    /// </summary>
    internal IEnumerable<DataReference> CollectReferences(IEnumerable<T> objects)
    {
        foreach (var obj in objects)
        {
            var fromId = IdPropertyHelper.GetId(obj);
            if (fromId == Guid.Empty)
                continue;

            var refs = ReferenceMapper.ExtractReferences(obj);
            if (refs == null)
                continue;

            foreach (var (propName, targets) in refs)
            {
                var toIds = targets
                    .Where(t => t.UUID.HasValue)
                    .Select(t => t.UUID!.Value)
                    .ToArray();
                if (toIds.Length > 0)
                    yield return new DataReference(fromId, propName, toIds);
            }
        }
    }

    /// <summary>
    /// Adds cross-references in batch via the Weaviate batch references API.
    /// Used internally by <see cref="Context.PendingBatch"/> during the deferred-reference phase.
    /// </summary>
    internal Task<BatchReferenceReturn> AddReferences(
        DataReference[] refs,
        CancellationToken cancellationToken = default
    ) => Inner.Data.ReferenceAddMany(refs, cancellationToken);

    /// <summary>
    /// Adds cross-references in batch via the Weaviate batch references API.
    /// Used by <see cref="Context.CollectionSet{T}"/> to execute a <see cref="Context.PendingReference{T}"/>.
    /// </summary>
    internal Task<BatchReferenceReturn> AddReferencesMany(
        DataReference[] refs,
        CancellationToken cancellationToken = default
    ) => Inner.Data.ReferenceAddMany(refs, cancellationToken);

    #region IManagedCollectionBatchTarget Implementation

    /// <summary>
    /// Explicit interface implementation for batch insert with type erasure.
    /// Casts type-erased entities to T and delegates to the typed InsertMany method.
    /// </summary>
    Task<BatchInsertResponse> Context.IManagedCollectionBatchTarget.InsertMany(
        IEnumerable<object> entities,
        CancellationToken cancellationToken
    )
    {
        return InsertMany(entities.Cast<T>(), cancellationToken);
    }

    /// <summary>
    /// Explicit interface implementation for batch insert without references.
    /// Casts type-erased entities to T and delegates to the typed InsertManyNoRefs method.
    /// </summary>
    Task Context.IManagedCollectionBatchTarget.InsertManyNoRefs(
        IEnumerable<object> entities,
        CancellationToken cancellationToken
    )
    {
        return InsertManyNoRefs(entities.Cast<T>(), cancellationToken);
    }

    /// <summary>
    /// Explicit interface implementation for collecting references.
    /// Casts type-erased entities to T and returns the collected references as an array.
    /// </summary>
    DataReference[] Context.IManagedCollectionBatchTarget.CollectReferences(
        IEnumerable<object> entities
    )
    {
        return CollectReferences(entities.Cast<T>()).ToArray();
    }

    /// <summary>
    /// Explicit interface implementation for adding references.
    /// Directly delegates to the internal AddReferences method.
    /// </summary>
    Task Context.IManagedCollectionBatchTarget.AddReferences(
        DataReference[] refs,
        CancellationToken cancellationToken
    )
    {
        return AddReferences(refs, cancellationToken);
    }

    #endregion

    /// <summary>
    /// Replaces all cross-reference links for a property on an object.
    /// Any existing links for that property are removed and replaced with <paramref name="toIds"/>.
    /// </summary>
    /// <param name="fromId">The UUID of the source object.</param>
    /// <param name="propertyName">The camelCase Weaviate property name.</param>
    /// <param name="toIds">The UUIDs of the new target objects.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task ReplaceReferences(
        Guid fromId,
        string propertyName,
        Guid[] toIds,
        CancellationToken cancellationToken = default
    ) => Inner.Data.ReferenceReplace(fromId, propertyName, toIds, cancellationToken);

    /// <summary>
    /// Removes a specific cross-reference link.
    /// </summary>
    /// <param name="fromId">The UUID of the source object.</param>
    /// <param name="propertyName">The camelCase Weaviate property name.</param>
    /// <param name="toId">The UUID of the target object to unlink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeleteReference(
        Guid fromId,
        string propertyName,
        Guid toId,
        CancellationToken cancellationToken = default
    ) => Inner.Data.ReferenceDelete(fromId, propertyName, toId, cancellationToken);

    /// <summary>
    /// Replaces an object in the collection (PUT semantics - full replacement).
    /// </summary>
    /// <param name="obj">The replacement object.</param>
    /// <param name="id">The UUID of the object to replace.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task Replace(T obj, Guid id, CancellationToken cancellationToken = default)
    {
        return Inner.Data.Replace(obj, id, cancellationToken);
    }

    /// <summary>
    /// Updates an object in the collection (PATCH semantics - partial update).
    /// </summary>
    /// <param name="obj">The object with updated properties.</param>
    /// <param name="id">The UUID of the object to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task Update(T obj, Guid id, CancellationToken cancellationToken = default)
    {
        return Inner.Data.Update(obj, id, cancellationToken);
    }

    /// <summary>
    /// Deletes an object from the collection by its UUID.
    /// </summary>
    /// <param name="id">The UUID of the object to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task Delete(Guid id, CancellationToken cancellationToken = default)
    {
        return Inner.Data.DeleteByID(id, cancellationToken);
    }

    /// <summary>
    /// Deletes multiple objects matching the filter criteria.
    /// </summary>
    /// <param name="where">Filter expression to identify objects to delete.</param>
    /// <param name="dryRun">If true, returns what would be deleted without actually deleting.</param>
    /// <param name="verbose">If true, returns detailed information about the deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing information about the deletion operation.</returns>
    public Task<DeleteManyResult> DeleteMany(
        Expression<Func<T, bool>> where,
        bool dryRun = false,
        bool verbose = false,
        CancellationToken cancellationToken = default
    )
    {
        return Inner.Data.DeleteMany(where, dryRun, verbose, cancellationToken);
    }

    /// <summary>
    /// Deletes multiple objects matching a raw filter.
    /// </summary>
    /// <param name="filter">The filter to identify objects to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing information about the deletion operation.</returns>
    public Task<DeleteManyResult> DeleteMany(
        Filter filter,
        CancellationToken cancellationToken = default
    )
    {
        return Inner.Data.DeleteMany(filter, cancellationToken: cancellationToken);
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Starts building a type-safe query for this collection.
    /// </summary>
    /// <returns>A query builder for constructing and executing queries.</returns>
    /// <example>
    /// <code>
    /// var results = await products.Query()
    ///     .Where(p => p.Price > 10)
    ///     .NearText("electronics")
    ///     .Limit(20)
    ///     .Execute();
    /// </code>
    /// </example>
    public CollectionMapperQueryClient<T> Query() => Inner.Query<T>();

    /// <summary>
    /// Starts building a projected query that maps results to TProjection instead of T.
    /// This is syntactic sugar for <c>Query().Project&lt;TProjection&gt;()</c>.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to map results to.</typeparam>
    /// <returns>A projected query builder.</returns>
    /// <example>
    /// <code>
    /// // Query with projection
    /// var summaries = await products.Query&lt;ProductSummary&gt;()
    ///     .Where(p => p.Price > 10)
    ///     .Execute();
    /// </code>
    /// </example>
    public ProjectedQueryClient<T, TProjection> Query<TProjection>()
        where TProjection : class, new()
    {
        return Query().Project<TProjection>();
    }

    #endregion

    #region Aggregate Operations

    /// <summary>
    /// Starts building a type-safe aggregation query for this collection.
    /// </summary>
    /// <returns>An aggregate query builder.</returns>
    /// <example>
    /// <code>
    /// public class ProductStats
    /// {
    ///     public double? PriceMean { get; set; }
    ///     public long? PriceCount { get; set; }
    /// }
    ///
    /// var stats = await products.Aggregate
    ///     .WithMetrics&lt;ProductStats&gt;()
    ///     .Where(p => p.InStock)
    ///     .Execute();
    ///
    /// Console.WriteLine($"Average price: {stats.Properties.PriceMean:C}");
    /// </code>
    /// </example>
    public AggregateStarter<T> Aggregate => new(Inner);

    #endregion

    #region Migration Operations

    /// <summary>
    /// Checks what schema changes would be needed to align the collection with the current type definition.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A migration plan describing necessary changes.</returns>
    public Task<Migrations.MigrationPlan> CheckMigrate(
        CancellationToken cancellationToken = default
    )
    {
        return Inner.Client.Collections.CheckMigrate<T>(cancellationToken);
    }

    /// <summary>
    /// Migrates the collection schema to match the current type definition.
    /// </summary>
    /// <param name="checkFirst">If true, validates the migration plan before executing.</param>
    /// <param name="allowBreakingChanges">If true, allows potentially destructive changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The executed migration plan.</returns>
    public Task<Migrations.MigrationPlan> Migrate(
        bool checkFirst = true,
        bool allowBreakingChanges = false,
        CancellationToken cancellationToken = default
    )
    {
        return Inner.Client.Collections.Migrate<T>(
            checkFirst,
            allowBreakingChanges,
            cancellationToken
        );
    }

    #endregion

    #region Collection Operations

    /// <summary>
    /// Returns the total count of objects in this collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of objects in the collection.</returns>
    public Task<ulong> Count(CancellationToken cancellationToken = default)
    {
        return Inner.Count(cancellationToken);
    }

    /// <summary>
    /// Iterates over all objects in the collection, returning strongly-typed mapped entities.
    /// Uses cursor-based pagination internally for efficient iteration over large collections.
    /// </summary>
    /// <param name="after">Start iteration after this object ID (for resuming).</param>
    /// <param name="cacheSize">Number of objects to fetch per page (default 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of mapped entities.</returns>
    /// <example>
    /// <code>
    /// await foreach (var product in products.Iterator())
    /// {
    ///     Console.WriteLine(product.Name);
    /// }
    /// </code>
    /// </example>
    public async IAsyncEnumerable<T> Iterator(
        Guid? after = null,
        uint cacheSize = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var typed = new TypedCollectionClient<T>(Inner);
        await foreach (
            var wo in typed
                .Iterator(after: after, cacheSize: cacheSize, cancellationToken: cancellationToken)
                .ConfigureAwait(false)
        )
        {
            yield return ManagedObjectMapper.FromWeaviateObject(wo);
        }
    }

    /// <summary>
    /// Checks if the collection exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the collection exists; otherwise false.</returns>
    public Task<bool> Exists(CancellationToken cancellationToken = default)
    {
        return Inner.Client.Collections.Exists(Name, cancellationToken);
    }

    /// <summary>
    /// Deletes this collection from Weaviate.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeleteCollection(CancellationToken cancellationToken = default)
    {
        return Inner.Client.Collections.Delete(Name, cancellationToken);
    }

    /// <summary>
    /// Exports the current collection configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection configuration export.</returns>
    public Task<CollectionConfigExport?> ExportConfig(CancellationToken cancellationToken = default)
    {
        return Inner.Client.Collections.Export(Name, cancellationToken);
    }

    #endregion

    #region Scoping Operations

    /// <summary>
    /// Creates a scoped collection for a specific tenant.
    /// </summary>
    /// <param name="tenant">The tenant name.</param>
    /// <returns>A new managed collection scoped to the specified tenant.</returns>
    public ManagedCollection<T> WithTenant(string tenant)
    {
        return new ManagedCollection<T>(Inner.WithTenant(tenant));
    }

    /// <summary>
    /// Creates a scoped collection with a specific consistency level.
    /// </summary>
    /// <param name="consistencyLevel">The consistency level to use.</param>
    /// <returns>A new managed collection scoped to the specified consistency level.</returns>
    public ManagedCollection<T> WithConsistencyLevel(ConsistencyLevels consistencyLevel)
    {
        return new ManagedCollection<T>(Inner.WithConsistencyLevel(consistencyLevel));
    }

    #endregion
}
