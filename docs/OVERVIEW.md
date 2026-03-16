# Weaviate.Client.Managed — Architecture Overview

## 1. Architecture Layers

```mermaid
graph TD
    UserCode["User Code<br/>(Domain Models + WeaviateContext)"]
    Managed["Weaviate.Client.Managed<br/>(ORM Layer)"]
    Core["Weaviate.Client<br/>(Core REST/gRPC Client)"]
    Weaviate["Weaviate Instance<br/>(REST + gRPC)"]

    UserCode -->|"CollectionSet<T> / ManagedCollection<T>"| Managed
    Managed -->|"CollectionClient / DataClient"| Core
    Core -->|"HTTP / gRPC"| Weaviate
```

## 2. Component Map

```mermaid
graph LR
    subgraph Context ["Context"]
        WC["WeaviateContext<br/>(EF Core-style base)"]
        CS["CollectionSet(T)<br/>(per-type facade)"]
        PB["PendingBatch<br/>(cross-collection unit-of-work)"]
        Admin["WeaviateAdmin<br/>(backup, users, roles)"]
        WC --> CS
        WC --> PB
        WC --> Admin
    end

    subgraph Query ["Query"]
        CMQC["CollectionMapperQueryClient(T)<br/>(fluent builder)"]
        PQC["ProjectedQueryClient(T,TProjection)<br/>(mapped results)"]
        GQE["GenerativeQueryExecutor(T)<br/>(RAG / .Generate())"]
        TVB["TargetVectorBuilder(T)<br/>(.VectorTargets() callback)"]
        SC["SearchConfig records<br/>(NearTextConfig etc.)"]
        ETFC["ExpressionToFilterConverter<br/>(LINQ → Weaviate filters)"]
        CMQC --> PQC
        CMQC --> GQE
        CMQC --> TVB
        CMQC --> ETFC
        PQC --> SC
    end

    subgraph Mapping ["Mapping"]
        COM["CollectionObjectMapper<br/>(C# ↔ WeaviateObject)"]
        VM["VectorMapper"]
        RM["ReferenceMapper"]
        MI["MetadataInjector"]
        PM["ProjectionMapper"]
        PrM["PropertyMapper"]
        COM --> VM
        COM --> RM
        COM --> MI
        PM --> PrM
    end

    subgraph Schema ["Schema"]
        CSB["CollectionSchemaBuilder<br/>(attributes → CollectionConfig)"]
        VCB["VectorConfigBuilder"]
        PCB["PropertyConfigBuilder"]
        MP["MigrationPlanner"]
        ME["MigrationExecutor"]
        CSB --> VCB
        CSB --> PCB
        MP --> ME
    end

    CS --> CMQC
    CS --> COM
    CS --> CSB
    PQC --> PM
    COM --> PrM
```

## 3. Key Data Flows

### Insert (single object, REST path)

```mermaid
sequenceDiagram
    participant U as User Code
    participant MC as "ManagedCollection<T>"
    participant COM as CollectionObjectMapper
    participant PM as PropertyMapper
    participant VM as VectorMapper
    participant RM as ReferenceMapper
    participant Core as DataClient (Core)
    participant W as Weaviate

    U->>MC: Insert(entity)
    MC->>COM: ToWeaviateObject(entity)
    COM->>PM: ToWeaviateProperties(entity)
    PM-->>COM: ExpandoObject (camelCase keys)
    COM->>VM: ExtractVectors(entity)
    VM-->>COM: "Dictionary<name, float[]>"
    COM->>RM: ExtractReferences(entity)
    RM-->>COM: "Dictionary<name, List<WeaviateObject>>"
    COM-->>MC: WeaviateObject
    MC->>Core: Data.Insert(weaviateObject)
    Core->>W: POST /objects
    W-->>Core: 200 OK (UUID)
    Core-->>MC: Guid
    MC-->>U: Guid
```

### Query with Projection

```mermaid
sequenceDiagram
    participant U as User Code
    participant CS as "CollectionSet<T>"
    participant CMQC as "CollectionMapperQueryClient<T>"
    participant PQC as "ProjectedQueryClient<T,TProjection>"
    participant PM as ProjectionMapper
    participant Core as QueryClient (Core, gRPC)
    participant W as Weaviate

    U->>CS: "Query.Where(...).Project<TProjection>().Limit(10)"
    CS->>CMQC: Where(predicate)
    CMQC->>PQC: "Project<TProjection>()"
    Note over PQC: AutoConfigure():<br/>- SetReturnProperties<br/>- AddIncludeVectors<br/>- WithMetadata<br/>- Targets() from attribute/method
    U->>PQC: Execute()
    PQC->>PQC: ApplyNearTextConfig / ApplyHybridConfig etc.
    PQC->>CMQC: ExecuteQueryAsync()
    CMQC->>Core: gRPC search
    Core->>W: gRPC SearchNearText / FetchObjects / etc.
    W-->>Core: gRPC SearchReply
    Core-->>CMQC: "WeaviateObject<T>[]"
    CMQC-->>PQC: WeaviateResult
    PQC->>PM: "MapToProjection<T,TProjection>(wo)"
    PM-->>PQC: TProjection
    PQC-->>U: "IEnumerable<QueryResult<TProjection>>"
```

