# Getting Started

Get up and running with Weaviate.Client.Managed in 5 minutes.

## Prerequisites

- .NET 8.0+ or .NET 9.0+
- A running Weaviate instance (local or cloud)

## Installation

```bash
dotnet add package Weaviate.Client.Managed
```

## Step 1: Define Your Model

Create a C# class with Managed attributes:

```csharp
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;

[WeaviateCollection("Products")]
public class Product
{
    [WeaviateUUID]
    public Guid Id { get; set; }

    [Property]
    [Index(Filterable = true, Searchable = true)]
    public string Name { get; set; } = "";

    [Property]
    public string Description { get; set; } = "";

    [Property]
    [Index(Filterable = true, RangeFilters = true)]
    public decimal Price { get; set; }

    [Property]
    [Index(Filterable = true)]
    public bool InStock { get; set; }

    // Vector for semantic search
    [Vector<Vectorizer.Text2VecOpenAI>(
        Model = "text-embedding-ada-002",
        SourceProperties = [nameof(Name), nameof(Description)]
    )]
    public float[]? Embedding { get; set; }
}
```

## Step 2: Create a Context (Recommended)

The `WeaviateContext` provides an EF Core-like experience with `CollectionSet<T>` properties:

```csharp
using Weaviate.Client.Managed.Context;

public class StoreContext : WeaviateContext
{
    public StoreContext(WeaviateClient client) : base(client) { }

    public CollectionSet<Product> Products { get; set; } = null!;
}
```

### Connect and create the collection

```csharp
using Weaviate.Client;
using Weaviate.Client.Managed.Extensions;

var client = new WeaviateClient(new WeaviateConfig
{
    Host = "localhost:8080"
});

// Create the collection in Weaviate from your model's attributes
await client.Collections.CreateFromClass<Product>();

// Create your context
var context = new StoreContext(client);
```

## Step 3: Insert Data

```csharp
// Single insert — ID is automatically assigned back to the entity
var product = new Product
{
    Name = "Wireless Mouse",
    Description = "Ergonomic wireless mouse with long battery life",
    Price = 29.99m,
    InStock = true
};
await context.Insert(product);
Console.WriteLine($"Inserted with ID: {product.Id}");

// Insert multiple
await context.Products.Insert(
    new Product { Name = "Keyboard", Description = "Mechanical keyboard", Price = 79.99m, InStock = true },
    new Product { Name = "Monitor", Description = "27-inch 4K display", Price = 399.99m, InStock = false }
);
```

## Step 4: Query Data

```csharp
// Wait for data to be indexed
await Task.Delay(500);

// Filter query
var results = await context.Products.Query()
    .Where(p => p.Price > 50)
    .Where(p => p.InStock)
    .Limit(10)
    .Execute();

// Each result wraps the entity, its ID, and optional metadata
foreach (var result in results)
{
    Console.WriteLine($"{result.Object.Name}: ${result.Object.Price}");
}

// Extract just objects when metadata not needed
var products = results.Objects();

// Semantic search with scores
var mouseResults = await context.Products.Query()
    .NearText("computer input device")
    .WithMetadata(MetadataQuery.Score)
    .Limit(5)
    .Execute();

foreach (var result in mouseResults)
{
    Console.WriteLine($"{result.Object.Name}: {result.Metadata?.Score:F3}");
}

// Hybrid search (combines keyword + semantic)
var hybridResults = await context.Products.Query()
    .Hybrid("wireless mouse", alpha: 0.5f)
    .Where(p => p.InStock)
    .Limit(10)
    .Execute();
```

## Step 5: Update and Delete

```csharp
// Find by ID
var found = await context.Products.Find(product.Id);

// Update (uses entity's [WeaviateUUID] property)
found.Price = 24.99m;
await context.Update(found);

// Delete by ID
await context.Delete<Product>(product.Id);
```

## Step 6: Batch Operations

Insert multiple objects in one call, or group operations across collections:

```csharp
// Batch insert — directly awaitable
await context.Products.Insert(
    new Product { Name = "Widget A", Price = 9.99m, InStock = true },
    new Product { Name = "Widget B", Price = 14.99m, InStock = true }
);

// Cross-collection batch with dependency ordering
var batch = context.Batch();
batch.Insert(new Category { Name = "Gadgets" });
batch.Insert(new Product { Name = "Widget C", Price = 19.99m, InStock = true });
await batch.Execute();

// Delete
await context.Delete<Product>(oldProductId);
```

## Step 7: Generate (RAG)

If your collection has a generative module configured, use `.Generate()` on any query:

```csharp
var ragResults = await context.Products.Query()
    .NearText("wireless mouse")
    .Limit(5)
    .Generate(singlePrompt: "Write a one-sentence product description for this item")
    .Execute();

foreach (var r in ragResults)
    Console.WriteLine($"{r.Object.Name}: {r.Generative?[0]}");
```

---

## Alternative: ManagedCollection\<T\>

If you don't need a full context, use `ManagedCollection<T>` directly:

```csharp
// Creates collection and returns a managed wrapper
var products = await client.Collections.CreateManaged<Product>();

// Same operations, just without context
var id = await products.Insert(new Product { Name = "Widget", Price = 9.99m });
var results = await products.Query().NearText("widget").Execute();
var count = await products.Count();

await foreach (var p in products.Iterator())
    Console.WriteLine(p.Name);
```

---

## What's Next?

- [Complete Guide](GUIDE.md) - Full feature coverage
- [Attributes Reference](ATTRIBUTES.md) - All attributes explained
- [Advanced Patterns](ADVANCED.md) - Multi-tenancy, RAG, multi-vector
- [Architecture](ARCHITECTURE.md) - Understanding the system design
- [API Reference](API_REFERENCE.md) - Complete API surface

## Common Patterns

### Type Inference

Data types are automatically inferred from C# types:

```csharp
[Property]
public string Name { get; set; }      // → DataType.Text

[Property]
public int Count { get; set; }        // → DataType.Int

[Property]
public decimal Price { get; set; }    // → DataType.Number

[Property]
public bool Active { get; set; }      // → DataType.Boolean

[Property]
public DateTime Created { get; set; } // → DataType.Date

[Property]
public List<string> Tags { get; set; } // → DataType.TextArray
```

### Named Vectors

Property name becomes the vector name:

```csharp
[Vector<Vectorizer.Text2VecOpenAI>]
public float[]? TitleEmbedding { get; set; }  // Vector name: "titleEmbedding"

[Vector<Vectorizer.Text2VecCohere>]
public float[]? ContentEmbedding { get; set; } // Vector name: "contentEmbedding"
```

### References

Link to other collections:

```csharp
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Title { get; set; } = "";

    [Reference]
    public Category? Category { get; set; }
}

[WeaviateCollection("Category")]
public class Category
{
    [Property]
    public string Name { get; set; } = "";
}
```
