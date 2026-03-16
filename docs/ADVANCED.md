# Advanced Patterns

Multi-tenancy, multi-vector, RAG, batch operations, and advanced configuration patterns.

## Table of Contents

- [Multi-Tenancy](#multi-tenancy)
- [Multi-Vector (ColBERT)](#multi-vector-colbert)
- [Multiple Named Vectors](#multiple-named-vectors)
- [Generative AI (RAG)](#generative-ai-rag)
- [Reranker Integration](#reranker-integration)
- [Batch Operations](#batch-operations)
- [Tenant and Consistency Scoping](#tenant-and-consistency-scoping)
- [Query Projections](#query-projections)
- [Collection Lifecycle Hooks](#collection-lifecycle-hooks)
- [ConfigMethod Patterns](#configmethod-patterns)
- [Complex References](#complex-references)

---

## Multi-Tenancy

Isolate data by tenant for multi-customer applications.

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

    [Property]
    public decimal Price { get; set; }
}
```

**Settings:**

- `MultiTenancyEnabled` — Immutable after creation
- `AutoTenantCreation` — Create tenant on first use
- `AutoTenantActivation` — Activate tenant automatically

### Tenant Operations with Context

```csharp
var context = new StoreContext(client);

// Create a tenant-scoped context
var acme = context.ForTenant("acme-corp");
var globex = context.ForTenant("globex-inc");

// Operations are isolated
await acme.Insert(new Product { Name = "Acme Widget", Price = 19.99m });
await globex.Insert(new Product { Name = "Globex Gadget", Price = 29.99m });

// Queries return only tenant's data
var acmeProducts = await acme.Products.Query().Execute();
```

### Tenant Operations with ManagedCollection

```csharp
var products = await client.Collections.CreateManaged<Product>();

var acme = products.WithTenant("acme-corp");
var globex = products.WithTenant("globex-inc");

await acme.Insert(new Product { Name = "Acme Widget", Price = 19.99m });
await globex.Insert(new Product { Name = "Globex Gadget", Price = 29.99m });

var acmeProducts = await acme.Query().Execute();
```

### Tenant Management

```csharp
// Create tenant manually (if AutoTenantCreation = false)
await client.Collections.Use("Products").Tenants.Create("new-tenant");

// List tenants
var tenants = await client.Collections.Use("Products").Tenants.Get();

// Deactivate tenant (data preserved but inaccessible)
await client.Collections.Use("Products").Tenants.Update(
    new TenantUpdate("old-tenant", TenantActivityStatus.Inactive));

// Delete tenant (data deleted)
await client.Collections.Use("Products").Tenants.Delete("departing-tenant");
```

### Per-Request Tenant Resolution

```csharp
public class ProductService
{
    private readonly StoreContext _context;
    private readonly ITenantResolver _tenantResolver;

    public ProductService(StoreContext context, ITenantResolver tenantResolver)
    {
        _context = context;
        _tenantResolver = tenantResolver;
    }

    public async Task<IEnumerable<Product>> SearchProducts(string query)
    {
        var tenantContext = _context.ForTenant(_tenantResolver.GetCurrentTenant());
        var results = await tenantContext.Products.Query()
            .Hybrid(query)
            .Execute();
        return results.Objects();
    }
}
```

---

## Multi-Vector (ColBERT)

Late interaction models with per-token embeddings.

### Define ColBERT Vectors

```csharp
[WeaviateCollection("Documents")]
public class Document
{
    [Property]
    public string Content { get; set; } = "";

    // Multi-vector: 2D array where each row is a token embedding
    [Vector<Vectorizer.SelfProvided>]
    [Encoding(KSim = 3, DProjections = 512)]
    public float[,]? TokenEmbeddings { get; set; }
}
```

**Encoding settings:**

- `KSim` — k for k-nearest token similarity matching
- `DProjections` — Projection dimensions
- `Repetitions` — Number of repetitions

### Insert Multi-Vectors

```csharp
// Generate token embeddings with your ColBERT model
float[,] tokenEmbeddings = colbertModel.Encode("Your document text");
// Shape: [numTokens, embeddingDim], e.g., [128, 768]

await documents.Insert(new Document
{
    Content = "Your document text",
    TokenEmbeddings = tokenEmbeddings
});
```

### Query with Multi-Vectors

```csharp
// Generate query token embeddings
float[,] queryEmbeddings = colbertModel.EncodeQuery("search query");

var results = await documents.Query
    .NearVector(queryEmbeddings.Cast<float>().ToArray())  // Flatten for query
    .Limit(10)
    .Execute();
```

---

## Multiple Named Vectors

Different embeddings for different search strategies.

### Define Multiple Vectors

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

    // Semantic search on text
    [Vector<Vectorizer.Text2VecOpenAI>(
        Model = "text-embedding-3-small",
        SourceProperties = [nameof(Name), nameof(Description)]
    )]
    public float[]? TextEmbedding { get; set; }

    // Visual similarity search
    [Vector<Vectorizer.Multi2VecClip>(
        ImageFields = [nameof(Image)]
    )]
    public float[]? ImageEmbedding { get; set; }

    // Combined text + image
    [Vector<Vectorizer.Multi2VecClip>(
        TextFields = [nameof(Name)],
        ImageFields = [nameof(Image)]
    )]
    public float[]? MultiModalEmbedding { get; set; }

    // User's custom embedding
    [Vector<Vectorizer.SelfProvided>]
    public float[]? CustomEmbedding { get; set; }
}
```

### Query Specific Vectors

```csharp
// Text search
var textResults = await products.Query()
    .NearText("wireless headphones", vector: p => p.TextEmbedding)
    .Execute();

// Image search
var imageResults = await products.Query()
    .NearVector(imageEmbedding, vector: p => p.ImageEmbedding)
    .Execute();

// Hybrid on specific vector
var hybridResults = await products.Query()
    .Hybrid("headphones", vector: p => p.TextEmbedding)
    .Execute();
```

### Retrieve Multiple Vectors

```csharp
var results = await products.Query()
    .NearText("headphones")
    .WithVectors(
        p => p.TextEmbedding,
        p => p.ImageEmbedding)
    .Execute();

foreach (var result in results)
{
    Console.WriteLine($"Text vector: {result.Object.TextEmbedding?.Length} dims");
    Console.WriteLine($"Image vector: {result.Object.ImageEmbedding?.Length} dims");
}
```

---

## Generative AI (RAG)

Retrieval Augmented Generation using the fluent `.Generate()` API.

### Configure Generative Module

```csharp
[WeaviateCollection("Documents")]
[Generative<GenerativeConfig.OpenAI>(
    Model = "gpt-4",
    Temperature = 0.7,
    MaxTokens = 2000
)]
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

### Provider Examples

```csharp
// Anthropic Claude
[Generative<GenerativeConfig.Anthropic>(
    Model = "claude-3-5-sonnet-20241022",
    MaxTokens = 4096
)]

// Azure OpenAI
[Generative<GenerativeConfig.AzureOpenAI>(
    ResourceName = "my-resource",
    DeploymentId = "gpt-4-deployment",
    Model = "gpt-4"
)]

// AWS Bedrock
[Generative<GenerativeConfig.AWS>(
    Model = "anthropic.claude-3-sonnet-20240229-v1:0",
    Region = "us-east-1"
)]

// Google
[Generative<GenerativeConfig.Google>(
    Model = "gemini-pro",
    ProjectId = "my-project"
)]

// Local Ollama
[Generative<GenerativeConfig.Ollama>(
    Model = "llama2",
    BaseURL = "http://localhost:11434"
)]
```

### Single Prompt Generation

Generate a response per result:

```csharp
var results = await context.Documents.Query()
    .NearText("machine learning")
    .Limit(5)
    .Generate(singlePrompt: "Summarize the key points of this document")
    .Execute();

foreach (var r in results)
{
    Console.WriteLine($"{r.Object.Title}:");
    Console.WriteLine($"  {r.Generative?[0]}");
}
```

### Grouped Task Generation

Generate a single response using all results as context:

```csharp
var results = await context.Documents.Query()
    .NearText("machine learning")
    .Limit(5)
    .Generate(groupedTask: "Compare and contrast these documents")
    .Execute();

// Grouped result
Console.WriteLine(results.Generative?[0]);
```

### Combined Single + Grouped

```csharp
var results = await context.Documents.Query()
    .NearText("machine learning")
    .Limit(5)
    .Generate(
        singlePrompt: "One-sentence summary",
        groupedTask: "Which document is the most comprehensive?"
    )
    .Execute();

// Per-object results
foreach (var r in results)
    Console.WriteLine($"{r.Object.Title}: {r.Generative?[0]}");

// Grouped result
Console.WriteLine($"\nBest: {results.Generative?[0]}");
```

---

## Reranker Integration

Re-order search results for better relevance.

### Configure Reranker

```csharp
[WeaviateCollection("Articles")]
[Reranker<RerankerConfig.Cohere>(
    Model = "rerank-english-v3.0"
)]
public class Article
{
    [Property]
    public string Title { get; set; } = "";

    [Property]
    public string Content { get; set; } = "";

    [Vector<Vectorizer.Text2VecOpenAI>]
    public float[]? Embedding { get; set; }
}
```

### Using Reranker in Queries

Use `.Rerank(property, query?)` to specify which property to score on, with an optional separate rerank query:

```csharp
// Rerank on a property with an explicit query
var results = await context.Articles.Query()
    .NearText("machine learning basics")
    .Rerank("title", "machine learning basics")
    .Limit(20)
    .Execute();

// Rerank on a property without a separate query
var results = await context.Articles.Query()
    .Hybrid("machine learning")
    .Rerank("content")
    .Execute();
```

### Reranker Provider Examples

```csharp
// Cohere
[Reranker<RerankerConfig.Cohere>(Model = "rerank-english-v3.0")]

// Voyage AI
[Reranker<RerankerConfig.VoyageAI>(Model = "rerank-1")]

// Jina AI
[Reranker<RerankerConfig.JinaAI>(Model = "jina-reranker-v1-base-en")]

// Local Transformers
[Reranker<RerankerConfig.Transformers>]
```

---

## Batch Operations

### Same-Collection Ordered Inserts (PendingInsert)

`Insert(params T[])` returns a `PendingInsert<T>` that is directly awaitable. Chain additional `.Insert()` calls to execute batches in sequence — useful when later items in the same collection reference earlier ones.

```csharp
// Single batch — directly awaitable
await context.Articles.Insert(article1, article2);

// Chained — second batch runs strictly after the first
await context.Articles
    .Insert(parentArticle)
    .Insert(childArticle)   // may reference parentArticle
    .Execute(cancellationToken);
```

### Cross-Collection Batch (PendingBatch)

`context.Batch()` collects operations across multiple collections and executes them in dependency order via topological sort. If `Article` references `Category`, categories are inserted before articles automatically.

```csharp
var batch = context.Batch();

batch.Insert(new Category { Name = "Technology" });
batch.Insert(new Article { Title = "Post 1" /* category ref set */ });
batch.Insert(new Article { Title = "Post 2" });

await batch.Execute(cancellationToken);
```

### Pending Delete (PendingDelete)

`Delete(params T[])` and `Delete(params Guid[])` return `PendingDelete<T>`. Chain calls to accumulate IDs; all are sent in a single batch call on execution.

```csharp
// Directly awaitable
await context.Articles.Delete(article1, article2);

// Chained accumulation
await context.Articles
    .Delete(article1, article2)
    .Delete(oldArticleId)
    .Execute(cancellationToken);
```

### Pending Update (PendingUpdate)

`Update(params T[])` returns `PendingUpdate<T>` — directly awaitable.

```csharp
var updated = await context.Products.Update(p1, p2, p3);
```

### Pending Reference (PendingReference)

`AddReference(...)` returns `PendingReference<T>`. Chain calls to accumulate links sent in a single `ReferenceAddMany` call.

```csharp
await context.Articles
    .AddReference(article1, a => a.Category, techCategory)
    .AddReference(article2, a => a.Category, scienceCategory)
    .Execute(cancellationToken);
```

---

## Tenant and Consistency Scoping

Both `WeaviateContext` and `ManagedCollection<T>` support tenant and consistency scoping through immutable cloning — the original instance is not modified.

### Context Scoping

```csharp
// Tenant scoping — returns a new context
var tenantContext = context.ForTenant("acme-corp");

// Consistency scoping — returns a new context
var strongContext = context.WithConsistencyLevel(ConsistencyLevel.All);

// Both
var scopedContext = context
    .ForTenant("acme-corp")
    .WithConsistencyLevel(ConsistencyLevel.Quorum);

// All operations on scopedContext use tenant + consistency
await scopedContext.Insert(new Product { Name = "Widget" });
```

### ManagedCollection Scoping

```csharp
var tenantProducts = products.WithTenant("acme-corp");
var strongProducts = products.WithConsistencyLevel(ConsistencyLevel.All);
```

### Consistency Levels

| Level | Behaviour |
| --- | --- |
| `One` | Fastest — writes/reads from one node |
| `Quorum` | Majority of nodes must acknowledge |
| `All` | All nodes must acknowledge (strongest) |

---

## Query Projections

Project query results into a different type than the collection entity. Useful for selecting subsets of properties, renaming fields, including vectors, or injecting metadata.

### Define a Projection

```csharp
[QueryProjection<Article>]
public class ArticleSummary
{
    // Same name as source — auto-mapped
    public string Title { get; set; } = "";

    // Different name — use [MapFrom]
    [MapFrom(nameof(Article.WordCount))]
    public int Words { get; set; }

    // Include a named vector
    [Vector(VectorName = "embedding")]
    public float[]? Embedding { get; set; }

    // Inject metadata
    [MetadataProperty]
    public double? Score { get; set; }
}
```

### Use a Projection

```csharp
var summaries = await context.Articles.Project<ArticleSummary>()
    .NearText("machine learning")
    .Limit(10)
    .Execute();

foreach (var s in summaries)
    Console.WriteLine($"{s.Title} ({s.Words} words): score={s.Score}");
```

---

## Collection Lifecycle Hooks

The `OnCollectionConfig` pattern allows intercepting collection creation.

### Per-Class Hook

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
            // Modify collection creation params
            return createParams;
        });
    }
}
```

### Global Interceptor

The `OnCollectionConfig.GlobalOnCreate` static property intercepts every collection creation. Applied **after** per-class hooks and **before** any external configure lambda.

```csharp
// Useful for test infrastructure (e.g., unique collection names per test)
OnCollectionConfig.GlobalOnCreate = createParams =>
{
    createParams.Name = $"{createParams.Name}_{testId}";
    return createParams;
};

// Remember to clean up
OnCollectionConfig.GlobalOnCreate = null;
```

Because `GlobalOnCreate` is static, integration tests that rely on it should be serialized (e.g., via xUnit's `[Collection]` attribute).

---

## ConfigMethod Patterns

Escape hatch for advanced configuration beyond attributes.

### Vector ConfigMethod

```csharp
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Content { get; set; } = "";

    [Vector<Vectorizer.Text2VecOpenAI>(
        Model = "text-embedding-3-small",
        ConfigMethod = nameof(ConfigureContentVector)
    )]
    public float[]? ContentEmbedding { get; set; }

    // Receives pre-built config with attribute values applied
    public static Vectorizer.Text2VecOpenAI ConfigureContentVector(
        string vectorName,
        Vectorizer.Text2VecOpenAI prebuilt)
    {
        prebuilt.VectorizeCollectionName = false;
        prebuilt.Type = "text";
        return prebuilt;
    }
}
```

### External ConfigMethod Class

Type-safe cross-class configuration:

```csharp
// Configuration class
public static class VectorConfigurations
{
    public static Vectorizer.Text2VecOpenAI ConfigureOpenAI(
        string vectorName,
        Vectorizer.Text2VecOpenAI prebuilt)
    {
        prebuilt.VectorizeCollectionName = false;
        return prebuilt;
    }

    public static Vectorizer.Text2VecCohere ConfigureCohere(
        string vectorName,
        Vectorizer.Text2VecCohere prebuilt)
    {
        prebuilt.Truncate = "END";
        return prebuilt;
    }
}

// Model class
[WeaviateCollection("Documents")]
public class Document
{
    [Vector<Vectorizer.Text2VecOpenAI>(
        ConfigMethod = nameof(VectorConfigurations.ConfigureOpenAI),
        ConfigMethodClass = typeof(VectorConfigurations)
    )]
    public float[]? Embedding { get; set; }
}
```

### Generative ConfigMethod

```csharp
[WeaviateCollection("Documents")]
[Generative<GenerativeConfig.Anthropic>(
    Model = "claude-3-5-sonnet-20241022",
    ConfigMethod = nameof(ConfigureGenerative)
)]
public class Document
{
    public static GenerativeConfig.Anthropic ConfigureGenerative(
        GenerativeConfig.Anthropic prebuilt)
    {
        prebuilt.StopSequences = new[] { "\n\nHuman:", "\n\nAssistant:" };
        prebuilt.TopK = 50;
        return prebuilt;
    }
}
```

---

## Complex References

### Circular References

```csharp
[WeaviateCollection("Employees")]
public class Employee
{
    [Property]
    public string Name { get; set; } = "";

    // Self-reference — target inferred from property type (Employee)
    [Reference]
    public Employee? Manager { get; set; }

    // Multi self-reference — target inferred from List<Employee>
    [Reference]
    public List<Employee>? DirectReports { get; set; }
}
```

### Multi-Collection References

```csharp
[WeaviateCollection("Orders")]
public class Order
{
    [Property]
    public DateTime OrderDate { get; set; }

    [Reference]
    public Customer? Customer { get; set; }

    [Reference]
    public List<Product>? Products { get; set; }

    [Reference]
    public Employee? SalesRep { get; set; }
}
```

### Reference Expansion

```csharp
var orders = await context.Orders.Query()
    .WithReferences(o => o.Customer)
    .WithReferences(o => o.Products)
    .WithReferences(o => o.SalesRep)
    .Execute();

foreach (var result in orders)
{
    var order = result.Object;
    Console.WriteLine($"Order for {order.Customer?.Name}");
    Console.WriteLine($"Products: {order.Products?.Count}");
    Console.WriteLine($"Sales rep: {order.SalesRep?.Name}");
}
```

### ID-Only References for Performance

When you don't need full objects:

```csharp
[WeaviateCollection("Events")]
public class Event
{
    [Property]
    public string Title { get; set; } = "";

    // ID only — target cannot be inferred from Guid?, so Target= is required
    [Reference(Target = typeof(User))]
    public Guid? CreatedById { get; set; }

    // Full object when needed — target inferred from Venue
    [Reference]
    public Venue? Venue { get; set; }
}

// Query without expansion (faster)
var events = await context.Events.Query().Execute();
// events[0].Object.CreatedById has the ID
// events[0].Object.Venue is null (not expanded)

// Query with selective expansion
var eventsWithVenue = await context.Events.Query()
    .WithReferences(e => e.Venue)  // Only expand Venue
    .Execute();
```

### Eager Reference Loading

Instead of calling `.WithReferences()` on every query, mark references as eagerly loaded:

```csharp
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    public string Title { get; set; } = "";

    // Always loaded — no .WithReferences() needed
    [Reference(Loading = ReferenceLoadingStrategy.Eager)]
    public Category? Category { get; set; }

    // Only loaded when explicitly requested
    [Reference]
    public List<Article>? RelatedArticles { get; set; }
}

// Category is automatically included
var results = await context.Articles.Query().Execute();
results.First().Object.Category?.Name  // Fully hydrated

// RelatedArticles still requires explicit request
var withRelated = await context.Articles.Query()
    .WithReferences(a => a.RelatedArticles)
    .Execute();
```

Eager references are merged with any explicit `.WithReferences()` calls — there are no duplicates.

### References in Query Projections

Use `[Reference]` in projection types to automatically include references:

```csharp
[QueryProjection<Article>]
public class ArticleWithCategory
{
    public string Title { get; set; } = "";

    [Reference]
    public Category? Category { get; set; }

    [Reference(SourceProperty = "RelatedArticles")]
    public List<Article>? Related { get; set; }

    [MetadataProperty]
    public double? Score { get; set; }
}

// References are auto-configured — no .WithReferences() needed
var results = await context.Articles.Project<ArticleWithCategory>()
    .NearText("technology")
    .Execute();
```
