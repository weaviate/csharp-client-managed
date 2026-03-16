# API Reference

Complete API surface for Weaviate.Client.Managed.

## Table of Contents

- [WeaviateContext](#weaviatecontext)
- [CollectionSet\<T\>](#collectionsett)
- [ManagedCollection\<T\>](#managedcollectiont)
- [CollectionMapperQueryClient\<T\>](#collectionmapperqueryclientt)
- [GenerativeQueryExecutor\<T\>](#generativequeryexecutort)
- [WeaviateAdmin](#weaviateadmin)
- [PendingInsert\<T\>](#pendinginsertt)
- [PendingDelete\<T\>](#pendingdeletet)
- [PendingUpdate\<T\>](#pendingupdatet)
- [PendingReference\<T\>](#pendingreferencet)
- [AggregateStarter\<T\>](#aggregatestartert)
- [Extension Methods](#extension-methods)
- [Models](#models)

---

## WeaviateContext

EF Core-like base class for managing collections. Derive from this class and declare `CollectionSet<T>` properties.

### Defining a Context

```csharp
public class BookstoreContext : WeaviateContext
{
    public BookstoreContext(WeaviateClient client) : base(client) { }

    public CollectionSet<Book> Books { get; set; } = null!;
    public CollectionSet<Author> Authors { get; set; } = null!;
}
```

### Properties

| Property | Type | Description |
| -------- | ---- | ----------- |
| `Client` | `WeaviateClient` | The underlying Weaviate client |
| `Admin` | `WeaviateAdmin` | Administrative operations (backup, RBAC, cluster, health) |
| `Tenant` | `string?` | Current tenant scope (null if unscoped) |
| `ConsistencyLevel` | `ConsistencyLevels?` | Current consistency level (null if default) |

### Data Operations

#### Insert\<T\> (single)

```csharp
Task<T> Insert<T>(T entity, CancellationToken ct = default) where T : class, new()
```

Inserts a single entity and returns it with its `[WeaviateUUID]` property populated.

```csharp
// Capture the returned entity to get its generated ID
var book = await context.Insert(new Book { Title = "Foundation" });
Console.WriteLine(book.Id); // Guid assigned by Weaviate
```

#### Insert\<T\> (batch)

```csharp
PendingInsert<T> Insert<T>(params T[] entities) where T : class, new()
```

Creates a pending batch insert. IDs are assigned back to `[WeaviateUUID]` properties in-place when executed. Directly awaitable or chainable. See [PendingInsert\<T\>](#pendinginsertt).

#### Update\<T\> (single)

```csharp
Task<T> Update<T>(T entity, CancellationToken ct = default) where T : class, new()
```

Updates a single entity (PATCH semantics) and returns it. Uses the entity's `[WeaviateUUID]` property.

#### Update\<T\> (batch)

```csharp
PendingUpdate<T> Update<T>(params T[] entities) where T : class, new()
```

Updates multiple entities using their `[WeaviateUUID]` properties (PATCH semantics). Directly awaitable. See [PendingUpdate\<T\>](#pendingupdatet).

#### Delete\<T\> (by ID)

```csharp
PendingDelete<T> Delete<T>(params Guid[] ids) where T : class, new()
```

Deletes entities by ID. Directly awaitable or chainable. See [PendingDelete\<T\>](#pendingdeletet).

#### Delete\<T\> (by entity)

```csharp
PendingDelete<T> Delete<T>(params T[] entities) where T : class, new()
```

Deletes entities using their `[WeaviateUUID]` property. Directly awaitable or chainable.

#### DeleteMany\<T\>

```csharp
Task DeleteMany<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class, new()
```

Batch deletes entities using `Filter.UUID.ContainsAny`.

#### AddReference\<T, TRef\>

```csharp
PendingReference<T> AddReference<T, TRef>(T entity,
    Expression<Func<T, object?>> property, TRef target,
    CancellationToken ct = default) where T : class, new()
```

Adds a cross-reference link from `entity` to `target`. Directly awaitable or chainable. See [PendingReference\<T\>](#pendingreferencet).

```csharp
await context.AddReference<Article, Category>(article, a => a.Category, techCategory);
```

#### Aggregate\<TProjection\>

```csharp
ContextAggregateBuilder<TProjection> Aggregate<TProjection>() where TProjection : class, new()
```

Creates an aggregation builder where the collection is inferred from the `[QueryAggregate<T>]` attribute on the projection type. Uses raw `Filter` for filtering (since the model type is only known at runtime). `.Execute()` is optional - the builder is directly awaitable.

```csharp
// Execute() is optional
var stats = await context.Aggregate<ProductStats>();

// With grouping - returns GroupedContextAggregateBuilder<TProjection>
var grouped = await context.Aggregate<ProductStats>()
    .GroupBy("category");
```

### Query Operations

#### Query\<T\>

```csharp
CollectionMapperQueryClient<T> Query<T>() where T : class, new()
```

Creates a query builder for the specified type.

```csharp
var results = await context.Query<Book>().Where(b => b.Price > 10).Execute();
```

#### Count\<T\>

```csharp
Task<ulong> Count<T>(CancellationToken ct = default) where T : class, new()
```

Returns the total number of objects in the collection.

```csharp
var bookCount = await context.Count<Book>();
```

#### Iterator\<T\>

```csharp
IAsyncEnumerable<T> Iterator<T>(Guid? after = null, uint cacheSize = 100,
    CancellationToken ct = default) where T : class, new()
```

Iterates over all objects using cursor-based pagination.

```csharp
await foreach (var book in context.Iterator<Book>())
{
    Console.WriteLine(book.Title);
}
```

### Collection Access

#### Set\<T\>

```csharp
CollectionSet<T> Set<T>() where T : class, new()
```

Gets the `CollectionSet<T>` for a type. If the type is declared as a property on the context, returns that instance; otherwise creates a new one.

### Migration Operations

#### GetPendingMigrations

```csharp
Task<Dictionary<string, MigrationPlan>> GetPendingMigrations(CancellationToken ct = default)
```

Returns migration plans for all collections in the context. Also detects orphaned collections (server collections not registered in the context).

#### Migrate

```csharp
Task Migrate(bool allowBreakingChanges = false, bool destructive = false, CancellationToken ct = default)
```

Migrates all collections to match their type definitions. When `destructive: true`, also deletes orphaned server collections not registered in the context.

### Scoping

#### ForTenant

```csharp
WeaviateContext ForTenant(string tenant)
```

Returns a **new context** scoped to the specified tenant. The original context is unchanged. The returned context is the same derived type with independent `CollectionSet<T>` instances.

```csharp
var acmeContext = (BookstoreContext)context.ForTenant("acme");
await acmeContext.Books.Insert(new Book { Title = "Acme Guide" });
```

#### WithConsistencyLevel

```csharp
WeaviateContext WithConsistencyLevel(ConsistencyLevels level)
```

Returns a **new context** with the specified consistency level.

```csharp
var quorumContext = context.WithConsistencyLevel(ConsistencyLevels.Quorum);
```

### Configuration

#### OnConfiguring

```csharp
protected virtual void OnConfiguring(WeaviateContextOptionsBuilder options)
```

Override to configure context options.

---

## CollectionSet\<T\>

Per-collection facade, similar to EF Core's `DbSet<T>`. Provides all CRUD, query, aggregate, count, iterator, and migration operations for a single entity type.

### Properties

| Property | Type | Description |
| -------- | ---- | ----------- |
| `CollectionName` | `string` | The Weaviate collection name |

### Data Operations

#### Insert (single)

```csharp
Task<T> Insert(T entity, CancellationToken ct = default)
```

Inserts a single entity and returns it with its `[WeaviateUUID]` property populated.

#### Insert (batch)

```csharp
PendingInsert<T> Insert(params T[] entities)
```

Creates a pending batch insert. Can be directly awaited or chained. See [PendingInsert\<T\>](#pendinginsertt).
Throws `BatchInsertException<T>` on failure.

#### Update (single)

```csharp
Task<T> Update(T entity, CancellationToken ct = default)
```

Updates a single entity (PATCH semantics) and returns it. Uses the entity's `[WeaviateUUID]` property.

#### Update (batch)

```csharp
PendingUpdate<T> Update(params T[] entities)
```

Updates multiple entities using their `[WeaviateUUID]` properties (PATCH semantics). Directly awaitable. See [PendingUpdate\<T\>](#pendingupdatet).

#### Replace

```csharp
Task<T> Replace(T entity, CancellationToken ct = default)
```

Replaces an entity entirely (PUT semantics) and returns it. Uses the entity's `[WeaviateUUID]`.

#### Delete (by ID)

```csharp
PendingDelete<T> Delete(params Guid[] ids)
```

Deletes entities by ID. Directly awaitable or chainable. See [PendingDelete\<T\>](#pendingdeletet).

#### Delete (by entity)

```csharp
PendingDelete<T> Delete(params T[] entities)
```

Deletes entities using their `[WeaviateUUID]` property. Directly awaitable or chainable.

#### DeleteMany (by entities)

```csharp
Task DeleteMany(IEnumerable<T> entities, CancellationToken ct = default)
```

Batch deletes entities using `Filter.UUID.ContainsAny`.

#### DeleteMany (by filter)

```csharp
Task DeleteMany(Expression<Func<T, bool>> filter, CancellationToken ct = default)
```

Deletes entities matching a filter expression.

### Reference Operations

#### AddReference

```csharp
PendingReference<T> AddReference<TRef>(T entity,
    Expression<Func<T, object?>> property, TRef target)
```

Adds a cross-reference link from `entity.property` to `target` (entity or `Guid`). Directly awaitable or chainable. See [PendingReference\<T\>](#pendingreferencet).

```csharp
// Directly awaitable
await context.Articles.AddReference(article, a => a.Category, techCategory);

// Chained — sent in one call
await context.Articles
    .AddReference(article1, a => a.Category, techCategory)
    .AddReference(article2, a => a.Category, scienceCategory)
    .Execute(cancellationToken);
```

#### ReplaceReferences

```csharp
Task ReplaceReferences<TRef>(T entity,
    Expression<Func<T, object?>> property, params TRef[] targets)
```

Replaces all values of the reference property with the supplied targets (entities or `Guid`s).

```csharp
await context.Articles.ReplaceReferences(article, a => a.Category, newCategoryId);
await context.Articles.ReplaceReferences(article, a => a.Tags, tag1Id, tag2Id, tag3Id);
```

#### DeleteReference

```csharp
Task DeleteReference<TRef>(T entity,
    Expression<Func<T, object?>> property, TRef target)
```

Removes a specific cross-reference link.

```csharp
await context.Articles.DeleteReference(article, a => a.Category, oldCategoryId);
await context.Articles.DeleteReference(article, a => a.Category, oldCategory);
```

### Query Operations

#### Query

```csharp
CollectionMapperQueryClient<T> Query()
```

Creates a query builder.

#### Project\<TProjection\>

```csharp
ProjectedQueryClient<T, TProjection> Project<TProjection>() where TProjection : class, new()
```

Creates a projected query that maps results to `TProjection`.

#### Find

```csharp
Task<T?> Find(Guid id, CancellationToken ct = default)
```

Finds an entity by ID. Returns null if not found.

```csharp
var book = await context.Books.Find(bookId);
```

### Aggregate Operations

#### Aggregate

```csharp
AggregateStarter<T> Aggregate()
```

Creates an aggregation builder. All aggregate operations support optional `.Execute()` - builders are directly awaitable.

```csharp
// Execute() is optional
var stats = await products.Aggregate.WithMetrics<ProductStats>();

// Grouped aggregation - returns GroupedAggregateBuilder<T, TResult>
var grouped = await products.Aggregate
    .WithMetrics<ProductStats>()
    .GroupBy(p => p.Category);
```

### Count and Iterator

#### Count

```csharp
Task<ulong> Count(CancellationToken ct = default)
```

Returns the total number of objects in the collection.

#### Iterator

```csharp
IAsyncEnumerable<T> Iterator(Guid? after = null, uint cacheSize = 100,
    CancellationToken ct = default)
```

Iterates over all objects using cursor-based pagination.

### Migration Operations

#### CheckMigrate

```csharp
Task<MigrationPlan> CheckMigrate(CancellationToken ct = default)
```

Dry-run migration check.

#### Migrate

```csharp
Task<MigrationPlan> Migrate(bool checkFirst = true, bool allowBreakingChanges = false,
    CancellationToken ct = default)
```

Migrates the collection schema.

---

## ManagedCollection\<T\>

Lower-level type-safe wrapper around `CollectionClient`. Use this when you don't need a full context.

### Properties

| Property | Type | Description |
| -------- | ---- | ----------- |
| `Inner` | `CollectionClient` | Underlying Weaviate collection client |
| `Name` | `string` | Collection name |
| `Tenant` | `string?` | Current tenant scope |

### Data Operations

#### Insert

```csharp
Task<Guid> Insert(T obj, Guid? id = null, CancellationToken ct = default)
```

Returns the UUID of the inserted object.

#### InsertMany

```csharp
Task<BatchInsertResponse> InsertMany(IEnumerable<T> objects, CancellationToken ct = default)
```

Low-level batch insert returning the core `BatchInsertResponse`.

#### Replace

```csharp
Task Replace(T obj, Guid id, CancellationToken ct = default)
```

Full replacement (PUT).

#### Update

```csharp
Task Update(T obj, Guid id, CancellationToken ct = default)
```

Partial update (PATCH).

#### Delete

```csharp
Task Delete(Guid id, CancellationToken ct = default)
```

#### DeleteMany

```csharp
Task<DeleteManyResult> DeleteMany(Expression<Func<T, bool>> where,
    bool dryRun = false, bool verbose = false, CancellationToken ct = default)
```

### Query and Aggregate

| Property | Type | Description |
| -------- | ---- | ----------- |
| `Query` | `CollectionMapperQueryClient<T>` | Query builder |
| `Aggregate` | `AggregateStarter<T>` | Aggregate builder |

### Collection Operations

#### Count

```csharp
Task<ulong> Count(CancellationToken ct = default)
```

#### Iterator

```csharp
IAsyncEnumerable<T> Iterator(Guid? after = null, uint cacheSize = 100,
    CancellationToken ct = default)
```

Cursor-based iteration over all objects.

#### Exists

```csharp
Task<bool> Exists(CancellationToken ct = default)
```

#### DeleteCollection

```csharp
Task DeleteCollection(CancellationToken ct = default)
```

#### ExportConfig

```csharp
Task<CollectionConfigExport?> ExportConfig(CancellationToken ct = default)
```

### Scoping

#### WithTenant

```csharp
ManagedCollection<T> WithTenant(string tenant)
```

#### WithConsistencyLevel

```csharp
ManagedCollection<T> WithConsistencyLevel(ConsistencyLevels level)
```

### Migration Operations

#### CheckMigrate / Migrate

Same signatures as `CollectionSet<T>`.

---

## CollectionMapperQueryClient\<T\>

Fluent query builder with LINQ-style methods.

### Filter Methods

#### Where

```csharp
CollectionMapperQueryClient<T> Where(Expression<Func<T, bool>> predicate)
```

Multiple calls are combined with AND.

**Supported operators:**

- Comparison: `==`, `!=`, `>`, `<`, `>=`, `<=`
- Logical: `&&`, `||`, `!`
- String: `.Contains()`, `.StartsWith()`, `.EndsWith()`
- Null: `== null`, `!= null`
- Collections: `.Contains()` on arrays
- Geo range: `.IsWithinGeoRange(lat, lon, distanceMetres)` or `.IsWithinGeoRange(GeoCoordinateConstraint)` on `GeoCoordinate` properties

### Vector Search Methods

#### NearText

```csharp
CollectionMapperQueryClient<T> NearText(string text,
    Expression<Func<T, object>>? vector = null,
    float? certainty = null, float? distance = null)
```

Semantic search via text-to-vector conversion.

#### NearVector

```csharp
CollectionMapperQueryClient<T> NearVector(float[] vectorValues,
    Expression<Func<T, object>>? vector = null,
    float? certainty = null, float? distance = null)
```

Search with a provided vector.

#### NearObject

```csharp
CollectionMapperQueryClient<T> NearObject(Guid objectId,
    Expression<Func<T, object>>? vector = null,
    float? certainty = null, float? distance = null)
```

Search using an existing object's vector.

#### Hybrid

```csharp
CollectionMapperQueryClient<T> Hybrid(string query,
    Expression<Func<T, object>>? vector = null,
    float? alpha = null, HybridFusion? fusionType = null,
    float? maxVectorDistance = null)
```

Combined BM25 + vector search. `alpha` controls balance (0 = keyword, 1 = vector).

#### BM25

```csharp
CollectionMapperQueryClient<T> BM25(string query,
    Expression<Func<T, object>>[]? searchFields = null,
    BM25Operator? searchOperator = null)
```

Keyword search. Optionally restrict to specific fields.

```csharp
.BM25("wireless mouse")
.BM25("wireless", searchFields: [p => p.Name], searchOperator: BM25Operator.And)
```

#### NearMedia

```csharp
CollectionMapperQueryClient<T> NearMedia(NearMediaInput.FactoryFn media)
```

Multi-modal search (image, video, audio).

```csharp
.NearMedia(m => m.Image(imageBytes).Build())
```

#### Targets

```csharp
CollectionMapperQueryClient<T> Targets(Func<TargetVectorBuilder<T>, TargetVectorBuilder<T>> configure)
```

Configures multi-vector targeting for collections with multiple named vectors. Works with `NearText`, `NearVector`, `NearObject`, and `Hybrid`.

**Combination Methods:**

- `Sum()` — Add vector distances
- `Average()` — Average vector distances
- `Minimum()` — Use closest distance (best match wins)
- `ManualWeights()` — Apply explicit weights to each vector
- `RelativeScore()` — Score-based weighting

```csharp
// Named vector combination (NearText/NearObject/Hybrid)
var results = await products.Query()
    .NearText("wireless mouse")
    .Targets(t => t.Sum(p => p.TextEmbedding, p => p.ImageEmbedding))
    .Execute();

// With weights
var results = await products.Query()
    .NearText("laptop")
    .Targets(t => t.ManualWeights(
        (p => p.TextEmbedding, 0.7),
        (p => p.ImageEmbedding, 0.3)
    ))
    .Execute();

// Per-target vectors (NearVector)
var textVec = new float[] { 0.1f, 0.2f };
var imageVec = new float[] { 0.3f, 0.4f };

var results = await products.Query()
    .NearVector()
    .Targets(t => t.Sum(
        (p => p.TextEmbedding, textVec),
        (p => p.ImageEmbedding, imageVec)
    ))
    .Execute();
```

### Result Control Methods

#### Limit / Offset / AutoLimit / After

```csharp
CollectionMapperQueryClient<T> Limit(uint limit)
CollectionMapperQueryClient<T> Offset(uint offset)
CollectionMapperQueryClient<T> AutoLimit(uint autoLimit)
CollectionMapperQueryClient<T> After(Guid cursor)
```

#### OrderBy / OrderByDescending / ThenBy / ThenByDescending

```csharp
CollectionMapperQueryClient<T> OrderBy<TProp>(Expression<Func<T, TProp>> property)
CollectionMapperQueryClient<T> OrderByDescending<TProp>(Expression<Func<T, TProp>> property)
CollectionMapperQueryClient<T> ThenBy<TProp>(Expression<Func<T, TProp>> property)
CollectionMapperQueryClient<T> ThenByDescending<TProp>(Expression<Func<T, TProp>> property)
```

Preferred chaining API for multi-column sort.

```csharp
.OrderBy(p => p.Category)
.ThenByDescending(p => p.Price)
```

#### Sort

```csharp
CollectionMapperQueryClient<T> Sort<TProp>(
    Expression<Func<T, TProp>> property, bool descending = false)
```

Low-level single-column sort. `OrderBy`/`ThenBy` are preferred for chaining.

#### Rerank

```csharp
CollectionMapperQueryClient<T> Rerank(Rerank rerank)
CollectionMapperQueryClient<T> Rerank(string property, string? query = null)
```

Re-ranks results using the collection's configured reranker module. The string overload specifies which property the reranker should score on, and an optional separate rerank query.

```csharp
// Rerank on property with explicit query
.Rerank("name", "wireless headphones")

// Rerank on property using the search query
.Rerank("description")

// Full control
.Rerank(new Rerank { Property = "name", Query = "gaming mouse" })
```

### Include Methods

#### WithVectors

```csharp
CollectionMapperQueryClient<T> WithVectors(params Expression<Func<T, object>>[] vectors)
```

#### WithReferences

```csharp
CollectionMapperQueryClient<T> WithReferences(params Expression<Func<T, object>>[] references)
```

#### Select

```csharp
CollectionMapperQueryClient<T> Select(Expression<Func<T, object>> selector)
```

Return only specific properties.

#### Project\<TProjection\> (query builder)

```csharp
ProjectedQueryClient<T, TProjection> Project<TProjection>() where TProjection : class, new()
```

Project results to a different type. See [Query Projections](#query-projections).

#### WithMetadata

```csharp
CollectionMapperQueryClient<T> WithMetadata(MetadataQuery metadata)
```

### Execution Methods

#### Execute

```csharp
Task<IEnumerable<QueryResult<T>>> Execute(CancellationToken ct = default)
```

Returns results wrapped in `QueryResult<T>` with `.Object`, `.Id`, and `.Metadata`.

```csharp
var results = await collection.Query().NearText("widget").Execute();

foreach (var result in results)
    Console.WriteLine($"{result.Object.Name}: {result.Metadata?.Score}");

// Extract just objects
var objects = results.Objects();
```

#### Generate

```csharp
GenerativeQueryExecutor<T> Generate(
    SinglePrompt? singlePrompt = null,
    GroupedTask? groupedTask = null,
    GenerativeProvider? provider = null)
```

Switches to generative (RAG) mode. See [GenerativeQueryExecutor](#generativequeryexecutort).

---

## GenerativeQueryExecutor\<T\>

Fluent builder for generative (RAG) queries. Returned by `.Generate()` on the query builder.

### Methods

#### SinglePrompt

```csharp
GenerativeQueryExecutor<T> SinglePrompt(string prompt)
```

Per-object generation. Supports `{propertyName}` template substitution.

#### GroupedTask

```csharp
GenerativeQueryExecutor<T> GroupedTask(string task, params string[] properties)
```

Result-set level generation. All results are provided as context.

#### WithProvider

```csharp
GenerativeQueryExecutor<T> WithProvider(GenerativeProvider provider)
```

Override the collection's configured generative module.

#### Execute

```csharp
Task<GenerativeQueryResponse<T>> Execute(CancellationToken ct = default)
```

Returns a `GenerativeQueryResponse<T>` containing per-object and grouped generative results.

### Usage

```csharp
// Per-object generation
var results = await products.Query
    .NearText("wireless mouse")
    .Limit(5)
    .Generate(singlePrompt: "Describe this product")
    .Execute();

foreach (var r in results)
    Console.WriteLine($"{r.Object.Name}: {r.Generative?[0]}");

// Grouped generation
var grouped = await products.Query
    .NearText("wireless mouse")
    .Limit(5)
    .Generate()
    .GroupedTask("Compare these products")
    .Execute();

Console.WriteLine(grouped.Generative?[0]);

// Both prompts
var both = await products.Query
    .NearText("wireless mouse")
    .Limit(5)
    .Generate(singlePrompt: "Summarize: {name}")
    .GroupedTask("Which is best?")
    .Execute();
```

---

## WeaviateAdmin

Administrative operations facade. Accessed via `context.Admin`.

### Properties

| Property | Type | Description |
| -------- | ---- | ----------- |
| `Backup` | `BackupClient` | Backup and restore operations |
| `Users` | `UsersClient` | User management |
| `Roles` | `RolesClient` | Role management and permissions |
| `Groups` | `GroupsClient` | OIDC group management |
| `Cluster` | `ClusterClient` | Cluster and replication |
| `Aliases` | `AliasClient` | Collection alias management |

### Methods

```csharp
Task<bool> IsLive(CancellationToken ct = default)
Task<bool> IsReady(CancellationToken ct = default)
Task<bool> WaitUntilReady(TimeSpan timeout, TimeSpan? pollInterval = null, CancellationToken ct = default)
Task<MetaInfo> GetMeta(CancellationToken ct = default)
```

### Usage

```csharp
// Health check
var ready = await context.Admin.IsReady();

// Server metadata
var meta = await context.Admin.GetMeta();
Console.WriteLine($"Weaviate {meta.Version}");

// Backup
await context.Admin.Backup.CreateSync(backupRequest);

// RBAC
var roles = await context.Admin.Roles.ListAll();
```

---

## PendingInsert\<T\>

A fluent pending-insert builder returned by `CollectionSet<T>.Insert(params T[])`.
Can be directly awaited or chained with additional `.Insert()` calls before calling `.Execute(ct)`.

### PendingInsert Methods

#### PendingInsert.Insert

```csharp
PendingInsert<T> Insert(params T[] entities)
```

Adds another batch to be executed sequentially after the previous ones.

#### PendingInsert.Execute

```csharp
Task<T[]> Execute(CancellationToken ct = default)
```

Executes all accumulated batches in order. IDs are assigned back to entities.
Throws `BatchInsertException<T>` if any batch has failures.

### PendingInsert Usage

```csharp
// Directly awaitable
var books = await context.Books.Insert(b1, b2, b3);

// Explicit CancellationToken
var books = await context.Books.Insert(b1, b2, b3).Execute(cancellationToken);

// Chained — second InsertMany runs after the first completes
// (useful for same-collection references)
var allBooks = await context.Books
    .Insert(b1, b2, b3)  // first InsertMany
    .Insert(b4)           // second InsertMany — b4 may reference b1/b2/b3
    .Execute(cancellationToken);
```

---

## PendingDelete\<T\>

A fluent pending-delete builder returned by `CollectionSet<T>.Delete(params T[])` and `CollectionSet<T>.Delete(params Guid[])`. Can be directly awaited or chained to accumulate IDs before a single batch call.

### PendingDelete Methods

#### PendingDelete.Delete (by entity)

```csharp
PendingDelete<T> Delete(params T[] entities)
```

Accumulates additional entities to delete.

#### PendingDelete.Delete (by ID)

```csharp
PendingDelete<T> Delete(params Guid[] ids)
```

Accumulates additional IDs to delete.

#### PendingDelete.Execute

```csharp
Task Execute(CancellationToken ct = default)
```

Sends all accumulated IDs in a single batch delete call.

### PendingDelete Usage

```csharp
// Directly awaitable
await context.Products.Delete(p1, p2, p3);

// Chained accumulation — all deleted in one batch call
await context.Products
    .Delete(p1, p2)
    .Delete(p3, p4)
    .Execute(cancellationToken);

// Mix entities and raw IDs
await context.Products
    .Delete(p1, p2)
    .Delete(oldId1, oldId2)
    .Execute(cancellationToken);
```

---

## PendingUpdate\<T\>

A fluent pending-update builder returned by `CollectionSet<T>.Update(params T[])`. Directly awaitable.

### PendingUpdate Methods

#### PendingUpdate.Execute

```csharp
Task<T[]> Execute(CancellationToken ct = default)
```

Executes all pending updates and returns the updated entities.

### PendingUpdate Usage

```csharp
// Directly awaitable — returns updated entities
var updated = await context.Products.Update(p1, p2, p3);

// Explicit CancellationToken
var updated = await context.Products.Update(p1, p2, p3).Execute(cancellationToken);
```

---

## PendingReference\<T\>

A fluent pending-reference builder returned by `CollectionSet<T>.AddReference(...)`. All accumulated links are sent in a single `ReferenceAddMany` call when executed. Directly awaitable or chainable.

### PendingReference Methods

#### PendingReference.AddReference

```csharp
PendingReference<T> AddReference<TRef>(T entity,
    Expression<Func<T, object?>> property, TRef target)
```

Accumulates an additional reference link to add.

#### PendingReference.Execute

```csharp
Task Execute(CancellationToken ct = default)
```

Sends all accumulated links in a single `ReferenceAddMany` call.

### PendingReference Usage

```csharp
// Directly awaitable — single link
await context.Articles.AddReference(article, a => a.Category, techCategory);

// Chained — all links sent in one call
await context.Articles
    .AddReference(article1, a => a.Category, techCategory)
    .AddReference(article2, a => a.Category, scienceCategory)
    .Execute(cancellationToken);

// Mix entity and Guid targets
await context.Articles
    .AddReference(article, a => a.Category, category)
    .AddReference(article, a => a.AuthorId, existingAuthorGuid)
    .Execute(cancellationToken);
```

---

## AggregateStarter\<T\>

Entry point for aggregation queries.

### WithMetrics\<TMetrics\>

```csharp
AggregateBuilder<T, TMetrics> WithMetrics<TMetrics>() where TMetrics : class, new()
```

Aggregate properties use `Aggregate` types from `Weaviate.Client.Models.Typed` with `[Metrics]` attributes to specify which metrics to compute. Returns `TypedAggregateBuilder<T, TMetrics>` which is directly awaitable (`.Execute()` optional).

```csharp
using Weaviate.Client.Models.Typed;
using Weaviate.Client.Managed.Attributes;

public class ProductStats
{
    [Metrics(Metric.Number.Mean, Metric.Number.Min, Metric.Number.Max, Metric.Number.Count)]
    public Aggregate.Number Price { get; set; }
}

// Execute() is optional - directly awaitable
var stats = await products.Aggregate()
    .WithMetrics<ProductStats>();

Console.WriteLine($"Average: {stats.Properties.Price.Mean:C}");

// GroupBy returns GroupedAggregateBuilder<T, TResult>
var grouped = await products.Aggregate()
    .WithMetrics<ProductStats>()
    .GroupBy(p => p.Category);

foreach (var group in grouped.Groups)
{
    Console.WriteLine($"{group.GroupedBy.Value}: {group.Properties.Price.Mean:C}");
}
```

---

## Extension Methods

### WeaviateClientExtensions

#### Collections.CreateManaged\<T\>

```csharp
Task<ManagedCollection<T>> CreateManaged<T>(this CollectionsClient collections, ...)
```

Creates a collection from type attributes and returns a managed wrapper.

#### Collections.CreateFromClass\<T\>

```csharp
Task<CollectionClient> CreateFromClass<T>(this CollectionsClient collections, ...)
```

Creates a collection from type attributes, returns raw `CollectionClient`.

#### Collections.UseManaged\<T\>

```csharp
ManagedCollection<T> UseManaged<T>(this CollectionsClient collections)
```

Gets an existing collection and wraps it in a managed wrapper. Collection name is resolved from `[WeaviateCollection]` attribute.

**Example:**
```csharp
// Compact syntax - infers collection name from attribute
var products = client.Collections.UseManaged<Product>();

// Equivalent to the verbose form:
var products = client.Collections.Use("Products").AsManaged<Product>();
```

#### Collections.Managed\<T\>

```csharp
Task<ManagedCollection<T>> Managed<T>(this CollectionsClient collections, ...)
```

Gets an existing collection with existence checking and returns a managed wrapper.

### CollectionClientExtensions

#### AsManaged\<T\>

```csharp
ManagedCollection<T> AsManaged<T>(this CollectionClient collection)
```

Wraps an existing collection client.

#### Query\<T\>

```csharp
CollectionMapperQueryClient<T> Query<T>(this CollectionClient collection)
```

Creates a query builder for an existing collection.

### QueryResultExtensions

```csharp
IEnumerable<T> Objects<T>(this IEnumerable<QueryResult<T>> results)
IEnumerable<QueryResult<T>> WithMetadata<T>(this IEnumerable<QueryResult<T>> results)
IOrderedEnumerable<QueryResult<T>> OrderByScore<T>(this IEnumerable<QueryResult<T>> results)
IOrderedEnumerable<QueryResult<T>> OrderByDistance<T>(this IEnumerable<QueryResult<T>> results)
```

---

## Models

### QueryResult\<T\>

```csharp
public record QueryResult<T>
{
    public Guid Id { get; init; }
    public T Object { get; init; }
    public Metadata? Metadata { get; init; }
}
```

### GenerativeQueryResult\<T\>

```csharp
public record GenerativeQueryResult<T> : QueryResult<T>
{
    public GenerativeResult? Generative { get; init; }
}
```

### GenerativeQueryResponse\<T\>

```csharp
public record GenerativeQueryResponse<T> : IEnumerable<GenerativeQueryResult<T>>
{
    public IList<GenerativeQueryResult<T>> Results { get; init; }
    public GenerativeResult? Generative { get; init; }  // Grouped task result
}
```

`GenerativeResult` implements `IList<string>`, so `result.Generative?[0]` returns the generated text.

### Query Projections

Project query results to a subset of properties using a projection type:

```csharp
[QueryProjection<Article>]
public class ArticleSummary
{
    [WeaviateUUID]
    public Guid Id { get; set; }

    public string Title { get; set; } = "";

    [MapFrom("WordCount")]
    public int Words { get; set; }
}

var results = await context.Query<Article>()
    .Project<ArticleSummary>()
    .Limit(10)
    .Execute();
```

### MigrationPlan

```csharp
public class MigrationPlan
{
    public string CollectionName { get; }
    public List<SchemaChange> Changes { get; }
    public bool HasChanges { get; }
    public bool IsCreate { get; }
    public bool IsSafe { get; }
    public bool IsOrphaned { get; }
    public string GetSummary();
    public static MigrationPlan ForOrphanedCollection(string collectionName);
}
```

### SchemaChange

```csharp
public class SchemaChange
{
    public SchemaChangeType ChangeType { get; }
    public string Description { get; }
    public bool IsSafe { get; }
}
```
