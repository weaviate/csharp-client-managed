# Best Practices

Collection design, performance optimization, error handling, and testing strategies.

## Collection Design

### Property Selection

**Do:**
- Include only properties you'll query or filter on
- Use appropriate data types (not everything needs to be `Text`)
- Add descriptions for documentation

**Don't:**
- Store large blobs unless needed for vectorization
- Create properties for computed values (calculate at query time)
- Over-index (every index has storage/performance cost)

```csharp
// ✅ Good
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    [Index(Filterable = true, Searchable = true)]
    public string Title { get; set; } = "";

    [Property]
    [Index(Searchable = true)]
    public string Content { get; set; } = "";

    [Property]
    [Index(Filterable = true)]
    public DateTime PublishedAt { get; set; }
}

// ❌ Avoid
[WeaviateCollection("Articles")]
public class Article
{
    [Property]
    [Index(Filterable = true, Searchable = true, RangeFilters = true)]  // Over-indexed
    public string Title { get; set; } = "";

    [Property(DataType.Blob)]  // Large blob stored unnecessarily
    public byte[] FullHtmlContent { get; set; }

    [Property]
    public int WordCount { get; set; }  // Could compute from Content
}
```

### Vector Strategy

**Single vector** - Most use cases:
```csharp
[Vector<Vectorizer.Text2VecOpenAI>]
public float[]? Embedding { get; set; }
```

**Multiple vectors** - When you need different search modes:
```csharp
// Semantic search
[Vector<Vectorizer.Text2VecOpenAI>(SourceProperties = [nameof(Title), nameof(Content)])]
public float[]? SemanticEmbedding { get; set; }

// Title-only for quick matching
[Vector<Vectorizer.Text2VecOpenAI>(SourceProperties = [nameof(Title)])]
public float[]? TitleEmbedding { get; set; }
```

**User-provided vectors** - When you have custom embeddings:
```csharp
[Vector<Vectorizer.SelfProvided>]
public float[]? CustomEmbedding { get; set; }
```

### Index Type Selection

| Use Case | Index Type | Why |
|----------|------------|-----|
| < 10K objects | `Flat` | Exact results, no overhead |
| 10K - 1M objects | `Hnsw` | Fast approximate search |
| Growing collection | `Dynamic` | Starts Flat, switches to HNSW |
| Very large (1M+) | `Hnsw` + Quantizer | Memory efficiency |

```csharp
// Small collection
[Vector<Vectorizer.Text2VecOpenAI>]
[VectorIndex<VectorIndexConfig.Flat>]
public float[]? Embedding { get; set; }

// Large collection with compression
[Vector<Vectorizer.Text2VecOpenAI>]
[VectorIndex<VectorIndexConfig.Hnsw>]
[QuantizerBQ(Cache = true)]
public float[]? Embedding { get; set; }

// Unknown size
[Vector<Vectorizer.Text2VecOpenAI>]
[VectorIndex<VectorIndexConfig.Dynamic>(Threshold = 10000)]
public float[]? Embedding { get; set; }
```

### Quantizer Selection

| Quantizer | Compression | Accuracy | Use Case |
|-----------|-------------|----------|----------|
| None | 1x | 100% | Small datasets, exact search |
| BQ | 32x | ~95% | Large datasets, cosine distance |
| SQ | 4x | ~99% | Good balance |
| PQ | 8-32x | ~97% | Very large datasets |
| RQ | 8-32x | ~98% | Best accuracy per compression |

```csharp
// Binary Quantization - fastest, good for cosine
[QuantizerBQ(Cache = true, RescoreLimit = 200)]

// Scalar Quantization - balanced
[QuantizerSQ(TrainingLimit = 100000)]

// Product Quantization - high compression
[QuantizerPQ(Segments = 128, Centroids = 256)]
```

---

## Performance Optimization

### Batch Operations

**Always batch for bulk operations:**

```csharp
// ❌ Slow - 1000 network calls
foreach (var product in products)
{
    await context.Insert(product);
}

// ✅ Fast - 1 network call
await context.Products.Insert(p1, p2, p3, /* ... */);

// ✅ Or pass a large array
var productArray = products.ToArray();
await context.Products.Insert(productArray);
```