### Insert (batch via PendingInsert)

```mermaid
sequenceDiagram
    participant U as User Code
    participant CS as "CollectionSet<T>"
    participant PI as "PendingInsert<T>"
    participant MC as "ManagedCollection<T>"
    participant COM as CollectionObjectMapper
    participant Core as DataClient (Core, gRPC)
    participant W as Weaviate gRPC

    U->>CS: Insert(e1, e2, e3)
    CS->>PI: new PendingInsert(entities)
    PI-->>U: "PendingInsert<T> (awaitable)"
    U->>PI: await / Execute()
    PI->>MC: InsertMany(entities) [internal]
    MC->>COM: ToWeaviateObject(entity) × N
    COM-->>MC: BatchInsertRequest[]
    MC->>Core: Data.InsertMany(BatchInsertRequest[])
    Core->>W: gRPC BatchObjects
    W-->>Core: BatchInsertResponse
    Core-->>MC: BatchInsertResponse
    MC-->>PI: BatchInsertResponse
    PI-->>U: T[] (with UUIDs populated)
```

## 4. Attribute System

All attributes live in `Weaviate.Client.Managed.Attributes/`.

### Entity-level (on collection classes)

| Attribute | Purpose |
|-----------|---------|
| `[WeaviateCollection]` | Collection name, lifecycle hooks, multi-tenancy, sharding, replication |
| `[WeaviateUUID]` | Marks a `Guid` property as the entity's UUID |
| `[Property(DataType)]` | Property definition with type, name override, description |
| `[Index]` | Filterable / Searchable / RangeFilters flags |
| `[Tokenization]` | Text tokenization strategy |
| `[Vector<TVectorizer>]` | Named vector with vectorizer config (generic, 47+ vectorizers) |
| `[VectorIndex<TIndexConfig>]` | HNSW / Flat / Dynamic index |
| `[Quantizer*]` | BQ / PQ / SQ / RQ vector quantization |
| `[Reference]` | Cross-reference; target inferred from property type; `Target=` required for `Guid?` |
| `[Generative<TModule>]` | RAG module config |
| `[Reranker<TModule>]` | Reranker config |

### Projection-level (on projection classes)

| Attribute | Purpose |
|-----------|---------|
| `[QueryProjection<TCollection>]` | Marks a class as a query projection; `Combination` for multi-vector |
| `[QueryAggregate<T>]` | Marks a class as an aggregation projection |
| `[MapFrom("SourceName")]` | Maps a projection property from a differently-named source property |
| `[Vector]` | Includes a named vector in projection results; `Weight` for ManualWeights |
| `[Reference]` | Includes a reference in projection results; `SourceProperty` override |
| `[MetadataProperty]` | Injects query metadata (score, distance, etc.) |

### Convention-based static methods on projection classes

| Method | Signature | Purpose |
|--------|-----------|---------|
| `ConfigureNearText` | `static NearTextConfig ConfigureNearText(NearTextConfig)` | Default NearText settings |
| `ConfigureNearVector` | `static NearVectorConfig ConfigureNearVector(NearVectorConfig)` | Default NearVector settings |
| `ConfigureHybrid` | `static HybridConfig ConfigureHybrid(HybridConfig)` | Default Hybrid settings |
| `ConfigureNearObject` | `static NearObjectConfig ConfigureNearObject(NearObjectConfig)` | Default NearObject settings |
| `ConfigureNearMedia` | `static NearMediaConfig ConfigureNearMedia(NearMediaConfig)` | Default NearMedia settings |
| `ConfigureVectorTargets` | `static TargetVectorBuilder<TCollection> ConfigureVectorTargets(TargetVectorBuilder<TCollection>)` | Multi-vector targeting (takes precedence over `Combination` attribute) |

### Convention-based static methods on entity and projection classes

| Method | Signature | Scope | Precedence | Purpose |
|--------|-----------|-------|------------|----------|
| `ConfigureSearch` | `static void ConfigureSearch(QueryConfig<T>)` | Entity or Projection | Query > Projection > Entity | Apply query options (Where, Limit, Offset, OrderBy, etc.) |

**Precedence rules** (highest to lowest):
1. **Explicit query calls**: `.Limit(27)` always takes precedence
2. **Projection-level**: `ConfigureSearch` on projection class overrides entity defaults
3. **Entity-level**: `ConfigureSearch` on entity class provides base defaults

## 5. Feature Summary

