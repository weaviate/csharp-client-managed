# Weaviate C# Client – Copilot Coding Agent Quick Reference

## Project Overview

- Official C# SDK for Weaviate vector DB
- Two packages: **Weaviate.Client** (core REST/gRPC) and **Weaviate.Client.Managed** (ORM layer)
- REST (NSwag auto-gen) for CRUD/metadata, gRPC for queries/vectors
- Targets .NET 8.0 and .NET 9.0, using .NET 9.0 SDK

## Architecture & Conventions

- Core entry: `WeaviateClient` (REST/gRPC, collections, backup, RBAC)
- Managed entry points: `WeaviateContext` (recommended), `CollectionSet<T>`, `ManagedCollection<T>`
- Partial classes for separation; never edit auto-generated files
- File-scoped namespaces, CSharpier formatting enforced
- All I/O is async/await
- Private fields use `_fieldName` prefix
- C# PascalCase auto-converts to Weaviate camelCase

## Managed Client Architecture

- **WeaviateContext**: EF Core-like base class with `CollectionSet<T>` properties
- **CollectionSet\<T\>**: Per-type facade (Insert, Query, Project, Aggregate, Count, Iterator, migrations)
- **ManagedCollection\<T\>**: Standalone wrapper for use without context
- **WeaviateAdmin**: Admin facade (Backup, Users, Roles, Groups, Cluster, Aliases)
- **BatchScope**: Unit-of-work with dependency-ordered commits (topological sort)
- **GenerativeQueryExecutor\<T\>**: RAG query builder (returned by `.Generate()` on query)

## Key Attributes

- `[WeaviateCollection]` — Collection config, lifecycle hooks via `CollectionConfigMethod`
- `[WeaviateUUID]` — Marks Guid property as entity UUID
- `[Property]` — Data property with type inference
- `[Index]` — Filterable, Searchable, RangeFilters
- `[Vector<T>]` — Named vector (47+ vectorizers)
- `[Generative<T>]` — RAG provider (15+ providers)
- `[Reranker<T>]` — Reranking provider
- `[Reference]` — Cross-reference to another collection (with `Loading` strategy: `Explicit`/`Eager`)
- `[MetadataProperty]` — Inject query metadata
- `[QueryProjection<T>]` — Query projection type
- `[QueryAggregate<T>]` — Aggregation projection for context-level `Aggregate<TProjection>()`
- `[Metrics]` — Specifies aggregate metrics (Metric.Number, Metric.Integer, Metric.Text, Metric.Boolean, Metric.Date) on Aggregate properties
- `[MapFrom]` — Rename projection property
- `[Vector]` — Include a named vector in projection results (bare, non-generic)
- `[Reference]` — Also serves as projection marker; bare `[Reference]` on a projection property includes that reference

## Query Builder

All search modes on `CollectionMapperQueryClient<T>`:

- `.Where()` — LINQ filter expressions
- `.NearText()` — Semantic search
- `.NearVector()` — Vector search
- `.NearObject()` — Object similarity
- `.Hybrid()` — Keyword + vector combined
- `.BM25()` — Pure keyword search
- `.NearMedia()` — Image/audio/video search
- `.Targets()` — Multi-vector combination (Sum, Average, Minimum, ManualWeights, RelativeScore)
- `.Generate()` — RAG (returns `GenerativeQueryExecutor<T>` - directly awaitable)
- `.GroupBy(p => p.Property, groups, perGroup)` — Search-level grouping (returns `GroupByQueryExecutor<T>` - directly awaitable)
- `.Rerank()` — Re-order results
- `.Sort()`, `.Limit()`, `.Offset()`, `.After()`, `.AutoLimit()`
- `.Select()`, `.Project<TProjection>()`
- `.WithVectors()`, `.WithReferences()`, `.WithMetadata()`
- `.Execute()` — Optional for all query types (queries, projections, generative, aggregates)

## Context Configuration

`WeaviateContext` supports `OnConfiguring()` override and DI registration:

```csharp
// OnConfiguring override
public class BlogContext : WeaviateContext
{
    protected override void OnConfiguring(WeaviateContextOptionsBuilder options)
    {
        options.UseAutoCreate();
        options.UseAutoMigrate(allowBreaking: false);
    }
}

// DI registration
services.AddWeaviateContext<BlogContext>(options =>
{
    options.UseAutoCreate();
    options.UseAutoMigrate();
}, eagerMigration: true);
```

Options: `UseAutoCreate()`, `UseAutoMigrate(allowBreaking)`

## Aggregate Builder

All aggregate modes on `AggregateStarter<T>`:

- `.WithMetrics<TResult>()` — Returns `TypedAggregateBuilder<T, TResult>` (directly awaitable)
- `.Where()` — LINQ filter expressions
- `.GroupBy(p => p.Property)` — Returns `GroupedAggregateBuilder<T, TResult>` (directly awaitable)
- `.Execute()` — Optional for all aggregate types

## Code Generation & DTOs

- REST DTOs: auto-gen via NSwag from OpenAPI spec
- gRPC: auto-gen from proto files
- User models: `src/Weaviate.Client/Models/`
- REST DTOs: `src/Weaviate.Client/Rest/Dto/`
- Use `ToModel()`/`ToDto()` in `Rest/Dto/Extensions.cs` for mapping

## Enum & Wire Format

- Use `ToEnumMemberString()`/`FromEnumMemberString<T>()` for converting enums to/from strings
- Always prefer enums for permission actions and resource types

## Testing

- Unit: xUnit, mock HTTP handler for REST
- Integration: Docker Compose for Weaviate instance (`./ci/start_weaviate.sh`)
- Integration tests serialized via `[Collection("Integration")]` due to `GlobalOnCreate` static

## Serialization Paths

- REST (single Insert): `ObjectHelper.BuildDataTransferObject` handles ExpandoObject
- gRPC batch (InsertMany): `ObjectHelper.BuildBatchProperties` with reflection + IDictionary
- `PropertyMapper.ToWeaviateProperties()` returns ExpandoObject with camelCase keys

## Common Pitfalls

- Don't edit generated files
- Use enum helpers wherever possible
- Test isolation: use provided helpers and `GlobalOnCreate` for unique collection names
- Version checks: use `RequireVersion()`
- `ForTenant()` / `WithConsistencyLevel()` clone the context — don't mutate the original