**Optimal batch size:** 100-1000 objects per batch.

### Query Optimization

**Limit results:**
```csharp
// ❌ Returns all matches
var results = await collection.Query().Where(p => p.InStock).Execute();

// ✅ Returns only what you need
var results = await collection.Query()
    .Where(p => p.InStock)
    .Limit(50)
    .Execute();
```

**Filter before vector search:**
```csharp
// ✅ Filter reduces search space
var results = await collection.Query()
    .Where(p => p.Category == "Electronics")  // Filter first
    .NearText("wireless headphones")           // Then vector search
    .Limit(10)
    .Execute();
```

**Select only needed properties:**
```csharp
// ❌ Returns all properties
var results = await collection.Query().Execute();

// ✅ Returns only Name and Price
var results = await collection.Query()
    .Select(p => new { p.Name, p.Price })
    .Execute();
```

**Don't fetch vectors unless needed:**
```csharp
// ❌ Fetches large vector data
var results = await collection.Query()
    .WithVectors(p => p.Embedding)
    .Execute();

// ✅ Skip vectors if not needed
var results = await collection.Query().Execute();
```

### Connection Management

**Reuse client instances:**
```csharp
// ❌ Creates new connection per request
public class ProductService
{
    public async Task<Product> GetProduct(Guid id)
    {
        var client = new WeaviateClient(config);  // Bad!
        // ...
    }
}

// ✅ Inject singleton client
public class ProductService
{
    private readonly ManagedCollection<Product> _products;

    public ProductService(ManagedCollection<Product> products)
    {
        _products = products;
    }
}
```

---

## Error Handling

### Common Exceptions

```csharp
try
{
    await collection.Insert(product);
}
catch (WeaviateException ex) when (ex.StatusCode == 400)
{
    // Bad request - invalid data
    _logger.LogError("Invalid product data: {Message}", ex.Message);
}
catch (WeaviateException ex) when (ex.StatusCode == 404)
{
    // Collection doesn't exist
    _logger.LogError("Collection not found");
}
catch (WeaviateException ex) when (ex.StatusCode == 422)
{
    // Validation error
    _logger.LogError("Validation failed: {Message}", ex.Message);
}
catch (WeaviateException ex)
{
    // Other Weaviate errors
    _logger.LogError("Weaviate error {Status}: {Message}",
        ex.StatusCode, ex.Message);
}
```

### Retry Patterns

```csharp
public async Task<T> WithRetry<T>(Func<Task<T>> operation, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await operation();
        }
        catch (WeaviateException ex) when (IsTransient(ex) && i < maxRetries - 1)
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
        }
    }
    throw new InvalidOperationException("Max retries exceeded");
}

private bool IsTransient(WeaviateException ex) =>
    ex.StatusCode is 429 or 503 or 504;
```

### Batch Error Handling

```csharp
var response = await collection.InsertMany(products);

if (response.FailedCount > 0)
{
    foreach (var error in response.Errors)
    {
        _logger.LogWarning(
            "Failed to insert object {Index}: {Message}",
            error.Index, error.Message);
    }

    // Optionally retry failed items
    var failedProducts = response.Errors
        .Select(e => products.ElementAt(e.Index))
        .ToList();
}
```

---

## Testing Strategies

### Unit Testing with Mocks

```csharp
public class ProductServiceTests
{
    [Fact]
    public async Task SearchProducts_ReturnsMatchingProducts()
    {
        // Arrange
        var mockCollection = new Mock<ManagedCollection<Product>>();
        var expectedProducts = new List<Product>
        {
            new() { Name = "Widget", Price = 9.99m }
        };

        mockCollection
            .Setup(c => c.Query().NearText(It.IsAny<string>()).Limit(10).Execute())
            .ReturnsAsync(expectedProducts);

        var service = new ProductService(mockCollection.Object);

        // Act
        var results = await service.SearchProducts("widget");

        // Assert
        Assert.Single(results);
        Assert.Equal("Widget", results.First().Name);
    }
}
```

### Integration Testing

