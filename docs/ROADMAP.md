# Roadmap

Current state, recent improvements, and future direction.

---

## Current State

### Fully Implemented

| Feature | Status | Description |
| --- | --- | --- |
| **WeaviateContext** | Complete | EF Core-like context with `CollectionSet<T>` properties |
| **CollectionSet\<T\>** | Complete | Per-type collection facade (Insert, Query, Aggregate, etc.) |
| **PendingBatch / PendingInsert / PendingDelete / PendingUpdate / PendingReference** | Complete | Fluent pending-operation builders; `PendingBatch` (cross-collection via `context.Batch()`) uses dependency-ordered commits |
| **WeaviateAdmin** | Complete | Admin facade (Backup, Users, Roles, Cluster, etc.) |
| **Schema Definition** | Complete | Declarative attributes for collections, properties, vectors |
| **Property Configuration** | Complete | All data types, indexing, tokenization |
| **Vector Configuration** | Complete | 47+ vectorizers, all index types, all quantizers |
| **Multi-Vector (ColBERT)** | Complete | Encoding configuration |
| **Generative AI (RAG)** | Complete | Fluent `.Generate()` API with 15+ providers |
| **Rerankers** | Complete | 6 providers with fluent `.Rerank()` |
| **References** | Complete | Single, multi, ID-only with full hydration via `.WithReferences()` |
| **Nested Objects** | Complete | Automatic type inference |
| **Query Builder** | Complete | Where, NearText, NearVector, NearObject, Hybrid, BM25, NearMedia |
| **Query Projections** | Complete | `Project<T>()` with `[MapFrom]`, `[Vector]`, `[Reference]`, `[MetadataProperty]`, `[QueryProjection<T>(Combination = ...)]` |
| **Aggregations** | Complete | Type-safe metric extraction, context-level `Aggregate<TProjection>()` with `[QueryAggregate<T>]` |
| **Data Operations** | Complete | Insert, InsertMany, Update, Replace, Delete (by ID/entity), DeleteMany (by filter/entities), Find |
| **Reference Loading** | Complete | Explicit (default) and Eager strategies via `[Reference(Loading = ...)]` |
| **Orphan Detection** | Complete | `GetPendingMigrations()` detects orphaned server collections; `Migrate(destructive: true)` deletes them |
| **Count** | Complete | `Count()` on collection, context, and set |
| **Iterator** | Complete | `IAsyncEnumerable<T>` with cursor-based pagination |
| **Object Mapping** | Complete | Automatic vector/reference extraction/injection |
| **Migrations** | Complete | Per-collection and context-wide, with breaking change detection |
| **Multi-Tenancy** | Complete | `ForTenant()` / `WithTenant()` scoping |
| **Consistency Levels** | Complete | `WithConsistencyLevel()` scoping |
| **Lifecycle Hooks** | Complete | `OnCollectionConfig` with `GlobalOnCreate` interceptor |
| **ConfigMethod** | Complete | Escape hatch for advanced configuration |
| **WeaviateUUID** | Complete | `[WeaviateUUID]` attribute with auto-assignment on insert |
| **Pagination** | Complete | Limit, Offset, AutoLimit, After (cursor) |
| **Dependency Injection** | Complete | `AddWeaviateContext<T>()` with `eagerMigration`, singleton/scoped lifetime, `WeaviateContextOptions` |
| **GroupBy** | Complete | Search-level `GroupBy<TProp>()` returning `GroupByQueryResponse<T>` with per-group hits and aggregates |
| **LINQ / IQueryable** | Complete | `CollectionSet<T>` implements `IQueryable<T>`; expressions translate to Weaviate filters at query time |

### Feature Parity

The managed client provides **full feature parity** with the core Weaviate C# client for data-plane operations. Any schema you can create manually can be expressed with attributes, and all query modes, data operations, and administrative APIs are accessible.

---

## Recent Improvements (Feb 2026)

### WeaviateContext and CollectionSet

EF Core-like context pattern with `CollectionSet<T>` properties, context-level shortcuts for Insert/Update/Delete/Query, and database-wide migration coordination via `context.Migrate()`.

### Generative AI (RAG) Query Builder

Fluent `.Generate()` method on the query builder for single prompts and grouped tasks. Returns `GenerativeQueryResponse<T>` with per-object and grouped generative results.

### Batch Operations

`PendingBatch` (via `context.Batch()`) with dependency-ordered commits using topological sort. Groups Insert, Update, and Delete operations across collection types. Same-collection ordered inserts use `PendingInsert<T>` chaining. `PendingDelete<T>`, `PendingUpdate<T>`, and `PendingReference<T>` provide fluent accumulation for their respective operations.

### Admin Facade

`WeaviateAdmin` exposing Backup, Users, Roles, Groups, Cluster, and Aliases sub-clients, plus health check and metadata methods.

### Extended Query Builder

Added BM25, NearObject, NearMedia search modes. Added Offset, AutoLimit, After (cursor) pagination. Added Rerank, `Project<>()` (projections).

### Reference Loading with Full Hydration

`.WithReferences()` explicitly requests reference expansion (similar to EF Core's `.Include()`). Referenced objects are fully populated recursively using the core client's `PropertyConverterRegistry`.

### Tenant and Consistency Scoping

`context.ForTenant()` and `context.WithConsistencyLevel()` create scoped contexts via immutable cloning.

### Count and Iterator

`Count()` for object counts and `Iterator()` returning `IAsyncEnumerable<T>` for streaming all objects.

### Unified Query Execution API

Single `Execute()` method returning `IEnumerable<QueryResult<T>>` with both `.Object` and `.Metadata`. Extension methods: `.Objects()`, `.WithMetadata()`, `.OrderByScore()`, `.OrderByDistance()`.

### GroupBy

Search-level `GroupBy<TProp>()` terminal method groups results by any filterable property. Returns `GroupByQueryResponse<T>` with per-group hits (each as `QueryResult<T>`) and aggregate data (min, max, mean, median, mode, count, sum).

### LINQ / IQueryable Provider

`CollectionSet<T>` implements `IQueryable<T>`. LINQ `Where()` expressions are translated to Weaviate filters at query time, enabling standard C# LINQ syntax to drive server-side filtering without manual expression trees.

---

## Known Limitations

### No Lazy Loading of References

References require explicit `.WithReferences()` in the query or `[Reference(Loading = Eager)]` on the model — they are not loaded on property access. This is by design: Weaviate is a remote service, and implicit network calls on property access would be surprising and hard to debug.

```csharp
// References are null unless explicitly requested or marked as Eager
var results = await context.Articles.Query().Execute();
results.First().Object.Category  // null (unless Loading = Eager)

// Explicit loading (like EF Core's .Include())
var results = await context.Articles.Query()
    .WithReferences(a => a.Category)
    .Execute();
results.First().Object.Category?.Name  // Fully hydrated

// Or mark as Eager on the model — auto-included in every query
[Reference(Loading = ReferenceLoadingStrategy.Eager)]
public Category? Category { get; set; }
```

### No Change Tracking

Change tracking is a deliberate non-goal. Weaviate is a remote service optimised for reads, and implicit dirty-checking would add memory overhead and surprising network calls. Explicit update calls are the intended pattern:

```csharp
// Explicit update — the supported pattern
await context.Update(article);
```

---

## Contributing

We welcome contributions! Priority areas:

1. **Documentation** — Examples, tutorials, migration guides
