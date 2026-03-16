# Dependency Injection

Integrating Weaviate.Client.Managed with ASP.NET Core and dependency injection.

## Registering WeaviateContext

### Using AddWeaviateContext

The simplest way to register a `WeaviateContext` with dependency injection:

```csharp
// Program.cs
using Weaviate.Client;
using Weaviate.Client.Managed.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register Weaviate client
builder.Services.AddSingleton(sp =>
    new WeaviateClient(new WeaviateConfig { Host = "localhost:8080" }));

// Register your context
builder.Services.AddWeaviateContext<BlogContext>();

var app = builder.Build();
```

### Configuring Context Options

Configure auto-creation and auto-migration at registration time:

```csharp
builder.Services.AddWeaviateContext<BlogContext>(options =>
{
    // Auto-create collections on first access
    options.UseAutoCreate();

    // Auto-migrate collections on first access (safe changes only)
    options.UseAutoMigrate();

    // Allow breaking schema changes during auto-migration
    options.UseAutoMigrate(allowBreaking: true);
});
```

### Eager Migration at Startup

Run schema migrations when the application starts using a hosted service:

```csharp
// Register context with eager migration enabled
builder.Services.AddWeaviateContext<BlogContext>(
    configureOptions: options =>
    {
        options.UseAutoCreate();
        options.UseAutoMigrate();
    },
    eagerMigration: true  // Runs migration at startup
);
```

This registers an `IHostedService` that will:
1. Resolve the context at application startup
2. Run `context.Migrate()` based on configured options
3. Create collections if `UseAutoCreate()` is enabled
4. Apply safe schema changes if `UseAutoMigrate()` is enabled

### Service Lifetime

By default, contexts are registered as **Singleton**. Unlike EF Core, `WeaviateContext` has no connection pooling or change tracking, making singleton the natural choice.

Use **Scoped** lifetime if you need per-request tenant resolution:

```csharp
builder.Services.AddWeaviateContext<BlogContext>(
    configureOptions: null,
    lifetime: ServiceLifetime.Scoped
);
```

### OnConfiguring Override

Context instances can override `OnConfiguring()` to set defaults, which take precedence over DI configuration:

```csharp
public class BlogContext : WeaviateContext
{
    public BlogContext(WeaviateClient client) : base(client) { }

    public CollectionSet<Article> Articles { get; set; } = null!;

    protected override void OnConfiguring(WeaviateContextOptionsBuilder options)
    {
        // This always wins, even if DI configuration differs
        options.UseAutoMigrate(allowBreaking: false);
    }
}
```

### Dependency Injection Usage

```csharp
// In controllers
public class ArticlesController : ControllerBase
{
    private readonly BlogContext _context;

    public ArticlesController(BlogContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var articles = await _context.Articles.Query()
            .Limit(20)
            .Execute();
        return Ok(articles.Objects());
    }
}

// In services
public class ArticleService
{
    private readonly BlogContext _context;

    public ArticleService(BlogContext context)
    {
        _context = context;
    }

    public async Task<Article> CreateArticle(string title, string content)
    {
        var article = new Article { Title = title, Content = content };
        return await _context.Insert(article);
    }
}
```

---

## Basic Setup

### Register WeaviateClient

```csharp
// Program.cs
using Weaviate.Client;
using Weaviate.Client.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register Weaviate client as singleton
builder.Services.AddWeaviateLocal(
    hostname: "localhost",
    restPort: 8080,
    grpcPort: 50051
);

var app = builder.Build();
```

### Configuration-Based Setup

```csharp
// appsettings.json
{
  "Weaviate": {
    "Host": "localhost:8080",
    "GrpcHost": "localhost:50051",
    "ApiKey": "your-api-key"
  }
}

// Program.cs
builder.Services.AddSingleton(sp =>
{
    var config = builder.Configuration.GetSection("Weaviate");
    return new WeaviateClient(new WeaviateConfig
    {
        Host = config["Host"]!,
        GrpcHost = config["GrpcHost"],
        ApiKey = config["ApiKey"]
    });
});
```

---

## Registering Managed Collections

### Factory Pattern

```csharp
// Register collection factory
builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<WeaviateClient>();
    return client.Collections.UseManaged<Product>();
});

builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<WeaviateClient>();
    return client.Collections.UseManaged<Article>();
});
```

### Async Initialization

Collections need to be created before use. Handle this with a hosted service:

