# Architecture

System design, component relationships, and design decisions for Weaviate.Client.Managed.

## Overview

Weaviate.Client.Managed is an **attribute-driven type-safe layer** built on top of Weaviate.Client. It provides:

- Declarative schema definition via C# attributes
- EF Core-like `WeaviateContext` with `CollectionSet<T>` properties
- Type-safe LINQ-style queries with expression trees
- Generative AI (RAG) query support
- Automatic object mapping (C# ↔ Weaviate)
- Batch operations with dependency ordering (`PendingBatch`, `PendingInsert`, `PendingDelete`, `PendingUpdate`, `PendingReference`)
- Safe schema migrations with breaking change detection
- Tenant and consistency level scoping

## High-Level Architecture

```text
┌─────────────────────────────────────────────────────────────┐
│  Your Application                                           │
│  - Define models with [WeaviateCollection], [Property], etc │
│  - Use WeaviateContext + CollectionSet<T>                    │
│  - Or use ManagedCollection<T> directly                     │
├─────────────────────────────────────────────────────────────┤
│  Weaviate.Client.Managed                                    │
│  ┌────────────────┐  ┌────────────────┐  ┌───────────────┐ │
│  │ WeaviateContext │  │ CollectionSet  │  │ PendingBatch  │ │
│  │ + Admin         │  │ <T>            │  │               │ │
│  └───────┬────────┘  └───────┬────────┘  └───────┬───────┘ │
│          │                   │                    │         │
│  ┌───────┴───────────────────┴────────────────────┴───────┐ │
│  │                ManagedCollection<T>                      │ │
│  │  Data: Insert, InsertMany, Update, Replace, Delete      │ │
│  │  Query: Where, NearText, Hybrid, BM25, Generate         │ │
│  │  Lifecycle: Count, Iterator, Exists, Migrate            │ │
│  └───────┬─────────────────────────────────────────────────┘ │
│          │                                                   │
│  ┌───────┴──────┐  ┌──────────────┐  ┌───────────────────┐ │
│  │ Query Builder │  │ Schema System │  │ Mapping System    │ │
│  │ + Generative  │  │              │  │                   │ │
│  └───────┬──────┘  └──────┬───────┘  └───────┬───────────┘ │
├──────────┴────────────────┴──────────────────┴──────────────┤
│  Weaviate.Client (Core)                                      │
│  - CollectionClient, DataClient, QueryClient, GenerateClient │
│  - REST (NSwag auto-gen) + gRPC communication                │
└──────────────────────────────────────────────────────────────┘
```

## Component Responsibilities

### Entry Points

| Component | Responsibility |
| --------- | -------------- |
| `WeaviateContext` | EF Core-like base class. Derive from it, declare `CollectionSet<T>` properties, use `Insert<T>()`, `Query<T>()`, `Count<T>()`, `Iterator<T>()`. Supports tenant scoping and consistency levels. |
| `CollectionSet<T>` | Per-collection facade (like `DbSet<T>`). Provides `Insert`, `Update`, `Replace`, `Delete`, `Find`, `Query`, `Aggregate`, `Count`, `Iterator`, and migration methods. |
| `ManagedCollection<T>` | Lower-level type-safe wrapper around `CollectionClient`. Same operations as `CollectionSet<T>` but without context integration. |
| `WeaviateAdmin` | Exposes admin operations: Backup, Users, Roles, Groups, Cluster, Aliases, health checks, and server metadata. Accessed via `context.Admin`. |
| `PendingBatch` | Cross-collection unit-of-work obtained via `context.Batch()`. Accumulates Insert/Update/Delete operations across types and executes them in dependency order via topological sort. |
| `PendingInsert<T>` | Same-collection ordered insert chaining; returned by `CollectionSet<T>.Insert(params T[])`. Directly awaitable or chainable. |
| `PendingDelete<T>` | Accumulated delete; returned by `CollectionSet<T>.Delete(...)`. Directly awaitable or chainable. |
| `PendingUpdate<T>` | Accumulated update; returned by `CollectionSet<T>.Update(params T[])`. Directly awaitable. |
| `PendingReference<T>` | Accumulated reference mutation; returned by `CollectionSet<T>.AddReference(...)`. Directly awaitable or chainable. |

### Schema System

| Component | Responsibility |
| --------- | -------------- |
| `CollectionSchemaBuilder` | Reads attributes from type `T`, produces `CollectionConfig`. |
| `VectorConfigBuilder` | Builds vector configurations from `[Vector<T>]` attributes. |
| `PropertyConfigBuilder` | Builds property configurations from `[Property]`, `[Index]`, etc. |
| Attribute classes | Declare schema intent on C# types. |

### Query System

| Component | Responsibility |
| --------- | -------------- |
| `CollectionMapperQueryClient<T>` | Fluent query builder with LINQ-style methods. Supports Where, NearText, NearVector, NearObject, Hybrid, BM25, NearMedia. |
| `GenerativeQueryExecutor<T>` | Wraps the query builder with generative (RAG) parameters. Returned by `.Generate()`. |
| `ProjectedQueryClient<T, TProjection>` | Projects query results to a different type using `[QueryProjection]`, `[MapFrom]`, and `[WeaviateUUID]` attributes. |
| `ExpressionToFilterConverter` | Converts `Expression<Func<T, bool>>` to Weaviate `Filter`. |
| `AggregateStarter<T>` | Entry point for type-safe aggregation queries. |

### Mapping System

| Component | Responsibility |
| --------- | -------------- |
| `CollectionObjectMapper` | Bidirectional mapping: C# object ↔ `WeaviateObject`. |
| `ManagedObjectMapper` | Maps `WeaviateObject<T>` back to a strongly-typed `T`. |
| `VectorMapper` | Extracts vectors from C# objects for insert, injects on read. |
| `ReferenceMapper` | Extracts reference IDs, handles single/multi references. |
| `MetadataInjector` | Injects query metadata (score, distance) into `[MetadataProperty]` fields. |

### Migration System

| Component | Responsibility |
| --------- | -------------- |
| `MigrationPlanner` | Compares type `T` schema vs. existing collection, produces `MigrationPlan`. |
| `MigrationExecutor` | Applies safe changes, blocks breaking changes unless explicitly allowed. |
| `SchemaChange` | Represents a single schema difference with safety indicator. |

### Context Infrastructure

| Component | Responsibility |
| --------- | -------------- |
| `CollectionSetDiscovery` | Discovers `CollectionSet<T>` properties on derived context classes via reflection. |
| `IdPropertyHelper` | Reads/writes `[WeaviateUUID]` properties on entities. |
| `BatchExecutor` | Executes `PendingBatch` operations with topological dependency ordering and cascading inserts. |

## Data Flow Diagrams

### Collection Creation

```text
CreateFromClass<Product>()
        │
        ▼
┌───────────────────────┐
│ CollectionSchemaBuilder│
│ - Read [WeaviateCollection] │
│ - Read [Property] attrs     │
│ - Read [Vector<T>] attrs    │
│ - Read [Reference] attrs    │
└───────────┬───────────┘
            │
            ▼
    CollectionConfig
            │
            ▼
┌───────────────────────┐
│ CollectionClient.Create │
│ (Core client)          │
└───────────────────────┘
```

### Context Insert Operation

```text
context.Insert(product)
        │
        ▼
┌───────────────────────┐
│ WeaviateContext.Insert │
│ → Set<T>().Insert()    │
└───────────┬───────────┘
        │
        ▼
┌───────────────────────┐
│ CollectionSet<T>.Insert│
│ → Collection.Insert()  │
│ → IdPropertyHelper.SetId() │
└───────────┬───────────┘
        │
        ├─► PropertyMapper.ToWeaviateProperties()
        │   → ExpandoObject with camelCase keys
        │
        ├─► VectorMapper.ExtractVectors()
        │   → Named vectors dict
        │
        ▼
┌───────────────────────┐
│ DataClient.Insert      │
│ (REST/gRPC to Weaviate)│
└───────────────────────┘
        │
        ▼
  UUID assigned back to product.Id
```

### Query Execution

```text
context.Products.Query()
    .Where(p => p.Price > 10)
    .NearText("wireless")
    .Limit(5)
    .Execute()
        │
        ▼
┌───────────────────────┐
│ CollectionMapperQueryClient │
│ - Build query state    │
└───────────┬───────────┘
        │
        ├─► ExpressionToFilterConverter
        │   - Expression tree → Filter
        │
        ▼
┌───────────────────────┐
│ TypedQueryClient<T>    │
│ - Execute gRPC query   │
└───────────┬───────────┘
        │
        ▼
   WeaviateObject<T>[]
        │
        ▼
┌───────────────────────┐
│ ManagedObjectMapper    │
│ - FromWeaviateObject() │
└───────────┬───────────┘
        │
        ▼
   IEnumerable<QueryResult<T>>
```

### Generative (RAG) Query

```text
context.Products.Query()
    .NearText("wireless mouse")
    .Generate(singlePrompt: "Describe this")
    .Execute()
        │
        ▼
┌───────────────────────┐
│ GenerativeQueryExecutor│
│ - Holds prompt params  │
│ - Delegates to builder │
└───────────┬───────────┘
        │
        ▼
┌───────────────────────┐
│ ExecuteGenerativeQueryAsync │
│ - TypedGenerateClient<T>    │
│ - Same search switch        │
│ + SinglePrompt/GroupedTask  │
└───────────┬───────────┘
        │
        ▼
   GenerativeQueryResponse<T>
   ├── Results[]: GenerativeQueryResult<T>
   │   ├── .Object (entity)
   │   ├── .Generative (per-object AI text)
   │   └── .Metadata
   └── .Generative (grouped task result)
```

### Batch Operation

```text
batch = context.Batch()
    batch.Insert(category1)
    batch.Insert(article1, article2)
    batch.Execute()
        │
        ▼
┌───────────────────────┐
│ BatchExecutor          │
│ 1. Collect all ops     │
│ 2. Detect cascading    │
│    inserts (refs)      │
│ 3. Execute deletes     │
│ 4. Topological sort    │
│    insert types        │
│ 5. Execute inserts     │
│    (InsertMany per type)│
│ 6. Execute updates     │
└───────────────────────┘
```

### Migration Flow

```text
context.Migrate()
        │
        ▼ (for each CollectionSet<T>)
┌───────────────────────┐
│ CollectionSchemaBuilder │
│ - Build expected schema │
└───────────┬───────────┘
        │
        ▼
┌───────────────────────┐
│ CollectionClient.Export │
│ - Get current schema   │
└───────────┬───────────┘
        │
        ▼
┌───────────────────────┐
│ MigrationPlanner       │
│ - Compare schemas      │
│ - Detect changes       │
│ - Classify safe/breaking│
└───────────┬───────────┘
        │
        ▼
   MigrationPlan
   { Changes[], HasBreakingChanges }
        │
        ▼
┌───────────────────────┐
│ MigrationExecutor      │
│ - Apply safe changes   │
│ - Block breaking (unless allowed) │
└───────────────────────┘
```

## Design Decisions

### 1. Extension Layer (Not Modification)

Managed is built **entirely on top of** Weaviate.Client using extension methods. Zero modifications to the core client.

**Why**:

- Core client remains stable and focused on wire protocol
- Managed features are opt-in
- Easy to maintain independently

### 2. Two API Levels

`WeaviateContext` + `CollectionSet<T>` for EF Core-style usage, `ManagedCollection<T>` for direct usage.

**Why**:

- Context provides familiar patterns (DbContext, DbSet)
- ManagedCollection provides a simpler entry point for small projects
- Both share the same underlying implementation

### 3. Attribute-Driven Schema

Schema is defined declaratively via C# attributes, not fluent builders or configuration classes.

**Why**:

- Schema lives with the model (single source of truth)
- Familiar pattern from EF Core, JSON serialization
- Compile-time discoverability via IntelliSense

### 4. Reflection at Schema Time Only

Attributes are read **once** during collection creation. Queries and data operations don't use reflection.

**Why**:

- Performance: no runtime attribute scanning
- Predictable: schema is static after creation

### 5. Expression Trees for Queries

`Where()` accepts `Expression<Func<T, bool>>` which is converted to Weaviate `Filter`.

**Why**:

- Compile-time type safety
- Familiar LINQ syntax
- IDE support (refactoring, find usages)

### 6. Automatic Vector Extraction/Injection

Vectors are extracted from model properties on insert and injected back on query.

**Why**:

- Natural C# experience (vectors are just properties)
- No manual dictionary manipulation
- Named vectors map to property names

### 7. Migration Safety by Default

`Migrate()` blocks breaking changes unless `allowBreakingChanges: true` is passed.

**Why**:

- Protect against accidental data loss
- Force explicit acknowledgment of destructive changes
- Safe for automated deployments

### 8. Naming Convention: PascalCase → camelCase

C# property `TitleContent` becomes Weaviate property `titleContent`.

**Why**:

- C# convention is PascalCase
- Weaviate convention is camelCase
- Automatic conversion via Humanizer
- Override with `[Property(Name = "custom_name")]`

### 9. Tenant and Consistency Scoping via Cloning

`ForTenant()` and `WithConsistencyLevel()` return **new context instances** (via `MemberwiseClone`) with fresh `CollectionSet<T>` instances. The original context is unchanged.

**Why**:

- Immutable scoping — no shared mutable state
- Each scope gets independent collection clients
- Safe for concurrent use across tenants

### 10. Batch Operations with Dependency Ordering

`PendingBatch.Execute()` uses topological sort to insert referenced entities before referencing entities.

**Why**:

- Cascading inserts work automatically
- No manual ordering of operations
- Circular reference detection with clear error messages

## Terminology

| Term | Meaning |
| ---- | ------- |
| **Managed** | The type-safe attribute-driven layer (this library) |
| `WeaviateContext` | EF Core-like base context class |
| `CollectionSet<T>` | Per-type collection facade (like `DbSet<T>`) |
| `ManagedCollection<T>` | Lower-level type-safe wrapper around `CollectionClient` |
| `WeaviateAdmin` | Admin operations facade (backup, RBAC, cluster, health) |
| `PendingBatch` | Cross-collection unit-of-work; obtained via `context.Batch()` |
| **Core Client** | The underlying `Weaviate.Client` library |
| **Schema** | Weaviate collection structure (properties, vectors, references) |
| **Mapping** | Converting between C# objects and Weaviate wire format |

## Extension Points

### Custom Type Conversion

Override how C# types map to Weaviate `DataType`:

```csharp
// Use explicit DataType when inference isn't correct
[Property(DataType.PhoneNumber)]
public string Phone { get; set; }
```

### Custom Property Names

Override the automatic PascalCase → camelCase conversion:

```csharp
[Property(Name = "user_email")]
public string Email { get; set; }
```

### ConfigMethod for Advanced Configuration

Full programmatic control when attributes aren't sufficient:

```csharp
[WeaviateCollection(ConfigMethod = nameof(ConfigureCollection))]
public class MyClass
{
    public static CollectionConfig ConfigureCollection(CollectionConfig config)
    {
        // Modify anything
        return config;
    }
}
```

### OnCollectionConfig Lifecycle Hook

Intercept collection configuration globally:

```csharp
// In your startup code
OnCollectionConfig.GlobalOnCreate = (config) =>
{
    // Apply to all collections
    config.ReplicationFactor = 2;
};
```

## Design Boundaries

These are deliberate design decisions, not missing features:

- **No Lazy Loading**: References must be explicitly loaded via `.WithReferences()` or marked `[Reference(Loading = Eager)]`. Implicit network I/O on property access would be surprising in a distributed system context.
- **No Change Tracking**: Updates are always explicit (`Update(entity)`). Dirty-tracking adds memory overhead and hidden network calls that conflict with Weaviate's read-optimised model.