```csharp
public class ProductIntegrationTests : IAsyncLifetime
{
    private WeaviateClient _client = null!;
    private ManagedCollection<Product> _products = null!;

    public async Task InitializeAsync()
    {
        _client = await Connect.Local();

        // Use unique collection name per test run
        var collectionName = $"Products_{Guid.NewGuid():N}";

        _products = await _client.Collections.CreateManaged<TestProduct>(
            name: collectionName);
    }

    public async Task DisposeAsync()
    {
        await _products.DeleteCollection();
        _client.Dispose();
    }

    [Fact]
    public async Task Insert_And_Query_Works()
    {
        // Arrange
        var product = new TestProduct { Name = "Test Widget", Price = 19.99m };

        // Act
        var id = await _products.Insert(product);
        var results = await _products.Query
            .Where(p => p.Name == "Test Widget")
            .Execute();

        // Assert
        Assert.Single(results);
        Assert.Equal(19.99m, results.First().Price);
    }
}

[WeaviateCollection]  // Name set dynamically
public class TestProduct
{
    [Property]
    public string Name { get; set; } = "";

    [Property]
    public decimal Price { get; set; }
}
```

### Test Fixtures

```csharp
public class WeaviateFixture : IAsyncLifetime
{
    public WeaviateClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Client = await Connect.Local();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
    }

    public async Task<ManagedCollection<T>> CreateTestCollection<T>()
        where T : class, new()
    {
        var name = $"Test_{typeof(T).Name}_{Guid.NewGuid():N}";
        return await Client.Collections.CreateManaged<T>(name: name);
    }
}

public class ProductTests : IClassFixture<WeaviateFixture>
{
    private readonly WeaviateFixture _fixture;

    public ProductTests(WeaviateFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Test()
    {
        var collection = await _fixture.CreateTestCollection<Product>();
        // Test...
        await collection.DeleteCollection();
    }
}
```

---

## Production Considerations

### Monitoring

```csharp
public class InstrumentedCollection<T> where T : class, new()
{
    private readonly ManagedCollection<T> _inner;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;

    public async Task<Guid> Insert(T obj)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var id = await _inner.Insert(obj);
            _metrics.RecordInsertLatency(sw.ElapsedMilliseconds);
            _metrics.IncrementInsertCount();
            return id;
        }
        catch (Exception ex)
        {
            _metrics.IncrementInsertErrors();
            _logger.LogError(ex, "Insert failed for {Type}", typeof(T).Name);
            throw;
        }
    }
}
```

### Health Checks

```csharp
public class WeaviateHealthCheck : IHealthCheck
{
    private readonly WeaviateClient _client;

    public WeaviateHealthCheck(WeaviateClient client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ready = await _client.Ready(cancellationToken);
            return ready
                ? HealthCheckResult.Healthy("Weaviate is ready")
                : HealthCheckResult.Degraded("Weaviate not ready");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Weaviate unreachable", ex);
        }
    }
}

// Registration
services.AddHealthChecks()
    .AddCheck<WeaviateHealthCheck>("weaviate");
```

### Graceful Shutdown

```csharp
public class WeaviateShutdownService : IHostedService
{
    private readonly WeaviateClient _client;

    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Allow pending operations to complete
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        _client.Dispose();
    }
}
```

---

## Nested vs References Decision Tree

```
Do you need to share this data across multiple objects?
├── Yes → Use [Reference]
│         - Data stored once, referenced many times
│         - Updates propagate automatically
│         - Separate collection lifecycle
│
└── No → Does this data have independent meaning?
         ├── Yes → Use [Reference]
         │         - Can query independently
         │         - Can exist without parent
         │
         └── No → Use nested object
                  - Data belongs to parent
                  - Always returned with parent
                  - Deleted with parent
```

**Examples:**

| Scenario | Pattern | Reason |
|----------|---------|--------|
| Product → Category | Reference | Categories shared across products |
| Order → LineItems | Nested | Line items belong to specific order |
| Article → Author | Reference | Author writes many articles |
| BlogPost → Comments | Nested | Comments belong to specific post |
| User → Address | Either | Depends if addresses are reused |