```csharp
public class WeaviateInitializationService : IHostedService
{
    private readonly WeaviateClient _client;
    private readonly ILogger<WeaviateInitializationService> _logger;

    public WeaviateInitializationService(
        WeaviateClient client,
        ILogger<WeaviateInitializationService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing Weaviate collections...");

        // Create collections if they don't exist
        await _client.Collections.CreateFromClass<Product>(
            existsAction: ExistsAction.UseExisting);

        await _client.Collections.CreateFromClass<Article>(
            existsAction: ExistsAction.UseExisting);

        _logger.LogInformation("Weaviate collections initialized");
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

// Registration
builder.Services.AddHostedService<WeaviateInitializationService>();
```

### Lazy Initialization

For collections that may not exist at startup:

```csharp
public class LazyCollectionProvider<T> where T : class, new()
{
    private readonly WeaviateClient _client;
    private ManagedCollection<T>? _collection;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LazyCollectionProvider(WeaviateClient client)
    {
        _client = client;
    }

    public async Task<ManagedCollection<T>> GetCollectionAsync()
    {
        if (_collection != null)
            return _collection;

        await _lock.WaitAsync();
        try
        {
            _collection ??= await _client.Collections.CreateManaged<T>(
                existsAction: ExistsAction.UseExisting);
            return _collection;
        }
        finally
        {
            _lock.Release();
        }
    }
}

// Registration
builder.Services.AddSingleton<LazyCollectionProvider<Product>>();
builder.Services.AddSingleton<LazyCollectionProvider<Article>>();
```

---

## Service Layer Pattern

### Repository Pattern

```csharp
public interface IProductRepository
{
    Task<Guid> CreateAsync(Product product);
    Task<Product?> GetByIdAsync(Guid id);
    Task<IEnumerable<Product>> SearchAsync(string query, int limit = 10);
    Task UpdateAsync(Product product, Guid id);
    Task DeleteAsync(Guid id);
}

public class WeaviateProductRepository : IProductRepository
{
    private readonly ManagedCollection<Product> _collection;

    public WeaviateProductRepository(ManagedCollection<Product> collection)
    {
        _collection = collection;
    }

    public async Task<Guid> CreateAsync(Product product) =>
        await _collection.Insert(product);

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        var results = await _collection.Inner.Query.FetchObjectByID<Product>(id);
        return results.Properties;
    }

    public async Task<IEnumerable<Product>> SearchAsync(string query, int limit = 10) =>
        await _collection.Query
            .Hybrid(query)
            .Limit((uint)limit)
            .Execute();

    public async Task UpdateAsync(Product product, Guid id) =>
        await _collection.Update(product, id);

    public async Task DeleteAsync(Guid id) =>
        await _collection.Delete(id);
}

// Registration
builder.Services.AddScoped<IProductRepository, WeaviateProductRepository>();
```

### Service Layer

```csharp
public class ProductService
{
    private readonly ManagedCollection<Product> _products;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        ManagedCollection<Product> products,
        ILogger<ProductService> logger)
    {
        _products = products;
        _logger = logger;
    }

    public async Task<IEnumerable<Product>> SearchProducts(
        string query,
        decimal? maxPrice = null,
        bool? inStock = null)
    {
        var queryBuilder = _products.Query
            .Hybrid(query, alpha: 0.7f);

        if (maxPrice.HasValue)
            queryBuilder = queryBuilder.Where(p => p.Price <= maxPrice.Value);

        if (inStock.HasValue)
            queryBuilder = queryBuilder.Where(p => p.InStock == inStock.Value);

        return await queryBuilder
            .Limit(20)
            .Execute();
    }

    public async Task<Guid> CreateProduct(CreateProductRequest request)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            InStock = true
        };

        var id = await _products.Insert(product);
        _logger.LogInformation("Created product {ProductId}: {Name}", id, product.Name);
        return id;
    }
}
```

---

## Multi-Tenant DI

### Tenant-Scoped Collections

