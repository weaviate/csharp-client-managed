# Guide

Complete user guide for Weaviate.Client.Managed.

## Table of Contents

- [Installation](#installation)
- [Defining Models](#defining-models)
- [WeaviateContext (Recommended)](#weaviatecontext-recommended)
- [Creating Collections](#creating-collections)
- [Data Operations](#data-operations)
- [Querying](#querying)
- [Generative AI (RAG)](#generative-ai-rag)
- [Aggregations](#aggregations)
- [Count and Iterator](#count-and-iterator)
- [Batch Operations](#batch-operations)
- [Migrations](#migrations)
- [Multi-Tenancy](#multi-tenancy)
- [Consistency Levels](#consistency-levels)
- [References](#references)
- [Nested Objects](#nested-objects)
- [Collection Configuration Hooks](#collection-configuration-hooks)
- [Admin Operations](#admin-operations)

---

## Installation

```bash
dotnet add package Weaviate.Client.Managed
```

Required namespaces:

```csharp
using Weaviate.Client;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Managed.Context;
using Weaviate.Client.Managed.Extensions;
using Weaviate.Client.Models;
```

---

## Defining Models

### Basic Model

```csharp
[WeaviateCollection("Articles")]
public class Article
{
    [WeaviateUUID]
    public Guid Id { get; set; }

    [Property]
    public string Title { get; set; } = "";

    [Property]
    public string Content { get; set; } = "";

    [Property]
    public DateTime PublishedAt { get; set; }

    [Vector<Vectorizer.Text2VecOpenAI>(
        SourceProperties = [nameof(Title), nameof(Content)]
    )]
    public float[]? Embedding { get; set; }
}
```

### With Indexing

```csharp
[WeaviateCollection("Products")]
public class Product
{
    [WeaviateUUID]
    public Guid Id { get; set; }

    [Property]
    [Index(Filterable = true, Searchable = true)]
    [Tokenization(PropertyTokenization.Word)]
    public string Name { get; set; } = "";

    [Property]
    [Index(Filterable = true, RangeFilters = true)]
    public decimal Price { get; set; }

    [Property]
    [Index(Filterable = true)]
    public bool InStock { get; set; }
}
```

### Multiple Vectors

```csharp
[WeaviateCollection("Products")]
public class Product
{
    [Property]
    public string Name { get; set; } = "";

    [Property]
    public string Description { get; set; } = "";

    [Property(DataType.Blob)]
    public byte[]? Image { get; set; }

    // Text embedding
    [Vector<Vectorizer.Text2VecOpenAI>(
        SourceProperties = [nameof(Name), nameof(Description)]
    )]
    public float[]? TextEmbedding { get; set; }

    // Multi-modal embedding
    [Vector<Vectorizer.Multi2VecClip>(
        TextFields = [nameof(Name)],
        ImageFields = [nameof(Image)]
    )]
    public float[]? MultiModalEmbedding { get; set; }

    // User-provided embedding
    [Vector<Vectorizer.SelfProvided>]
    public float[]? CustomEmbedding { get; set; }
}
```

### With RAG

```csharp
[WeaviateCollection("Documents")]
[Generative<GenerativeConfig.OpenAI>(Model = "gpt-4")]
[Reranker<RerankerConfig.Cohere>(Model = "rerank-english-v3.0")]
public class Document
{
    [Property]
    public string Title { get; set; } = "";

    [Property]
    public string Content { get; set; } = "";

    [Vector<Vectorizer.Text2VecOpenAI>]
    public float[]? Embedding { get; set; }
}
```

---

## WeaviateContext (Recommended)

The `WeaviateContext` provides an EF Core-like experience with `CollectionSet<T>` properties. This is the recommended approach for most applications.

### Define a Context

```csharp
public class BlogContext : WeaviateContext
{
    public BlogContext(WeaviateClient client) : base(client) { }

    public CollectionSet<Article> Articles { get; set; } = null!;
    public CollectionSet<Category> Categories { get; set; } = null!;
    public CollectionSet<Author> Authors { get; set; } = null!;
}
```

### Context Configuration

Override `OnConfiguring()` to set automatic behaviors:

```csharp
public class BlogContext : WeaviateContext
{
    public BlogContext(WeaviateClient client) : base(client) { }

    public CollectionSet<Article> Articles { get; set; } = null!;

    protected override void OnConfiguring(WeaviateContextOptionsBuilder options)
    {
        // Auto-create collections on first access
        options.UseAutoCreate();

        // Auto-migrate collections on first access (safe changes only)
        options.UseAutoMigrate();

        // Auto-migrate with breaking changes allowed
        options.UseAutoMigrate(allowBreaking: true);
    }
}
```

**Available Options:**

- `UseAutoCreate()` — Automatically creates collections that don't exist when first accessed
- `UseAutoMigrate(allowBreaking: false)` — Automatically migrates collections on first access

When using dependency injection, options can be configured at registration time (see [DEPENDENCY_INJECTION.md](DEPENDENCY_INJECTION.md)).

### Create and Use

```csharp
var client = new WeaviateClient(new WeaviateConfig { Host = "localhost:8080" });

// Create collections from model attributes
await client.Collections.CreateFromClass<Article>();
await client.Collections.CreateFromClass<Category>();
await client.Collections.CreateFromClass<Author>();

// Create context
var context = new BlogContext(client);

// Use collection sets
await context.Articles.Insert(new Article { Title = "Hello World" });
var results = await context.Articles.Query().NearText("hello").Execute();
```

### Context-Level Shortcuts

The context provides direct methods that infer the collection from the generic type:

```csharp
// Insert (infers collection from type)
var article = new Article { Title = "My Post" };
await context.Insert(article);
Console.WriteLine(article.Id); // ID assigned back to entity

// Update
article.Title = "Updated Title";
await context.Update(article);

// Delete
await context.Delete<Article>(article.Id);

// Query
var results = await context.Query<Article>()
    .Where(a => a.Title.Contains("hello"))
    .Execute();

// Count
var count = await context.Count<Article>();

// Iterate all objects
await foreach (var a in context.Iterator<Article>())
    Console.WriteLine(a.Title);
```

---

## Creating Collections

### Using ManagedCollection\<T\> (Alternative)

If you don't need a full context:

```csharp
var client = new WeaviateClient(new WeaviateConfig { Host = "localhost:8080" });

// Creates collection and returns ManagedCollection<T>
var products = await client.Collections.CreateManaged<Product>();
```

### Create Only (No Wrapper)

```csharp
// Creates collection, returns raw CollectionClient
var collection = await client.Collections.CreateFromClass<Product>();
```

### Wrap Existing Collection

```csharp
var collection = client.Collections.Use("Products");
var managed = collection.AsManaged<Product>();
```

### Create If Not Exists

```csharp
var products = await client.Collections.CreateManaged<Product>(
    existsAction: ExistsAction.UseExisting
);
```

---

## Data Operations

### Insert Single

`Insert` returns the entity with its `[WeaviateUUID]` property populated, so you can use it
inline:

```csharp
// Inline — captures the returned entity with its assigned ID
var product = await context.Insert(new Product { Name = "Wireless Mouse", Price = 29.99m });
Console.WriteLine(product.Id); // Guid assigned by Weaviate

// Also works when you already hold a reference — entity is mutated in-place and returned
var product = new Product { Name = "Keyboard", Price = 79.99m };
await context.Insert(product);
Console.WriteLine(product.Id); // Same result
```

### Insert Many (Batch)

`Insert(params T[])` returns a `PendingInsert<T>` that can be directly awaited or
chained with additional `.Insert()` calls before calling `.Execute(ct)`.
On partial failure it throws `BatchInsertException<T>`.

```csharp
// Directly awaitable — simple batch
var products = await context.Products.Insert(keyboard, monitor, webcam);

// Explicit Execute with CancellationToken
var products = await context.Products
    .Insert(keyboard, monitor, webcam)
    .Execute(cancellationToken);

// Chained batches — second batch is inserted AFTER the first completes.
// Useful when p4 has same-collection cross-references to p1/p2/p3.
var all = await context.Products
    .Insert(p1, p2, p3)
    .Insert(p4)
    .Execute(cancellationToken);
```

On failure, catch `BatchInsertException<T>`:

```csharp
try
{
    await context.Products.Insert(p1, p2, p3);
}
catch (BatchInsertException<T> ex)
{
    Console.WriteLine($"Inserted: {ex.Inserted.Count}");
    foreach (var e in ex.Errors)
        Console.WriteLine($"{e.Object.Name}: {e.Error.Message}");
}
```

### Update (Partial)

PATCH semantics — only non-null properties are updated. Returns the entity.

```csharp
product.Price = 24.99m;
var updated = await context.Update(product);
```

### Replace (Full)

PUT semantics — replaces the entire object. Returns the entity.

```csharp
var replaced = await context.Products.Replace(new Product
{
    Id = productId,
    Name = "Updated Mouse",
    Price = 34.99m,
    InStock = true
});
```

### Delete

```csharp
// Delete by ID
await context.Delete<Product>(productId);

// Delete by entity (extracts UUID from [WeaviateUUID] property)
await context.Delete(product);

// Delete multiple entities
await context.Delete(product1, product2, product3);

// Batch delete by entities (uses Filter.UUID.ContainsAny internally)
await context.DeleteMany(products);

// Delete many by filter (via ManagedCollection)
var result = await managedProducts.DeleteMany(p => !p.InStock);
Console.WriteLine($"Deleted: {result.Matches}");

// Dry run first
var preview = await managedProducts.DeleteMany(p => p.Price < 10, dryRun: true);
Console.WriteLine($"Would delete: {preview.Matches}");
```

### Find by ID

```csharp
var product = await context.Products.Find(productId);
```

---

## Querying

All search modes share the same fluent builder, accessed via `.Query()`.

### Basic Filtering (Fetch)

```csharp
var results = await context.Products.Query()
    .Where(p => p.Price > 50)
    .Limit(20)
    .Execute();
```

### Multiple Filters (AND)

```csharp
var results = await context.Products.Query()
    .Where(p => p.Price > 20)
    .Where(p => p.Price < 100)
    .Where(p => p.InStock)
    .Execute();
```

### Complex Filters

```csharp
// String operations
.Where(p => p.Name.Contains("wireless"))
.Where(p => p.Name.StartsWith("Pro"))

// Null checks
.Where(p => p.Category != null)

// Combined with OR (inside single Where)
.Where(p => p.Price < 10 || p.Name.Contains("sale"))
```

### Geo Range Filters

Filter by distance from a geographic coordinate using `IsWithinGeoRange` on any `GeoCoordinate`
property. The distance is in **metres**.

```csharp
// Inline lat/lon/distance
var results = await context.Stores.Query()
    .Where(s => s.Location.IsWithinGeoRange(52.52f, 13.405f, 5000f))
    .Execute();

// Reusable constraint
var area = new GeoCoordinateConstraint(52.52f, 13.405f, 5000f);
var results = await context.Stores.Query()
    .Where(s => s.Location.IsWithinGeoRange(area))
    .Execute();

// Combined with other filters
var results = await context.Stores.Query()
    .Where(s => s.Location.IsWithinGeoRange(52.52f, 13.405f, 5000f))
    .Where(s => s.IsOpen)
    .Execute();
```

The entity property must be declared as `GeoCoordinate` with `[Index(Filterable = true)]`:

```csharp
[Index(Filterable = true)]
[Property(DataType.GeoCoordinate)]
public GeoCoordinate Location { get; set; }
```

### Semantic Search (NearText)

```csharp
var results = await context.Products.Query()
    .NearText("computer input device")
    .Limit(10)
    .Execute();

// With specific vector
var results = await context.Products.Query()
    .NearText("computer input", vector: p => p.TextEmbedding)
    .Limit(10)
    .Execute();

// With threshold
var results = await context.Products.Query()
    .NearText("mouse", certainty: 0.8f)
    .Execute();
```

### Vector Search (NearVector)

```csharp
float[] embedding = GetEmbeddingFromSomewhere();

var results = await context.Products.Query()
    .NearVector(embedding)
    .Limit(10)
    .Execute();
```

### Object Similarity Search (NearObject)

Find objects similar to an existing object:

```csharp
var results = await context.Products.Query()
    .NearObject(existingProductId)
    .Limit(10)
    .Execute();

// With specific vector
var results = await context.Products.Query()
    .NearObject(existingProductId, vector: p => p.TextEmbedding)
    .Execute();
```

### Hybrid Search

Combines BM25 keyword search with vector search:

```csharp
var results = await context.Products.Query()
    .Hybrid("wireless mouse")
    .Limit(10)
    .Execute();

// Adjust alpha (0 = keyword only, 1 = vector only)
var results = await context.Products.Query()
    .Hybrid("wireless mouse", alpha: 0.7f)
    .Execute();
```

### BM25 Keyword Search

Pure keyword search without any vector component:

```csharp
var results = await context.Products.Query()
    .BM25("wireless mouse")
    .Limit(10)
    .Execute();

// Search specific properties
var results = await context.Products.Query()
    .BM25("wireless", properties: [nameof(Product.Name)])
    .Execute();
```

### Near Media Search

Search by image, audio, video, or other media:

```csharp
var imageBytes = File.ReadAllBytes("photo.jpg");
var base64Image = Convert.ToBase64String(imageBytes);

var results = await context.Products.Query()
    .NearMedia(base64Image, NearMediaType.Image)
    .Limit(10)
    .Execute();
```

### Pagination

```csharp
// Offset-based (for small result sets)
var page2 = await context.Products.Query()
    .NearText("mouse")
    .Limit(10)
    .Offset(10)
    .Execute();

// Cursor-based (for large result sets)
var nextPage = await context.Products.Query()
    .NearText("mouse")
    .Limit(10)
    .After(lastResultId)
    .Execute();

// Auto limit
var results = await context.Products.Query()
    .NearText("mouse")
    .AutoLimit(3)
    .Execute();
```

### Sorting

The preferred API uses `OrderBy`/`OrderByDescending` with `ThenBy`/`ThenByDescending` for chaining. The lower-level `Sort()` method is also available.

```csharp
// Ascending
var results = await context.Products.Query()
    .OrderBy(p => p.Price)
    .Limit(20)
    .Execute();

// Descending
var results = await context.Products.Query()
    .OrderByDescending(p => p.Price)
    .Execute();

// Multi-column sort
var results = await context.Products.Query()
    .OrderBy(p => p.Category)
    .ThenByDescending(p => p.Price)
    .Execute();

// Low-level Sort() method (same effect)
var results = await context.Products.Query()
    .Sort(p => p.Price, descending: true)
    .Execute();
```

### Reranking

Re-order results for better relevance (requires `[Reranker<T>]` on collection).

Use the string overload `Rerank(property, query?)` to specify which property to rerank on, plus an optional rerank query:

```csharp
// Rerank on a property with a specific query
var results = await context.Products.Query()
    .NearText("electronics")
    .Rerank("name", "wireless headphones")
    .Limit(10)
    .Execute();

// Rerank on a property without a separate query (uses search query)
var results = await context.Products.Query()
    .Hybrid("laptop")
    .Rerank("description")
    .Execute();

// Full control via the Rerank object
var results = await context.Products.Query()
    .NearText("wireless mouse")
    .Rerank(new Rerank { Property = "name", Query = "gaming mouse" })
    .Limit(10)
    .Execute();
```

### Including Vectors

```csharp
var results = await context.Products.Query()
    .WithVectors(p => p.TextEmbedding, p => p.MultiModalEmbedding)
    .Execute();

foreach (var result in results)
{
    Console.WriteLine($"Vector: {result.Object.TextEmbedding?.Length} dimensions");
}
```

### Multi-Vector Targeting

When your collection has multiple named vectors, use `.Targets()` to control how they're combined during vector search. This works with `NearText`, `NearVector`, `NearObject`, and `Hybrid`.

#### Combining Named Vectors (NearText/NearObject/Hybrid)

```csharp
// Sum - Add vectors together
var results = await context.Products.Query()
    .NearText("wireless headphones")
    .Targets(t => t.Sum(p => p.TextEmbedding, p => p.DescriptionEmbedding))
    .Execute();

// Average - Average vectors
var results = await context.Products.Query()
    .NearText("mouse")
    .Targets(t => t.Average(p => p.TextEmbedding, p => p.DescriptionEmbedding))
    .Execute();

// Minimum - Use closest distance
var results = await context.Products.Query()
    .NearText("laptop")
    .Targets(t => t.Minimum(p => p.TextEmbedding, p => p.DescriptionEmbedding))
    .Execute();

// Manual weights - Custom weighting
var results = await context.Products.Query()
    .NearText("keyboard")
    .Targets(t => t.ManualWeights(
        (p => p.TextEmbedding, 0.7),
        (p => p.DescriptionEmbedding, 0.3)
    ))
    .Execute();

// Relative score - Score-based weighting
var results = await context.Products.Query()
    .NearObject(existingProductId)
    .Targets(t => t.RelativeScore(
        (p => p.TextEmbedding, 2.0),
        (p => p.DescriptionEmbedding, 1.0)
    ))
    .Execute();
```

#### Per-Target Vectors (NearVector)

When searching with explicit vectors, provide a different vector for each target:

```csharp
var textVec = new float[] { 0.1f, 0.2f, 0.3f };
var descVec = new float[] { 0.4f, 0.5f, 0.6f };

// Sum with per-target vectors
var results = await context.Products.Query()
    .NearVector()
    .Targets(t => t.Sum(
        (p => p.TextEmbedding, textVec),
        (p => p.DescriptionEmbedding, descVec)
    ))
    .Execute();

// Manual weights with per-target vectors
var results = await context.Products.Query()
    .NearVector()
    .Targets(t => t.ManualWeights(
        (p => p.TextEmbedding, textVec, 0.7),
        (p => p.DescriptionEmbedding, descVec, 0.3)
    ))
    .Execute();
```

#### Hybrid Search with Multi-Vector

```csharp
// Hybrid with named vector targeting
var results = await context.Products.Query()
    .Hybrid("wireless mouse")
    .Targets(t => t.Sum(p => p.TextEmbedding, p => p.ImageEmbedding))
    .Execute();

// Hybrid with per-target vectors
var textVec = GetTextEmbedding("wireless mouse");
var imageVec = GetImageEmbedding(productImage);

var results = await context.Products.Query()
    .Hybrid("wireless mouse")
    .Targets(t => t.ManualWeights(
        (p => p.TextEmbedding, textVec, 0.6),
        (p => p.ImageEmbedding, imageVec, 0.4)
    ))
    .Execute();
```

**Combination Methods:**

- `Sum()` — Add vector distances
- `Average()` — Average vector distances
- `Minimum()` — Use closest distance (best match wins)
- `ManualWeights()` — Apply explicit weights to each vector
- `RelativeScore()` — Score-based weighting

### Including References

```csharp
var results = await context.Products.Query()
    .WithReferences(p => p.Category)
    .Execute();
```

### Including Metadata

```csharp
var results = await context.Products.Query()
    .Hybrid("mouse")
    .WithMetadata(MetadataQuery.Score)
    .Execute();

foreach (var result in results)
    Console.WriteLine($"{result.Object.Name}: {result.Metadata?.Score}");
```

### Selecting Specific Properties

```csharp
var results = await context.Products.Query()
    .Select(p => p.Name, p => p.Price)
    .Execute();
```

### Query Projections

Project query results to a different type. Projections support properties, vectors, metadata, ID mapping, references, and convention-based static hooks.

#### Projection Attributes

| Attribute | Purpose |
| --------- | ------- |
| `[QueryProjection<TSource>]` | Marks this type as a projection of `TSource` |
| `[WeaviateUUID]` | Maps the Weaviate object ID to this property |
| `[MapFrom("SourceName")]` | Maps from a differently-named source property |
| `[Vector(VectorName = "...")]` | Includes the named vector in results |
| `[Reference]` | Includes this reference in results (no `.WithReferences()` needed) |
| `[MetadataProperty]` | Injects query metadata (score, distance, etc.) |

```csharp
[QueryProjection<Product>]
public class ProductSummary
{
    [WeaviateUUID]
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    [MapFrom(nameof(Product.Price))]
    public decimal Cost { get; set; }

    [Vector(VectorName = "textEmbedding")]
    public float[]? Vector { get; set; }

    [Reference]
    public Category? Category { get; set; }

    [MetadataProperty]
    public double? Score { get; set; }
}

var summaries = await context.Products.Project<ProductSummary>()
    .NearText("mouse")
    .Execute();
```

#### Convention-Based Static Hooks

Projection types can declare static methods (discovered by name) to customize default query settings when that projection is used:

| Method | Signature | Effect |
| ------ | --------- | ------ |
| `ConfigureNearText` | `static NearTextConfig ConfigureNearText(NearTextConfig c)` | Default NearText settings |
| `ConfigureNearVector` | `static NearVectorConfig ConfigureNearVector(NearVectorConfig c)` | Default NearVector settings |
| `ConfigureHybrid` | `static HybridConfig ConfigureHybrid(HybridConfig c)` | Default Hybrid settings |
| `ConfigureNearObject` | `static NearObjectConfig ConfigureNearObject(NearObjectConfig c)` | Default NearObject settings |
| `ConfigureNearMedia` | `static NearMediaConfig ConfigureNearMedia(NearMediaConfig c)` | Default NearMedia settings |
| `ConfigureVectorTargets` | `static TargetVectorBuilder<T> ConfigureVectorTargets(TargetVectorBuilder<T> b)` | Multi-vector combination strategy |
| `ConfigureSearch` | `static void ConfigureSearch(QueryConfig<T> q)` | Apply query options (Where, Limit, Offset, OrderBy, etc.) at entity or projection level |

```csharp
[QueryProjection<Product>]
public class ProductSummary
{
    public string Name { get; set; } = "";

    [MetadataProperty]
    public double? Score { get; set; }

    // Applied automatically when this projection is used with NearText
    public static NearTextConfig ConfigureNearText(NearTextConfig c)
    {
        c.Certainty = 0.7f;
        return c;
    }

    // Applied automatically when this projection is used
    public static void ConfigureSearch(QueryConfig<Product> q)
    {
        q.Where(p => p.IsActive)
         .Limit(10u)
         .WithMetadata(MetadataQuery.Score);
    }
}
```

#### ConfigureSearch Precedence

The `ConfigureSearch` hook can be defined at multiple levels with clear precedence:

**Precedence (highest to lowest):**
1. **Explicit query calls** - Always wins
2. **Projection-level** - Overrides entity defaults
3. **Entity-level** - Provides base defaults

```csharp
// Entity-level default
[WeaviateCollection("Products")]
public class Product
{
    public string Name { get; set; }
    public bool IsActive { get; set; }

    public static void ConfigureSearch(QueryConfig<Product> q)
    {
        q.Where(p => p.IsActive)
         .Limit(100u)
         .WithMetadata(MetadataQuery.Score); // Default for all Product queries
    }
}

// Projection-level override
[QueryProjection<Product>]
public class ProductSummary
{
    public string Name { get; set; }

    public static void ConfigureSearch(QueryConfig<Product> q)
    {
        q.Limit(10u); // Overrides entity's 100u when using this projection
    }
}

// Usage:
// Uses Product.ConfigureSearch (limit 100, filtered to active)
var allProducts = await context.Products.Query().Execute();

// Uses ProductSummary.ConfigureSearch (limit 10, no filter)
var summaries = await context.Products.Query<ProductSummary>().Execute();

// Explicit call overrides everything (limit 5)
var few = await context.Products.Query().Project<ProductSummary>().Limit(5u).Execute();
```

### Result Helpers

`Execute()` returns `IEnumerable<QueryResult<T>>` with both `.Object` and `.Metadata`:

```csharp
var results = await context.Products.Query()
    .NearText("mouse")
    .WithMetadata(MetadataQuery.Score | MetadataQuery.Distance)
    .Execute();

// Extract just objects
var products = results.Objects();

// Filter by metadata
var relevant = results.WithMetadata(m => m.Score > 0.5);

// Order by score/distance
var ranked = results.OrderByScore();
var nearest = results.OrderByDistance();
```

---

## Generative AI (RAG)

If your collection has a `[Generative<T>]` module configured, use `.Generate()` on any query to add AI generation.

### Single Prompt

Generate a response per result:

```csharp
var results = await context.Products.Query()
    .NearText("wireless mouse")
    .Limit(5)
    .Generate(singlePrompt: "Write a one-sentence product description for this item")
    .Execute();

foreach (var r in results)
    Console.WriteLine($"{r.Object.Name}: {r.Generative?[0]}");
```

### Grouped Task

Generate a single response using all results as context:

```csharp
var results = await context.Products.Query()
    .NearText("wireless mouse")
    .Limit(5)
    .Generate(groupedTask: "Compare these products and recommend the best one")
    .Execute();

// Grouped result is on the response itself
Console.WriteLine(results.Generative?[0]);
```

### Both Prompts

```csharp
var results = await context.Products.Query()
    .NearText("wireless mouse")
    .Limit(5)
    .Generate(
        singlePrompt: "Summarize this product",
        groupedTask: "Which product is the best value?"
    )
    .Execute();
```

### Custom Provider Override

```csharp
var results = await context.Products.Query()
    .NearText("wireless mouse")
    .Limit(5)
    .Generate(singlePrompt: "Describe this product")
    .WithProvider(new GenerativeProvider { /* ... */ })
    .Execute();
```

---

## Aggregations

Define an aggregate projection class using `Aggregate` types from `Weaviate.Client.Models.Typed` and specify which metrics to compute using the `[Metrics]` attribute:

```csharp
using Weaviate.Client.Models.Typed;
using Weaviate.Client.Managed.Attributes;

public class ProductStats
{
    [Metrics(Metric.Number.Mean, Metric.Number.Min, Metric.Number.Max, Metric.Number.Count, Metric.Number.Sum)]
    public Aggregate.Number Price { get; set; }

    [Metrics(Metric.Integer.Sum, Metric.Integer.Count)]
    public Aggregate.Integer Stock { get; set; }

    [Metrics(Metric.Boolean.TotalTrue, Metric.Boolean.PercentageTrue)]
    public Aggregate.Boolean InStock { get; set; }
}
```

Execute aggregation (`.Execute()` is optional - directly awaitable):

```csharp
var stats = await context.Products.Aggregate()
    .WithMetrics<ProductStats>();

Console.WriteLine($"Average price: {stats.Properties.Price.Mean:C}");
Console.WriteLine($"Min: {stats.Properties.Price.Min:C}");
Console.WriteLine($"Max: {stats.Properties.Price.Max:C}");
Console.WriteLine($"Total: {stats.Properties.Price.Count} products");
```

### With Filters

```csharp
var stats = await context.Products.Aggregate()
    .WithMetrics<ProductStats>()
    .Where(p => p.InStock);
```

### Grouping

#### Aggregate GroupBy

Group aggregate metrics by property value:

```csharp
// Execute() is optional - directly awaitable
var grouped = await context.Products.Aggregate()
    .WithMetrics<ProductStats>()
    .GroupBy(p => p.Category);
```

#### Search GroupBy

Group search results by property value (buckets objects by shared property):

```csharp
// Execute() is optional - directly awaitable
var response = await context.Products.Query()
    .NearText("laptop")
    .Where(p => p.InStock)
    .GroupBy(p => p.Category, numberOfGroups: 5, objectsPerGroup: 3);

foreach (var group in response.Groups.Values)
{
    Console.WriteLine($"{group.Name}: {group.Count} objects");
    foreach (var item in group.Objects)
    {
        Console.WriteLine($"  - {item.Object.Name}");
    }
}
```

### Context-Level Aggregation

Use `context.Aggregate<TProjection>()` with `[QueryAggregate<T>]` to aggregate without specifying the collection:

```csharp
[QueryAggregate<Product>]
public class ProductStats
{
    [Metrics(Metric.Number.Mean, Metric.Number.Min, Metric.Number.Max, Metric.Number.Count)]
    public Aggregate.Number Price { get; set; }
}

// Collection inferred from [QueryAggregate<Product>]
var stats = await context.Aggregate<ProductStats>();
Console.WriteLine($"Average price: {stats.Properties.Price.Mean:C}");

// With filter (uses raw Filter since model type is only known at runtime)
var filtered = await context.Aggregate<ProductStats>()
    .Where(Filter.Property("inStock").Equal(true));

// Grouped - Execute() is optional
var grouped = await context.Aggregate<ProductStats>()
    .GroupBy("category");
```

### Single Metric Extraction

Instead of using full `Aggregate.*` types, you can extract individual metrics to scalar properties using the `[Metrics("propertyName", Metrics.xxx.Metric)]` syntax:

```csharp
[QueryAggregate<Product>]
public class SimpleStats
{
    // Extract single metrics to scalar properties
    [Metrics("price", Metric.Number.Mean)]
    public double? AveragePrice { get; set; }

    [Metrics("price", Metric.Number.Count)]
    public long? PriceCount { get; set; }

    [Metrics("stock", Metric.Integer.Sum)]
    public long? TotalStock { get; set; }

    [Metrics("createdAt", Metric.Date.Min)]
    public DateTime? EarliestDate { get; set; }
}

var stats = await context.Aggregate<SimpleStats>();
Console.WriteLine($"Average price: ${stats.AveragePrice:F2}");
Console.WriteLine($"Total stock: {stats.TotalStock}");
Console.WriteLine($"Earliest product: {stats.EarliestDate}");
```

#### Naming Convention

When using single-metric extraction, it's recommended to follow the naming convention: **PropertyName + MetricSuffix**. The analyzer will warn (WEAVIATE008) if this convention isn't followed:

```csharp
[QueryAggregate<Product>]
public class ProductStats
{
    // Follows convention: "price" + "Mean" = "PriceMean"
    [Metrics("price", Metric.Number.Mean)]
    public double? PriceMean { get; set; }

    // Follows convention: "price" + "Sum" = "PriceSum"
    [Metrics("price", Metric.Number.Sum)]
    public double? PriceSum { get; set; }

    // Follows convention: "stock" + "Count" = "StockCount"
    [Metrics("stock", Metric.Integer.Count)]
    public long? StockCount { get; set; }

    // Warning: Does not follow convention (should be "PriceMinimum")
    [Metrics("price", Metric.Number.Min)]
    public double? MinPrice { get; set; }  // ⚠️ WEAVIATE008
}
```

**Available metric suffixes by type:**

- **Number**: Mean, Median, Mode, Maximum, Minimum, Count, Sum, Type
- **Integer**: Mean, Median, Mode, Maximum, Minimum, Count, Sum, Type
- **Text**: Count, Type, TopOccurrences
- **Boolean**: Count, Type, TotalTrue, TotalFalse, PercentageTrue, PercentageFalse
- **Date**: Count, Type, Minimum, Maximum, Median, Mode

This convention improves code readability and makes the property-to-metric mapping obvious. However, it's only a warning - you can use any property name if you prefer (like `AveragePrice` instead of `PriceMean`).
```

The analyzer validates that:
- Only one metric is specified (no combined flags with `|`)
- The scalar type matches the metric (e.g., `Mean` → `double?`, `Count` → `long?`)

See [ATTRIBUTES.md](ATTRIBUTES.md#metricsattribute) for the complete type mapping table.

---

## Count and Iterator

### Count

Get the total number of objects in a collection:

```csharp
// Via context
var count = await context.Count<Product>();

// Via CollectionSet
var count = await context.Products.Count();

// Via ManagedCollection
var count = await products.Count();
```

### Iterator

Stream all objects with `IAsyncEnumerable<T>`:

```csharp
// Via context
await foreach (var product in context.Iterator<Product>())
    Console.WriteLine(product.Name);

// Via CollectionSet
await foreach (var product in context.Products.Iterator())
    Console.WriteLine(product.Name);

// With cursor (resume iteration)
await foreach (var product in context.Products.Iterator(after: lastSeenId))
    Console.WriteLine(product.Name);

// Custom page size
await foreach (var product in products.Iterator(cacheSize: 200))
    Console.WriteLine(product.Name);
```

---

## Batch Operations

### Same-Collection Chaining (PendingInsert)

`Insert(params T[])` returns a `PendingInsert<T>` that can be directly awaited or chained. Each `.Insert()` call in the chain runs after the previous one completes, which is useful when later items reference earlier items in the same collection.

```csharp
// Single batch — directly awaitable
await context.Articles.Insert(article1, article2);

// Chained — second batch runs after first (useful for same-collection references)
await context.Articles
    .Insert(parentArticle)
    .Insert(childArticle)   // may reference parentArticle
    .Execute(cancellationToken);
```

### Cross-Collection Batch (PendingBatch)

`context.Batch()` returns a `PendingBatch` that collects operations across multiple collections and executes them in dependency order using topological sort. If `Article` has a `[Reference]` to `Category`, categories are inserted before articles automatically.

```csharp
var batch = context.Batch();

batch.Insert(new Category { Name = "Technology" });
batch.Insert(new Article { Title = "Post 1", /* ... */ });
batch.Insert(new Article { Title = "Post 2", /* ... */ });

await batch.Execute(cancellationToken);
```

### PendingDelete

`Delete(params T[])` and `Delete(params Guid[])` return `PendingDelete<T>`, which accumulates IDs and sends them in a single batch call. Directly awaitable or chainable.

```csharp
// Directly awaitable
await context.Products.Delete(p1, p2, p3);

// Chained accumulation
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

### PendingUpdate

`Update(params T[])` returns `PendingUpdate<T>` (directly awaitable):

```csharp
// Update multiple entities — directly awaitable
var updated = await context.Products.Update(p1, p2, p3);
```

---

## Migrations

### Check Migration (Dry Run)

```csharp
var plan = await context.Products.CheckMigrate();

if (plan.HasChanges)
{
    foreach (var change in plan.Changes)
    {
        var warning = change.IsBreaking ? "BREAKING" : "OK";
        Console.WriteLine($"  {warning} {change.Type}: {change.Description}");
    }
}
```

### Execute Migration

```csharp
// Safe migration (blocks breaking changes)
await context.Products.Migrate();

// Allow breaking changes
await context.Products.Migrate(allowBreakingChanges: true);
```

### Migrate All Collections

```csharp
// Check all pending migrations (includes orphaned collection detection)
var pending = await context.GetPendingMigrations();

// Check for orphans (server collections not registered in context)
foreach (var (name, plan) in pending.Where(p => p.Value.IsOrphaned))
    Console.WriteLine($"Orphaned: {name}");

// Migrate all (safe changes only)
await context.Migrate();

// Allow breaking schema changes
await context.Migrate(allowBreakingChanges: true);

// Also delete orphaned collections not in the context
await context.Migrate(destructive: true);
```

See [MIGRATIONS.md](MIGRATIONS.md) for the full migration guide.

---

## Multi-Tenancy

### Enable Multi-Tenancy

```csharp
[WeaviateCollection("Products",
    MultiTenancyEnabled = true,
    AutoTenantCreation = true,
    AutoTenantActivation = true)]
public class Product
{
    [Property]
    public string Name { get; set; } = "";
}
```

### Tenant Scoping with Context

```csharp
// Create a tenant-scoped context
var acmeContext = context.ForTenant("acme-corp");

// All operations on this context are isolated to the tenant
await acmeContext.Insert(new Product { Name = "Acme Widget" });
var acmeProducts = await acmeContext.Products.Query().Execute();
```

### Tenant Scoping with ManagedCollection

```csharp
var products = await client.Collections.CreateManaged<Product>();

var acme = products.WithTenant("acme-corp");
var globex = products.WithTenant("globex-inc");

await acme.Insert(new Product { Name = "Acme Widget" });
await globex.Insert(new Product { Name = "Globex Gadget" });

// Queries return only tenant's data
var acmeProducts = await acme.Query().Execute();
```

---

## Consistency Levels

Control read/write consistency:

```csharp
// Context-level
var strongContext = context.WithConsistencyLevel(ConsistencyLevel.All);
await strongContext.Insert(new Product { Name = "Critical Item" });

// ManagedCollection-level
var strongProducts = products.WithConsistencyLevel(ConsistencyLevel.Quorum);
await strongProducts.Insert(new Product { Name = "Important Item" });
```

Available levels:

- `ConsistencyLevel.One` — Fastest, writes to one node
- `ConsistencyLevel.Quorum` — Writes to majority of nodes
- `ConsistencyLevel.All` — Writes to all nodes (strongest guarantee)

---

## References

### Define References

```csharp
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Title { get; set; } = "";

    // Single reference (full object) — explicit loading (default)
    [Reference]
    public Category? Category { get; set; }

    // Eager loading — auto-included in every query
    [Reference(Loading = ReferenceLoadingStrategy.Eager)]
    public Category? Category { get; set; }

    // Multi-reference
    [Reference]
    public List<Article>? RelatedArticles { get; set; }

    // ID-only reference — Target= required because Guid? carries no type info
    [Reference(Target = typeof(Author))]
    public Guid? AuthorId { get; set; }
}

[WeaviateCollection("Category")]
public class Category
{
    [Property]
    public string Name { get; set; } = "";
}
```

### Insert with References

```csharp
var categoryId = await categories.Insert(new Category { Name = "Technology" });

await articles.Insert(new Article
{
    Title = "New Tech Article",
    AuthorId = existingAuthorId
});
```

### Query with References

```csharp
var results = await context.Articles.Query()
    .WithReferences(a => a.Category)
    .Execute();

foreach (var result in results)
{
    Console.WriteLine($"{result.Object.Title} - {result.Object.Category?.Name}");
}
```

### Managing References (AddReference, ReplaceReferences, DeleteReference)

Use `AddReference`, `ReplaceReferences`, and `DeleteReference` on `CollectionSet<T>` (or on the context directly) to create, replace, and remove cross-references after objects have been inserted.

`AddReference` returns a `PendingReference<T>` that is directly awaitable or chainable. All links accumulated in a chain are sent in a single `ReferenceAddMany` call.

```csharp
// Add a single reference — directly awaitable
await context.Articles.AddReference(article, a => a.Category, techCategory);

// Chained — all links sent in one call
await context.Articles
    .AddReference(article1, a => a.Category, techCategory)
    .AddReference(article2, a => a.Category, scienceCategory)
    .Execute(cancellationToken);

// Mix entity and ID targets
await context.Articles
    .AddReference(article, a => a.Category, category)
    .AddReference(article, a => a.AuthorId, existingAuthorGuid)
    .Execute(cancellationToken);

// Via context (when you don't have the CollectionSet reference handy)
await context.AddReference<Article, Category>(article, a => a.Category, techCategory);

// Replace all values for a reference property with a new set
await context.Articles.ReplaceReferences(article, a => a.Category, newCategoryId);

// Replace a multi-reference property
await context.Articles.ReplaceReferences(article, a => a.Tags, tag1Id, tag2Id, tag3Id);

// Remove a specific link by entity or ID
await context.Articles.DeleteReference(article, a => a.Category, oldCategoryId);
await context.Articles.DeleteReference(article, a => a.Category, oldCategory);
```

---

## Nested Objects

### Define Nested Types

```csharp
[WeaviateCollection("BlogPosts")]
public class BlogPost
{
    [Property]
    public string Title { get; set; } = "";

    // Single nested object (type inferred)
    [Property(DataType.Object)]
    public Author Author { get; set; } = new();

    // Nested array (type inferred from List<T>)
    [Property(DataType.ObjectArray)]
    public List<Comment> Comments { get; set; } = new();
}

// Nested types don't need [WeaviateCollection]
public class Author
{
    [Property]
    public string Name { get; set; } = "";

    [Property]
    public string Email { get; set; } = "";
}

public class Comment
{
    [Property]
    public string Text { get; set; } = "";

    [Property]
    public DateTime PostedAt { get; set; }
}
```

### Nested vs References

| Aspect | Nested Objects | References |
| --- | --- | --- |
| Storage | Embedded in parent | Separate collection |
| Queries | Always included | Explicit `.WithReferences()` |
| Reuse | Duplicated per parent | Shared across parents |
| Updates | Must update parent | Update independently |
| Use when | Data belongs to parent | Data shared/reusable |

---

## Collection Configuration Hooks

### CollectionConfigMethod

Use `CollectionConfigMethod` on `[WeaviateCollection]` to intercept and customize collection creation parameters at class level:

```csharp
[WeaviateCollection("Products", CollectionConfigMethod = nameof(OnConfig))]
public class Product
{
    [Property]
    public string Name { get; set; } = "";

    public static void OnConfig(OnCollectionConfig config)
    {
        config.OnCreate(createParams =>
        {
            // Modify collection creation params before Weaviate receives them
            return createParams;
        });
    }
}
```

### OnCollectionConfig.GlobalOnCreate

The static `OnCollectionConfig.GlobalOnCreate` interceptor is applied to **every** collection created during the process lifetime. It runs after per-class hooks. This is useful in test infrastructure to isolate tests by giving each run unique collection names:

```csharp
// Prefix all collection names with a test-run ID
var testId = Guid.NewGuid().ToString("N")[..8];
OnCollectionConfig.GlobalOnCreate = createParams =>
{
    createParams.Name = $"{createParams.Name}_{testId}";
    return createParams;
};

// Remember to reset after the test
OnCollectionConfig.GlobalOnCreate = null;
```

Because `GlobalOnCreate` is static, integration tests that rely on it should be serialized (e.g., via xUnit's `[Collection]` attribute).

### ConfigMethod Escape Hatch

When C# attribute limitations prevent full configuration, use `ConfigMethod` on individual attribute declarations:

```csharp
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Content { get; set; } = "";

    [Vector<Vectorizer.Text2VecOpenAI>(
        Model = "text-embedding-3-small",
        ConfigMethod = nameof(ConfigureVector)
    )]
    public float[]? Embedding { get; set; }

    // Receives the pre-built config with attribute values already applied
    public static Vectorizer.Text2VecOpenAI ConfigureVector(
        string vectorName,
        Vectorizer.Text2VecOpenAI prebuilt)
    {
        prebuilt.VectorizeCollectionName = false;
        return prebuilt;
    }
}
```

---

## Admin Operations

The `WeaviateAdmin` facade is accessible via `context.Admin`:

```csharp
// Health checks
var isLive = await context.Admin.IsLive();
var isReady = await context.Admin.IsReady();
await context.Admin.WaitUntilReady(TimeSpan.FromSeconds(30));

// Server metadata
var meta = await context.Admin.GetMeta();
Console.WriteLine($"Weaviate version: {meta.Version}");

// Sub-clients
context.Admin.Backup   // BackupClient
context.Admin.Users    // UsersClient
context.Admin.Roles    // RolesClient
context.Admin.Groups   // GroupsClient
context.Admin.Cluster  // ClusterClient
context.Admin.Aliases  // AliasClient
```
