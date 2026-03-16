using System.Reflection;
using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Managed.Internal;
using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// A pending cross-collection batch insert that automatically:
/// <list type="bullet">
///   <item>Assigns UUIDs client-side to all entities without one, so references between
///   entities in the batch resolve correctly regardless of insertion order.</item>
///   <item>Inserts all objects first (without reference properties).</item>
///   <item>Adds all cross-references in a second phase via the batch references API,
///   which handles two-way (circular) references without deadlock.</item>
///   <item>Orders object insertions by dependency as a best-effort optimisation
///   (circular dependencies are silently accepted rather than throwing).</item>
/// </list>
/// </summary>
/// <remarks>
/// Created via <see cref="WeaviateContext.Batch"/>. For same-collection ordering
/// (self-referential inserts), use <see cref="PendingInsert{T}"/> chaining instead.
/// </remarks>
/// <example>
/// <code>
/// // Author is inserted before Book (detected via [Reference] on Book).
/// // The mutual reference (Author.Books) is handled via AddReference after both are inserted.
/// await context.Batch()
///     .Insert(book1, book2)
///     .Insert(author)
///     .Execute(ct);
/// </code>
/// </example>
public sealed class PendingBatch
{
    private readonly WeaviateContext _context;
    private readonly List<(Type EntityType, object[] Entities)> _batches = [];