```csharp
public interface ITenantProvider
{
    string GetCurrentTenant();
}

public class HttpContextTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentTenant()
    {
        // Get tenant from header, claim, route, etc.
        return _httpContextAccessor.HttpContext?
            .Request.Headers["X-Tenant-Id"].FirstOrDefault()
            ?? throw new InvalidOperationException("Tenant not found");
    }
}

public class TenantScopedCollection<T> where T : class, new()
{
    private readonly ManagedCollection<T> _baseCollection;
    private readonly ITenantProvider _tenantProvider;

    public TenantScopedCollection(
        ManagedCollection<T> baseCollection,
        ITenantProvider tenantProvider)
    {
        _baseCollection = baseCollection;
        _tenantProvider = tenantProvider;
    }

    public ManagedCollection<T> Collection =>
        _baseCollection.WithTenant(_tenantProvider.GetCurrentTenant());
}

// Registration
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, HttpContextTenantProvider>();
builder.Services.AddScoped(sp =>
{
    var baseCollection = sp.GetRequiredService<ManagedCollection<Product>>();
    var tenantProvider = sp.GetRequiredService<ITenantProvider>();
    return new TenantScopedCollection<Product>(baseCollection, tenantProvider);
});
```

---

## Multiple Weaviate Instances

```csharp
// Named clients
builder.Services.AddKeyedSingleton("primary", (sp, key) =>
    new WeaviateClient(new WeaviateConfig { Host = "primary.weaviate:8080" }));

builder.Services.AddKeyedSingleton("secondary", (sp, key) =>
    new WeaviateClient(new WeaviateConfig { Host = "secondary.weaviate:8080" }));

// Usage
public class MultiClusterService
{
    private readonly WeaviateClient _primary;
    private readonly WeaviateClient _secondary;

    public MultiClusterService(
        [FromKeyedServices("primary")] WeaviateClient primary,
        [FromKeyedServices("secondary")] WeaviateClient secondary)
    {
        _primary = primary;
        _secondary = secondary;
    }
}
```

---

## Configuration Options

### Full Configuration Example

```csharp
// appsettings.json
{
  "Weaviate": {
    "Host": "weaviate.example.com:443",
    "GrpcHost": "grpc.weaviate.example.com:443",
    "Scheme": "https",
    "GrpcScheme": "https",
    "ApiKey": "your-api-key",
    "Headers": {
      "X-Custom-Header": "value"
    }
  }
}

// Program.cs
builder.Services.AddSingleton(sp =>
{
    var section = builder.Configuration.GetSection("Weaviate");

    var config = new WeaviateConfig
    {
        Host = section["Host"]!,
        GrpcHost = section["GrpcHost"],
        Scheme = section["Scheme"] ?? "http",
        GrpcScheme = section["GrpcScheme"] ?? "http",
        ApiKey = section["ApiKey"]
    };

    // Custom headers
    var headers = section.GetSection("Headers")
        .GetChildren()
        .ToDictionary(x => x.Key, x => x.Value!);

    foreach (var header in headers)
    {
        config.Headers[header.Key] = header.Value;
    }

    return new WeaviateClient(config);
});
```

### Options Pattern

```csharp
public class WeaviateOptions
{
    public string Host { get; set; } = "localhost:8080";
    public string? GrpcHost { get; set; }
    public string Scheme { get; set; } = "http";
    public string? ApiKey { get; set; }
}

// Registration
builder.Services.Configure<WeaviateOptions>(
    builder.Configuration.GetSection("Weaviate"));

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<WeaviateOptions>>().Value;
    return new WeaviateClient(new WeaviateConfig
    {
        Host = options.Host,
        GrpcHost = options.GrpcHost,
        Scheme = options.Scheme,
        ApiKey = options.ApiKey
    });
});
```

---

## Lifetime Considerations

| Component | Recommended Lifetime | Reason |
|-----------|---------------------|--------|
| `WeaviateClient` | Singleton | Connection pooling, thread-safe |
| `ManagedCollection<T>` | Singleton | Stateless wrapper |
| Repository/Service | Scoped | Per-request logic |
| Tenant-scoped collection | Scoped | Tenant resolved per-request |

```csharp
// Correct lifetimes
builder.Services.AddSingleton<WeaviateClient>(...);
builder.Services.AddSingleton<ManagedCollection<Product>>(...);
builder.Services.AddScoped<IProductRepository, WeaviateProductRepository>();
builder.Services.AddScoped<ProductService>();
```

---

## Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("weaviate", async (ct) =>
    {
        var client = builder.Services.BuildServiceProvider()
            .GetRequiredService<WeaviateClient>();

        try
        {
            var ready = await client.Ready(ct);
            return ready
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("Weaviate not ready");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Weaviate unreachable", ex);
        }
    });

app.MapHealthChecks("/health");
```