| Feature | Entry point | Notes |
|---------|-------------|-------|
| **Schema creation** | `client.Collections.CreateFromClass<T>()` | Reads attributes, creates collection |
| **Schema migration** | `collection.Migrate()` | Compares live schema to attribute spec; blocks breaking changes by default |
| **Insert (single)** | `await context.Insert(entity)` | REST path; returns entity with UUID |
| **Insert (batch)** | `await context.Insert(e1, e2, e3)` | gRPC path; returns `PendingInsert<T>` (directly awaitable) |
| **Update** | `collection.Update(entity, id)` | Partial update (PATCH) |
| **Replace** | `collection.Replace(entity, id)` | Full replace (PUT) |
| **Delete (single/batch)** | `await context.Delete(e1, e2, e3)` | Returns `PendingDelete<T>` (directly awaitable); uses `Filter.UUID.ContainsAny` |
| **Query (typed)** | `collection.Query()` | Returns `IEnumerable<QueryResult<T>>` (`.Execute()` optional) |
| **Query (projected)** | `collection.Query<TProjection>()` | Maps results to projection type (`.Execute()` optional) |
| **Query (generative/RAG)** | `.Generate(...)` | Returns `GenerativeQueryResponse<T>` (`.Execute()` optional) |
| **Query (grouped)** | `.GroupBy(p => p.Property, groups, perGroup)` | Returns `GroupByQueryExecutor<T>` (directly awaitable) → `GroupByQueryResponse<T>` |
| **Aggregate** | `collection.Aggregate.WithMetrics<TResult>()` | Typed aggregate results (`.Execute()` optional) |
| **Aggregate (grouped)** | `.GroupBy(p => p.Property)` | Returns `GroupedAggregateBuilder<T,TResult>` (directly awaitable) |
| **Multi-vector targeting** | `.Targets(t => t.Sum(...))` | Combines named vectors |
| **Batch (cross-collection)** | `context.Batch().Execute()` | Topological-sort ordering |
| **References** | `[Reference]` on property | Eager/Explicit loading; target inferred from type |
| **Multi-tenancy** | `collection.WithTenant("tenant")` | Per-tenant scoping |
| **Dependency Injection** | `services.AddWeaviateContext<TContext>()` | Registers context + eager migration option |
| **Roslyn Analyzer** | `Weaviate.Client.Managed.Analyzers` | Compile-time validation of attribute usage |

## 6. Mapping Architecture

```mermaid
graph LR
    subgraph "C# → Weaviate (Write)"
        Entity["Entity (C# object)"]
        PropMapper["PropertyMapper<br/>PascalCase → camelCase<br/>ExpandoObject"]
        VecMapper["VectorMapper<br/>float[] → Vectors dict"]
        RefMapper["ReferenceMapper<br/>object refs → UUID lists"]
        WObj["WeaviateObject"]
        Entity --> PropMapper --> WObj
        Entity --> VecMapper --> WObj
        Entity --> RefMapper --> WObj
    end

    subgraph "Weaviate → C# (Read)"
        WObj2["WeaviateObject(T)"]
        Unmarshal["ObjectHelper.UnmarshallProperties(T)<br/>(case-insensitive matching)"]
        MetaInject["MetadataInjector<br/>score/distance → [MetadataProperty]"]
        ProjMap["ProjectionMapper<br/>T → TProjection"]
        Entity2["Entity / Projection (C# object)"]
        WObj2 --> Unmarshal --> Entity2
        WObj2 --> MetaInject --> Entity2
        Entity2 --> ProjMap --> Entity2
    end
```

## 7. Query Builder State Machine

`CollectionMapperQueryClient<T>` tracks search mode and accumulates parameters:

```mermaid
graph TD
    Start([Start]) --> Idle
    Idle -->|.NearText| NearText[NearText Mode]
    Idle -->|.NearVector| NearVector[NearVector Mode]
    Idle -->|.Hybrid| Hybrid[Hybrid Mode]
    Idle -->|.BM25| BM25[BM25 Mode]
    Idle -->|.NearObject| NearObject[NearObject Mode]
    Idle -->|.NearMedia| NearMedia[NearMedia Mode]

    NearText -->|.Where/.Limit/.Targets/.Rerank/...| NearText
    NearVector -->|.Where/.Limit/.Targets/...| NearVector
    Hybrid -->|.Where/.Limit/.Targets/...| Hybrid
    BM25 -->|.Where/.Limit/...| BM25
    NearObject -->|.Where/.Limit/.Targets/...| NearObject
    NearMedia -->|.Where/.Limit/.Targets/...| NearMedia

    Idle -->|.Execute| Results([Execute: fetch-all])
    NearText -->|.Execute| Results
    NearVector -->|.Execute| Results
    Hybrid -->|.Execute| Results
    BM25 -->|.Execute| Results
    NearObject -->|.Execute| Results
    NearMedia -->|.Execute| Results
```