    internal PendingBatch(WeaviateContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Adds entities to the batch. The collection is inferred from the
    /// [WeaviateCollection] attribute on <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="entities">The entities to insert.</param>
    /// <returns>This instance for chaining.</returns>
    public PendingBatch Insert<T>(params T[] entities)
        where T : class, new()
    {
        if (entities.Length > 0)
            _batches.Add((typeof(T), entities.Cast<object>().ToArray()));
        return this;
    }

    /// <summary>
    /// Executes the batch. When no entities in the batch declare any
    /// <c>[Reference]</c> properties, objects are inserted directly via a single
    /// <c>InsertMany</c> call per type (fast path). When references are present,
    /// the full two-phase approach is used:
    /// <list type="number">
    ///   <item>Pre-assign UUIDs, then insert all objects (without references).</item>
    ///   <item>Add all cross-references via the batch references API.</item>
    /// </list>
    /// IDs are assigned back to entities' [WeaviateUUID] properties.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (_batches.Count == 0)
            return;

        // Merge entities per type (preserving insertion order within each type)
        var byType = new Dictionary<Type, List<object>>();
        foreach (var (type, entities) in _batches)
        {
            if (!byType.TryGetValue(type, out var list))
                byType[type] = list = [];
            list.AddRange(entities);
        }

        // Fast path: no [Reference] properties on any type — skip everything reference-related.
        if (!HasAnyReferences(byType.Keys))
        {
            foreach (var (type, entities) in byType)
                await ExecuteInserts(type, entities, cancellationToken).ConfigureAwait(false);
            return;
        }

        var types = byType.Keys.ToList();
        var (orderedTypes, hasCycles) = TopologicalSortTypes(types);

        if (!hasCycles)
        {
            // One-way refs only: insert in dependency order with references included.
            // Referenced objects are already inserted (and have IDs) by the time
            // referencing objects are serialized.
            foreach (var type in orderedTypes)
            {
                if (byType.TryGetValue(type, out var entities))
                    await ExecuteInserts(type, entities, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        // Two-way / circular refs: full four-phase approach.

        // Phase 1: Pre-assign UUIDs so references can be resolved across all entities
        // before any insert is executed.
        AssignMissingIds(byType);

        // Phase 2: Collect all reference operations (now that every entity has an ID).
        var deferredRefs = CollectAllReferences(byType);

        // Phase 3: Insert all objects without reference properties.
        // orderedTypes already includes remaining (cyclic) types appended in any order.
        foreach (var type in orderedTypes)
        {
            if (byType.TryGetValue(type, out var entities))
                await ExecuteInsertsNoRefs(type, entities, cancellationToken).ConfigureAwait(false);
        }

        // Phase 4: Add all cross-references in batch.
        foreach (var (type, refs) in deferredRefs)
        {
            if (refs.Count > 0)
                await ExecuteAddReferences(type, refs, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Enables direct <c>await</c> without an explicit <see cref="Execute"/> call,
    /// using the default <see cref="CancellationToken"/>.
    /// </summary>
    public TaskAwaiter GetAwaiter() => Execute().GetAwaiter();

    // ── Private helpers ──────────────────────────────────────────────────────

    private static void AssignMissingIds(Dictionary<Type, List<object>> byType)
    {
        foreach (var (type, entities) in byType)
        {
            var idProp = IdPropertyHelper.GetIdProperty(type);
            if (idProp == null)
                continue;

            foreach (var entity in entities)
            {
                if ((Guid)(idProp.GetValue(entity) ?? Guid.Empty) == Guid.Empty)
                    idProp.SetValue(entity, Guid.NewGuid());
            }
        }
    }

    private static bool HasAnyReferences(IEnumerable<Type> types) =>
        types.Any(type =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(p => p.GetCustomAttribute<ReferenceAttribute>() != null)
        );

    private async Task ExecuteInserts(
        Type entityType,
        List<object> entities,
        CancellationToken cancellationToken
    )
    {
        if (entities.Count == 0)
            return;

        var collection = GetCollection(entityType);
        var response = await collection
            .InsertMany(entities, cancellationToken)
            .ConfigureAwait(false);

        // Assign server-generated UUIDs back to entities
        foreach (var entry in response.Objects)
        {
            if (entry.Index < 0 || entry.Index >= entities.Count || !entry.UUID.HasValue)
                continue;

            var entity = entities[entry.Index];
            var idProp = IdPropertyHelper.GetIdProperty(entity.GetType());
            if (idProp == null || !idProp.CanWrite)
                continue;

            if (idProp.PropertyType == typeof(Guid))
                idProp.SetValue(entity, entry.UUID.Value);
            else if (idProp.PropertyType == typeof(Guid?))
                idProp.SetValue(entity, (Guid?)entry.UUID.Value);
        }
    }

    private Dictionary<Type, List<DataReference>> CollectAllReferences(
        Dictionary<Type, List<object>> byType
    )
    {
        var result = new Dictionary<Type, List<DataReference>>();

        foreach (var (type, entities) in byType)
        {
            var collection = GetCollection(type);
            var refs = collection.CollectReferences(entities);

            if (refs.Length > 0)
                result[type] = refs.ToList();
        }

        return result;
    }

    private async Task ExecuteInsertsNoRefs(
        Type entityType,
        List<object> entities,
        CancellationToken cancellationToken
    )
    {
        if (entities.Count == 0)
            return;

        var collection = GetCollection(entityType);
        await collection.InsertManyNoRefs(entities, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteAddReferences(
        Type entityType,
        List<DataReference> refs,
        CancellationToken cancellationToken
    )
    {
        var collection = GetCollection(entityType);
        await collection.AddReferences(refs.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private IManagedCollectionBatchTarget GetCollection(Type entityType)
    {
        var getCollectionMethod = typeof(WeaviateContext)
            .GetMethod(
                nameof(WeaviateContext.GetManagedCollection),
                BindingFlags.NonPublic | BindingFlags.Instance
            )!
            .MakeGenericMethod(entityType);
        return (IManagedCollectionBatchTarget)getCollectionMethod.Invoke(_context, null)!;
    }

    private (List<Type> Ordered, bool HasCycles) TopologicalSortTypes(List<Type> types)
    {
        var dependencies = new Dictionary<Type, HashSet<Type>>();
        foreach (var type in types)
            dependencies[type] = GetTypeDependencies(type, types);

        var result = new List<Type>();
        var noIncoming = new Queue<Type>(
            types.Where(t => !dependencies.Values.Any(d => d.Contains(t)))
        );

        while (noIncoming.Count > 0)
        {
            var type = noIncoming.Dequeue();
            result.Add(type);

            foreach (var dependent in types.Where(t => dependencies[t].Contains(type)))
            {
                dependencies[dependent].Remove(type);
                if (dependencies[dependent].Count == 0 && !result.Contains(dependent))
                    noIncoming.Enqueue(dependent);
            }
        }

        var hasCycles = result.Count != types.Count;

        // Append remaining (cyclic) types in any order so the caller always gets
        // a complete list regardless of which path it takes.
        if (hasCycles)
            result.AddRange(types.Except(result));

        return (result, hasCycles);
    }

    private static HashSet<Type> GetTypeDependencies(Type type, List<Type> availableTypes)
    {
        var dependencies = new HashSet<Type>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<ReferenceAttribute>() == null)
                continue;

            var refType = property.PropertyType;
            if (refType.IsGenericType && refType.GetGenericTypeDefinition() == typeof(List<>))
                refType = refType.GetGenericArguments()[0];

            if (availableTypes.Contains(refType))
                dependencies.Add(refType);
        }

        return dependencies;
    }
}
