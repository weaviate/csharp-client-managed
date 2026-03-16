using System.Collections;
using System.Linq.Expressions;
using Weaviate.Client.Managed.Aggregates;
using Weaviate.Client.Managed.Internal;
using Weaviate.Client.Managed.Models;
using Weaviate.Client.Managed.Query;
using Weaviate.Client.Models;
using ManagedPropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// Represents a collection of entities in a Weaviate database context.
/// Similar to EF Core's DbSet&lt;T&gt;, this provides access to query and manipulate
/// entities of type T.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public class CollectionSet<T> : IQueryable<T>, IOrderedQueryable<T>
    where T : class, new()
{
    private readonly WeaviateContext _context;
    private ManagedCollection<T>? _collection;
    private WeaviateQueryProvider<T>? _queryableProvider;

    /// <summary>
    /// Gets the name of the Weaviate collection.
    /// </summary>
    public string CollectionName { get; }

    /// <summary>
    /// Creates a new collection set for the specified context.
    /// </summary>
    /// <param name="context">The parent context.</param>
    internal CollectionSet(WeaviateContext context)
    {
        _context = context;
        CollectionName = WeaviateContext.GetCollectionName(typeof(T));
    }

    /// <summary>
    /// Gets the underlying managed collection, initializing it lazily.
    /// </summary>
    private ManagedCollection<T> Collection => _collection ??= _context.GetManagedCollection<T>();

    #region IQueryable<T>

    /// <inheritdoc />
    public Expression Expression => Expression.Constant(this);

    /// <inheritdoc />
    public Type ElementType => typeof(T);

    /// <inheritdoc />
    public IQueryProvider Provider =>
        _queryableProvider ??= new WeaviateQueryProvider<T>(this, () => Collection.Query());

    /// <summary>Throws — use <c>await</c> or <see cref="Iterator"/> instead.</summary>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() =>
        throw new InvalidOperationException(
            $"CollectionSet<{typeof(T).Name}> cannot be enumerated synchronously. "
                + "Use 'await', Iterator(), or a LINQ query with 'await'."
        );

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();

    /// <summary>
    /// Creates a <see cref="WeaviateQueryable{T}"/> rooted at this collection set.
    /// Used internally by Weaviate LINQ extension methods.
    /// </summary>
    internal WeaviateQueryable<T> ToWeaviateQueryable() =>
        new WeaviateQueryable<T>(new WeaviateQueryConfig(), [], () => Collection.Query(), Provider);

    /// <summary>
    /// Allows <c>await context.Products</c> to fetch all objects in the collection.
    /// Also handles <c>await (from p in context.Products select p)</c> since the C# compiler
    /// degenerates the trivial identity-select query to the source.
    /// </summary>
    public System.Runtime.CompilerServices.TaskAwaiter<IEnumerable<T>> GetAwaiter() =>
        ToWeaviateQueryable().ExecuteAsync(CancellationToken.None).GetAwaiter();

    #endregion

    #region Data Operations

    /// <summary>
    /// Inserts a single entity into the collection and returns it with its generated ID assigned.
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inserted entity with its [WeaviateUUID] property populated.</returns>
    /// <example>
    /// <code>
    /// var product = await context.Products.Insert(new Product { Name = "Widget" });
    /// Console.WriteLine(product.Id); // Guid assigned by Weaviate
    /// </code>
    /// </example>
    public async Task<T> Insert(T entity, CancellationToken cancellationToken = default)
    {
        var id = await Collection
            .Insert(entity, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        IdPropertyHelper.SetId(entity, id);
        return entity;
    }

    /// <summary>
    /// Creates a pending batch insert for the given entities.
    /// Chain additional <c>.Insert()</c> calls for ordered multi-batch execution,
    /// then call <c>.Execute(ct)</c> to run. Can also be directly awaited.
    /// IDs are assigned back to entities' [WeaviateUUID] properties.
    /// Throws <see cref="BatchInsertException{T}"/> if any entities fail to insert.
    /// </summary>
    /// <param name="entities">The entities to insert.</param>
    /// <returns>A pending insert that executes on <c>.Execute(ct)</c> or direct await.</returns>
    /// <example>
    /// <code>
    /// // Simple batch
    /// var products = await context.Products.Insert(p1, p2, p3);
    ///
    /// // Ordered multi-batch (p4 can reference p1/p2/p3 as same-collection references)
    /// var all = await context.Products.Insert(p1, p2, p3).Insert(p4).Execute(ct);
    /// </code>
    /// </example>
    public PendingInsert<T> Insert(params T[] entities)
    {
        return new PendingInsert<T>(this, entities);
    }

    /// <summary>
    /// Executes a single batch insert, assigning IDs back to entities.
    /// Used internally by <see cref="PendingInsert{T}"/>.
    /// </summary>
    internal async Task<T[]> ExecuteBatch(
        T[] entities,
        CancellationToken cancellationToken = default
    )
    {
        var response = await Collection
            .InsertMany(entities, cancellationToken)
            .ConfigureAwait(false);

        var succeeded = new List<T>();
        var failed = new List<BatchInsertError<T>>();

        foreach (var entry in response.Objects)
        {
            if (entry.Index < 0 || entry.Index >= entities.Length)
                continue;

            var entity = entities[entry.Index];
            if (entry.Error is not null)
                failed.Add(new BatchInsertError<T>(entity, entry.Error));
            else if (entry.UUID.HasValue)
            {
                IdPropertyHelper.SetId(entity, entry.UUID.Value);
                succeeded.Add(entity);
            }
        }

        if (failed.Count > 0)
            throw new BatchInsertException<T>(succeeded, failed);

        return succeeded.ToArray();
    }

    /// <summary>
    /// Updates a single entity in the collection (PATCH semantics) and returns it.
    /// The entity's [WeaviateUUID] property is used to identify the object.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated entity.</returns>
    public async Task<T> Update(T entity, CancellationToken cancellationToken = default)
    {
        var id = IdPropertyHelper.GetId(entity);
        if (id == Guid.Empty)
            throw new InvalidOperationException(
                $"Cannot update entity of type {typeof(T).Name}: UUID property is not set."
            );

        await Collection.Update(entity, id, cancellationToken).ConfigureAwait(false);
        return entity;
    }

    /// <summary>
    /// Creates a pending batch update for the given entities.
    /// Chain additional <c>.Update()</c> calls to accumulate more entities,
    /// then call <c>.Execute(ct)</c> to run. Can also be directly awaited.
    /// Each entity is updated individually (PATCH semantics); Weaviate has no batch-update endpoint.
    /// </summary>
    /// <param name="entities">The entities to update.</param>
    /// <returns>A pending update that executes on <c>.Execute(ct)</c> or direct await.</returns>
    /// <example>
    /// <code>
    /// // Simple batch
    /// var updated = await context.Products.Update(p1, p2, p3);
    ///
    /// // Chained accumulation
    /// var all = await context.Products.Update(p1, p2).Update(p3).Execute(ct);
    /// </code>
    /// </example>
    public PendingUpdate<T> Update(params T[] entities)
    {
        return new PendingUpdate<T>(this, entities);
    }

    /// <summary>
    /// Replaces an entity in the collection (PUT semantics — full replacement) and returns it.
    /// The entity's [WeaviateUUID] property is used to identify the object.
    /// </summary>
    /// <param name="entity">The entity to replace.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The replaced entity.</returns>
    public async Task<T> Replace(T entity, CancellationToken cancellationToken = default)
    {
        var id = IdPropertyHelper.GetId(entity);
        if (id == Guid.Empty)
            throw new InvalidOperationException(
                $"Cannot replace entity of type {typeof(T).Name}: UUID property is not set."
            );

        await Collection.Replace(entity, id, cancellationToken).ConfigureAwait(false);
        return entity;
    }

    /// <summary>
    /// Creates a pending batch delete for the given IDs.
    /// Chain additional <c>.Delete()</c> calls to accumulate more IDs,
    /// then call <c>.Execute(ct)</c> to run. Can also be directly awaited.
    /// All accumulated IDs are deleted in a single server call via <c>Filter.UUID.ContainsAny</c>.
    /// </summary>
    /// <param name="ids">The IDs of the entities to delete.</param>
    /// <returns>A pending delete that executes on <c>.Execute(ct)</c> or direct await.</returns>
    /// <example>
    /// <code>
    /// // Simple batch
    /// await context.Products.Delete(id1, id2, id3);
    ///
    /// // Chained accumulation — all deleted in one batch
    /// await context.Products.Delete(id1, id2).Delete(id3).Execute(ct);
    /// </code>
    /// </example>
    public PendingDelete<T> Delete(params Guid[] ids)
    {
        return new PendingDelete<T>(this, ids);
    }

    /// <summary>
    /// Deletes entities matching a filter expression.
    /// </summary>
    /// <param name="filter">The filter expression.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public Task DeleteMany(
        Expression<Func<T, bool>> filter,
        CancellationToken cancellationToken = default
    )
    {
        return Collection.DeleteMany(filter, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Creates a pending batch delete for the given entities.
    /// Chain additional <c>.Delete()</c> calls to accumulate more entities,
    /// then call <c>.Execute(ct)</c> to run. Can also be directly awaited.
    /// All accumulated IDs are deleted in a single server call via <c>Filter.UUID.ContainsAny</c>.
    /// </summary>
    /// <param name="entities">The entities to delete.</param>
    /// <returns>A pending delete that executes on <c>.Execute(ct)</c> or direct await.</returns>
    /// <example>
    /// <code>
    /// // Simple batch
    /// await context.Products.Delete(p1, p2, p3);
    ///
    /// // Chained accumulation — all deleted in one batch
    /// await context.Products.Delete(p1, p2).Delete(p3).Execute(ct);
    /// </code>
    /// </example>
    public PendingDelete<T> Delete(params T[] entities)
    {
        var ids = entities
            .Select(e =>
            {
                var id = IdPropertyHelper.GetId(e);
                if (id == Guid.Empty)
                    throw new InvalidOperationException(
                        $"Cannot delete entity of type {typeof(T).Name}: UUID property is not set."
                    );
                return id;
            })
            .ToArray();
        return new PendingDelete<T>(this, ids);
    }

    /// <summary>
    /// Deletes multiple entities using their UUID properties in a single batch operation.
    /// Uses Filter.UUID.ContainsAny for efficient batch deletion.
    /// </summary>
    /// <param name="entities">The entities to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public Task DeleteMany(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var ids = entities.Select(e => IdPropertyHelper.GetId(e)).ToList();
        if (ids.Any(id => id == Guid.Empty))
        {
            throw new InvalidOperationException(
                "All entities must have a valid UUID for batch delete."
            );
        }

        return Collection.DeleteMany(Filter.UUID.ContainsAny(ids), cancellationToken);
    }

    /// <summary>
    /// Executes a batch delete for the given IDs using <c>Filter.UUID.ContainsAny</c>.
    /// Used internally by <see cref="PendingDelete{T}"/>.
    /// </summary>
    internal Task ExecuteDeleteBatch(Guid[] ids, CancellationToken cancellationToken = default) =>
        ids.Length == 0
            ? Task.CompletedTask
            : Collection.DeleteMany(Filter.UUID.ContainsAny(ids.ToList()), cancellationToken);

    /// <summary>
    /// Executes a batch reference add via <c>ReferenceAddMany</c>.
    /// Used internally by <see cref="PendingReference{T}"/>.
    /// </summary>
    internal Task ExecuteAddReferences(
        DataReference[] refs,
        CancellationToken cancellationToken = default
    ) => Collection.AddReferencesMany(refs, cancellationToken);

    #endregion

    #region Reference Operations

    /// <summary>
    /// Adds a cross-reference link from <paramref name="from"/> to an entity target and returns
    /// a <see cref="PendingReference{T}"/> for optional chaining.
    /// Can be directly awaited or chained with additional <c>.AddReference()</c> calls before
    /// calling <c>.Execute(ct)</c>. All accumulated links are sent in a single batch call.
    /// </summary>
    /// <typeparam name="TTarget">The target entity type.</typeparam>
    /// <param name="from">The source entity (must have a populated <c>[WeaviateUUID]</c> property).</param>
    /// <param name="property">Expression selecting the reference property (e.g., <c>a => a.Category</c>).</param>
    /// <param name="to">The target entity (must have a populated <c>[WeaviateUUID]</c> property).</param>
    /// <returns>A pending reference that executes on <c>.Execute(ct)</c> or direct await.</returns>
    /// <example>
    /// <code>
    /// // Single reference
    /// await context.Articles.AddReference(article, a => a.Category, techCategory);
    ///
    /// // Batched — all links sent in one call
    /// await context.Articles
    ///     .AddReference(article1, a => a.Category, techCategory)
    ///     .AddReference(article2, a => a.Category, scienceCategory)
    ///     .Execute(ct);
    /// </code>
    /// </example>
    public PendingReference<T> AddReference<TTarget>(
        T from,
        Expression<Func<T, object?>> property,
        TTarget to
    )
        where TTarget : class, new()
    {
        var camelName = ManagedPropertyHelper.ToCamelCase(
            ManagedPropertyHelper.GetPropertyName<T, object?>(property)
        );
        var fromId = IdPropertyHelper.GetId(from);
        var toId = IdPropertyHelper.GetId(to);
        return new PendingReference<T>(this, new DataReference(fromId, camelName, [toId]));
    }

    /// <summary>
    /// Adds a cross-reference link from <paramref name="from"/> to a target specified by ID and
    /// returns a <see cref="PendingReference{T}"/> for optional chaining.
    /// </summary>
    /// <param name="from">The source entity (must have a populated <c>[WeaviateUUID]</c> property).</param>
    /// <param name="property">Expression selecting the reference property (e.g., <c>a => a.AuthorId</c>).</param>
    /// <param name="toId">The UUID of the target object.</param>
    /// <returns>A pending reference that executes on <c>.Execute(ct)</c> or direct await.</returns>
    public PendingReference<T> AddReference(
        T from,
        Expression<Func<T, object?>> property,
        Guid toId
    )
    {
        var camelName = ManagedPropertyHelper.ToCamelCase(
            ManagedPropertyHelper.GetPropertyName<T, object?>(property)
        );
        var fromId = IdPropertyHelper.GetId(from);
        return new PendingReference<T>(this, new DataReference(fromId, camelName, [toId]));
    }

    /// <summary>
    /// Replaces all cross-reference links for a property on <paramref name="from"/> with
    /// the specified target IDs. Any existing links for that property are removed.
    /// </summary>
    /// <param name="from">The source entity.</param>
    /// <param name="property">Expression selecting the reference property.</param>
    /// <param name="toIds">The UUIDs of the new target objects.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <example>
    /// <code>
    /// // Replace all Category links with a single new category
    /// await context.Articles.ReplaceReferences(article, a => a.Category, newCategoryId);
    ///
    /// // Replace multi-reference with a new set
    /// await context.Articles.ReplaceReferences(article, a => a.Tags, tag1Id, tag2Id, tag3Id);
    /// </code>
    /// </example>
    public Task ReplaceReferences(
        T from,
        Expression<Func<T, object?>> property,
        CancellationToken cancellationToken = default,
        params Guid[] toIds
    )
    {
        var camelName = ManagedPropertyHelper.ToCamelCase(
            ManagedPropertyHelper.GetPropertyName<T, object?>(property)
        );
        return Collection.ReplaceReferences(
            IdPropertyHelper.GetId(from),
            camelName,
            toIds,
            cancellationToken
        );
    }

    /// <summary>
    /// Replaces all cross-reference links for a property on <paramref name="from"/> with
    /// links to the specified target entities. Any existing links for that property are removed.
    /// </summary>
    /// <typeparam name="TTarget">The target entity type.</typeparam>
    /// <param name="from">The source entity.</param>
    /// <param name="property">Expression selecting the reference property.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="targets">The new target entities.</param>
    public Task ReplaceReferences<TTarget>(
        T from,
        Expression<Func<T, object?>> property,
        CancellationToken cancellationToken = default,
        params TTarget[] targets
    )
        where TTarget : class, new()
    {
        var camelName = ManagedPropertyHelper.ToCamelCase(
            ManagedPropertyHelper.GetPropertyName<T, object?>(property)
        );
        var toIds = targets.Select(t => IdPropertyHelper.GetId(t)).ToArray();
        return Collection.ReplaceReferences(
            IdPropertyHelper.GetId(from),
            camelName,
            toIds,
            cancellationToken
        );
    }

    /// <summary>
    /// Removes a specific cross-reference link from <paramref name="from"/> to a target by ID.
    /// </summary>
    /// <param name="from">The source entity.</param>
    /// <param name="property">Expression selecting the reference property.</param>
    /// <param name="toId">The UUID of the target object to unlink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <example>
    /// <code>
    /// await context.Articles.DeleteReference(article, a => a.Category, oldCategoryId);
    /// </code>
    /// </example>
    public Task DeleteReference(
        T from,
        Expression<Func<T, object?>> property,
        Guid toId,
        CancellationToken cancellationToken = default
    )
    {
        var camelName = ManagedPropertyHelper.ToCamelCase(
            ManagedPropertyHelper.GetPropertyName<T, object?>(property)
        );
        return Collection.DeleteReference(
            IdPropertyHelper.GetId(from),
            camelName,
            toId,
            cancellationToken
        );
    }

    /// <summary>
    /// Removes a specific cross-reference link from <paramref name="from"/> to a target entity.
    /// </summary>
    /// <typeparam name="TTarget">The target entity type.</typeparam>
    /// <param name="from">The source entity.</param>
    /// <param name="property">Expression selecting the reference property.</param>
    /// <param name="target">The target entity to unlink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeleteReference<TTarget>(
        T from,
        Expression<Func<T, object?>> property,
        TTarget target,
        CancellationToken cancellationToken = default
    )
        where TTarget : class, new() =>
        DeleteReference(from, property, IdPropertyHelper.GetId(target), cancellationToken);

    #endregion

    #region Query Operations

    /// <summary>
    /// Creates a query for this collection.
    /// </summary>
    /// <returns>A query client for building queries.</returns>
    public CollectionMapperQueryClient<T> Query()
    {
        return Collection.Query();
    }

    /// <summary>
    /// Creates a projected query for this collection that maps results to TProjection.
    /// This is syntactic sugar for <c>Query().Project&lt;TProjection&gt;()</c>.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to map results to.</typeparam>
    /// <returns>A projected query client for building queries.</returns>
    /// <example>
    /// <code>
    /// var summaries = await context.Products.Query&lt;ProductSummary&gt;()
    ///     .Where(p => p.Price > 10)
    ///     .Execute();
    /// </code>
    /// </example>
    public ProjectedQueryClient<T, TProjection> Query<TProjection>()
        where TProjection : class, new()
    {
        return Collection.Query().Project<TProjection>();
    }

    /// <summary>
    /// Creates a projected query for this collection that maps results to TProjection.
    /// </summary>
    /// <typeparam name="TProjection">The projection type to map results to.</typeparam>
    /// <returns>A projected query client for building queries.</returns>
    /// <remarks>
    /// <b>Prefer using <c>Query&lt;TProjection&gt;()</c> directly</b> instead of <c>Project&lt;TProjection&gt;()</c>.
    /// This method is kept public for advanced scenarios where you need to conditionally apply projections.
    /// </remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public ProjectedQueryClient<T, TProjection> Project<TProjection>()
        where TProjection : class, new()
    {
        return Collection.Query().Project<TProjection>();
    }

    /// <summary>
    /// Finds an entity by its ID.
    /// </summary>
    /// <param name="id">The entity ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity if found, null otherwise.</returns>
    public async Task<T?> Find(Guid id, CancellationToken cancellationToken = default)
    {
        // Use fetch by ID through the typed query client
        var typedQuery = new Weaviate.Client.Typed.TypedQueryClient<T>(Collection.Inner.Query);
        var result = await typedQuery
            .FetchObjectByID(id, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result == null)
            return null;

        // Map the result back to T
        return Mapping.ManagedObjectMapper.FromWeaviateObject(result);
    }

    #endregion

    #region Aggregate Operations

    /// <summary>
    /// Creates an aggregation query for this collection.
    /// </summary>
    /// <returns>An aggregate starter for building aggregations.</returns>
    public AggregateStarter<T> Aggregate()
    {
        return Collection.Aggregate;
    }

    #endregion

    #region Count and Iterator Operations

    /// <summary>
    /// Returns the total count of objects in this collection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of objects in the collection.</returns>
    public Task<ulong> Count(CancellationToken cancellationToken = default)
    {
        return Collection.Count(cancellationToken);
    }

    /// <summary>
    /// Iterates over all objects in the collection, returning strongly-typed mapped entities.
    /// Uses cursor-based pagination internally for efficient iteration over large collections.
    /// </summary>
    /// <param name="after">Start iteration after this object ID (for resuming).</param>
    /// <param name="cacheSize">Number of objects to fetch per page (default 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of mapped entities.</returns>
    public IAsyncEnumerable<T> Iterator(
        Guid? after = null,
        uint cacheSize = 100,
        CancellationToken cancellationToken = default
    )
    {
        return Collection.Iterator(after, cacheSize, cancellationToken);
    }

    #endregion

    #region Migration Operations

    /// <summary>
    /// Checks what schema changes would be needed to align the collection with the type definition.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration plan.</returns>
    public Task<Migrations.MigrationPlan> CheckMigrate(
        CancellationToken cancellationToken = default
    )
    {
        return Collection.CheckMigrate(cancellationToken);
    }

    /// <summary>
    /// Migrates the collection schema to match the type definition.
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
        return Collection.Migrate(checkFirst, allowBreakingChanges, cancellationToken);
    }

    #endregion
}
