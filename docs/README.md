# Weaviate.Client.Managed Documentation

Type-safe, attribute-driven Weaviate client for .NET. Define schemas with C# attributes, query with LINQ-style expressions.

## Quick Navigation

| Document | Description |
|----------|-------------|
| [Getting Started](GETTING_STARTED.md) | 5-minute quickstart |
| [Guide](GUIDE.md) | Complete user guide |
| [Architecture](ARCHITECTURE.md) | System design and decisions |
| [API Reference](API_REFERENCE.md) | Complete API surface |

### Reference

| Document | Description |
|----------|-------------|
| [Attributes](ATTRIBUTES.md) | All 14 attributes with properties and examples |
| [Mapping](MAPPING.md) | Type conversion, vectors, references |
| [Migrations](MIGRATIONS.md) | Schema evolution and breaking changes |

### Advanced & Operations

| Document | Description |
|----------|-------------|
| [Advanced](ADVANCED.md) | Multi-tenancy, multi-vector, RAG, ConfigMethod |
| [Best Practices](BEST_PRACTICES.md) | Collection design, performance, testing |
| [Dependency Injection](DEPENDENCY_INJECTION.md) | DI patterns for ASP.NET Core |
| [Roadmap](ROADMAP.md) | Current state and future vision |

## Installation

```bash
dotnet add package Weaviate.Client.Managed
```

## Minimal Example

```csharp
using Weaviate.Client;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Managed.Extensions;

// Define your model
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Title { get; set; } = "";

    [Property]
    public string Content { get; set; } = "";

    [Vector<Vectorizer.Text2VecOpenAI>(SourceProperties = [nameof(Title), nameof(Content)])]
    public float[]? Embedding { get; set; }
}

// Use it
var client = new WeaviateClient(new WeaviateConfig { Host = "localhost:8080" });
var articles = await client.Collections.CreateManaged<Article>();

await articles.Insert(new Article { Title = "Hello", Content = "World" });

var results = await articles.Query
    .NearText("greeting")
    .Limit(10)
    .Execute();

// Access results
foreach (var result in results)
{
    Console.WriteLine($"{result.Object.Title}: Score={result.Metadata?.Score}");
}

// Or extract just objects when metadata not needed
var objects = results.Objects();
```

## Terminology

This library uses **"Managed"** terminology:
- `ManagedCollection<T>` - Type-safe collection wrapper
- `CreateManaged<T>()` - Create a managed collection
- `UseManaged<T>()` - Get existing collection as managed (collection name from attribute)
- `AsManaged<T>()` - Wrap existing collection client

Historical note: Earlier development used "CollectionMapper" and "WOMP" terminology.
