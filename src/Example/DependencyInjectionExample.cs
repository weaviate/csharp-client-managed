using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weaviate.Client;
using Weaviate.Client.DependencyInjection;
using Weaviate.Client.Managed;
using Weaviate.Client.Managed.Context;
using Weaviate.Client.Managed.DependencyInjection;
using Weaviate.Client.Managed.Extensions;

namespace Example;

/// <summary>
/// Example demonstrating how to use Weaviate with dependency injection.
/// </summary>
public class DependencyInjectionExample
{
    public static async Task Run()
    {
        // Build host with dependency injection
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(
                (context, services) =>
                {
                    // Register Weaviate client
                    services.AddWeaviateLocal(
                        hostname: "localhost",
                        restPort: 8080,
                        grpcPort: 50051,
                        eagerInitialization: true // Client initializes on startup
                    );

                    // Register your services that use Weaviate
                    services.AddSingleton<CatService>();
                }
            )
            .Build();

        // Run the host - this triggers eager initialization
        await host.StartAsync();

        // Get the service and use it
        var catService = host.Services.GetRequiredService<CatService>();

        Console.WriteLine("=== Dependency Injection Example ===\n");

        // The client is already initialized and ready to use!
        await catService.DemonstrateUsageAsync();

        await host.StopAsync();
    }
}

/// <summary>
/// Example service that uses Weaviate via dependency injection.
/// </summary>
public class CatService
{
    private readonly WeaviateClient _weaviate;
    private readonly ILogger<CatService> _logger;

    public CatService(WeaviateClient weaviate, ILogger<CatService> logger)
    {
        _weaviate = weaviate;
        _logger = logger;

        // Client is already initialized!
        _logger.LogInformation(
            "CatService created. Weaviate version: {Version}",
            _weaviate.WeaviateVersion
        );
    }

    public async Task DemonstrateUsageAsync()
    {
        // Check if client is initialized
        _logger.LogInformation("Client initialized: {IsInitialized}", _weaviate.IsInitialized);

        // Create collection from Cat class attributes
        // CreateManaged automatically reads the [WeaviateCollection] attribute
        _logger.LogInformation("Creating Cat collection from class attributes...");

        try
        {
            // Try to delete if it already exists (cleanup from previous run)
            await _weaviate.Collections.Delete("Cat");
        }
        catch
        {
            // Collection didn't exist, that's fine
        }

        var collection = await _weaviate.Collections.CreateManaged<Cat>();

        // Insert a cat
        _logger.LogInformation("Inserting a cat...");
        var catId = await collection.Insert(
            new Cat
            {
                Name = "Fluffy",
                Breed = "Persian",
                Color = "white",
                Counter = 1,
            }
        );

        _logger.LogInformation("Inserted cat with UUID: {Id}", catId);

        // Query cats - Execute() is now optional!
        _logger.LogInformation("Querying cats...");
        var results = await collection.Query().Limit(10);

        // Get just the Cat objects (without metadata) using .Objects()
        var cats = results.Objects();
        _logger.LogInformation("Found {Count} cats", cats.Count());

        foreach (var cat in cats)
        {
            _logger.LogInformation("  - {Name} ({Breed}, {Color})", cat.Name, cat.Breed, cat.Color);
        }

        // Cleanup
        _logger.LogInformation("Cleaning up...");
        await _weaviate.Collections.Delete(collection.Name);
    }
}

/// <summary>
/// Alternative example using configuration from appsettings.json
/// </summary>
public class ConfigurationExample
{
    public static async Task RunAsync()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(
                (context, services) =>
                {
                    // Register from configuration section
                    services.AddWeaviate(
                        context.Configuration.GetSection("Weaviate"),
                        eagerInitialization: true
                    );

                    services.AddSingleton<CatService>();
                }
            )
            .Build();

        await host.StartAsync();

        var catService = host.Services.GetRequiredService<CatService>();
        await catService.DemonstrateUsageAsync();

        await host.StopAsync();
    }
}

/// <summary>
/// Example using lazy initialization
/// </summary>
public class LazyInitializationExample
{
    public static async Task RunAsync()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(
                (context, services) =>
                {
                    // Lazy initialization - client initializes on first use
                    services.AddWeaviateLocal(eagerInitialization: false);
                }
            )
            .Build();

        await host.StartAsync();

        var client = host.Services.GetRequiredService<WeaviateClient>();

        Console.WriteLine($"Is initialized: {client.IsInitialized}"); // False

        // Manually trigger initialization
        await client.InitializeAsync();

        Console.WriteLine($"Is initialized: {client.IsInitialized}"); // True
        Console.WriteLine($"Weaviate version: {client.WeaviateVersion}");

        await host.StopAsync();
    }
}

/// <summary>
/// Example demonstrating WeaviateContext with dependency injection.
/// This shows how to register a managed context for DI resolution,
/// similar to EF Core's AddDbContext pattern.
/// </summary>
public class ManagedDependencyInjectionExample
{
    public static async Task RunAsync()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(
                (context, services) =>
                {
                    // Register core Weaviate client
                    services.AddWeaviateLocal(eagerInitialization: true);

                    // Register managed context with options
                    services.AddWeaviateContext<CatContext>(options =>
                    {
                        options.AutoCreateCollections = true;
                        options.AutoMigrate = true;
                    });

                    // Or with eager migration at startup:
                    // services.AddWeaviateContext<CatContext>(
                    //     options => { options.AutoMigrate = true; },
                    //     eagerMigration: true
                    // );
                }
            )
            .Build();

        await host.StartAsync();

        // Resolve context from DI - singleton by default
        var catContext = host.Services.GetRequiredService<CatContext>();

        Console.WriteLine("=== Managed DI Example ===\n");

        // Use context-level operations
        var cat = new Cat
        {
            Name = "Whiskers",
            Breed = "Siamese",
            Color = "cream",
            Counter = 1,
            DefaultVector = [0.1f, 0.2f, 0.3f],
        };

        await catContext.Insert(cat);
        Console.WriteLine("Inserted cat via context");

        var count = await catContext.Count<Cat>();
        Console.WriteLine($"Total cats: {count}");

        // Query using CollectionSet
        var cats = await catContext.Cats.Query().Execute();
        foreach (var c in cats)
        {
            Console.WriteLine($"  - {c}");
        }

        await host.StopAsync();
    }
}

/// <summary>
/// WeaviateContext for Cat entities, registered via DI.
/// </summary>
public class CatContext : WeaviateContext
{
    // Constructor for manual instantiation
    public CatContext(WeaviateClient client)
        : base(client) { }

    // Constructor for DI (options injected by container)
    public CatContext(WeaviateClient client, WeaviateContextOptions<CatContext> options)
        : base(client, options) { }

    public CollectionSet<Cat> Cats { get; set; } = null!;
}

/// <summary>
/// Example using Connect helpers (backward compatible)
/// </summary>
public class ConnectHelperExample
{
    public static async Task RunAsync()
    {
        // These still work! Fully async, no blocking
        var client = await Connect.Local();

        Console.WriteLine($"Connected to Weaviate {client.WeaviateVersion}");

        var collection = client.Collections.Use<Cat>("Cat");

        // Use the client...
        var results = await collection.Query.FetchObjects(limit: 10);

        Console.WriteLine($"Found {results.Objects.Count} cats");

        client.Dispose();
    }
}
