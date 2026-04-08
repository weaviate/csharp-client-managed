using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Weaviate.Client.DependencyInjection;
using Xunit;

namespace Weaviate.Client.Managed.Tests.DependencyInjection;

public class WeaviateContextDITests
{
    [Fact]
    public void AddWeaviateContext_RegistersContextType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>();

        // Act
        var provider = services.BuildServiceProvider();
        var context = provider.GetService<TestStoreContext>();

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context.Products);
    }

    [Fact]
    public void AddWeaviateContext_PropagatesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>(options =>
        {
            options.AutoCreateCollections = true;
            options.AutoMigrate = true;
            options.AllowBreakingMigrations = true;
        });

        // Act
        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<TestStoreContext>();

        // Assert
        Assert.True(context.Options.AutoCreateCollections);
        Assert.True(context.Options.AutoMigrate);
        Assert.True(context.Options.AllowBreakingMigrations);
    }

    [Fact]
    public void AddWeaviateContext_OnConfiguringCanOverrideOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<OverridingContext>(options =>
        {
            options.AutoMigrate = false;
        });

        // Act
        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<OverridingContext>();

        // Assert - OnConfiguring enables AutoMigrate regardless of DI config
        Assert.True(context.Options.AutoMigrate);
    }

    [Fact]
    public void AddWeaviateContext_DefaultLifetimeIsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>();
        var provider = services.BuildServiceProvider();

        // Act
        var context1 = provider.GetRequiredService<TestStoreContext>();
        var context2 = provider.GetRequiredService<TestStoreContext>();

        // Assert - same instance
        Assert.Same(context1, context2);
    }

    [Fact]
    public async Task AddWeaviateContext_ScopedLifetimeProducesDifferentInstances()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>(lifetime: ServiceLifetime.Scoped);
        var provider = services.BuildServiceProvider();

        // Act
        TestStoreContext context1;
        TestStoreContext context2;
        await using (var scope1 = provider.CreateAsyncScope())
        {
            context1 = scope1.ServiceProvider.GetRequiredService<TestStoreContext>();
        }
        await using (var scope2 = provider.CreateAsyncScope())
        {
            context2 = scope2.ServiceProvider.GetRequiredService<TestStoreContext>();
        }

        // Assert - different instances per scope
        Assert.NotSame(context1, context2);
    }

    [Fact]
    public async Task AddWeaviateContext_ScopedReturnsSameWithinScope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>(lifetime: ServiceLifetime.Scoped);
        var provider = services.BuildServiceProvider();

        // Act
        await using var scope = provider.CreateAsyncScope();
        var context1 = scope.ServiceProvider.GetRequiredService<TestStoreContext>();
        var context2 = scope.ServiceProvider.GetRequiredService<TestStoreContext>();

        // Assert - same within a scope
        Assert.Same(context1, context2);
    }

    [Fact]
    public void AddWeaviateContext_MultipleContextTypes_RegisteredIndependently()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>(options =>
        {
            options.AutoMigrate = true;
        });
        services.AddWeaviateContext<SecondContext>(options =>
        {
            options.AutoMigrate = false;
        });

        // Act
        var provider = services.BuildServiceProvider();
        var storeContext = provider.GetRequiredService<TestStoreContext>();
        var secondContext = provider.GetRequiredService<SecondContext>();

        // Assert - different types with independent options
        Assert.True(storeContext.Options.AutoMigrate);
        Assert.False(secondContext.Options.AutoMigrate);
    }

    [Fact]
    public void AddWeaviateContext_WithoutWeaviateClient_ThrowsMeaningfulError()
    {
        // Arrange - no AddWeaviate* call
        var services = new ServiceCollection();
        services.AddWeaviateContext<TestStoreContext>();

        // Act & Assert
        var provider = services.BuildServiceProvider();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<TestStoreContext>()
        );
        Assert.Contains(nameof(WeaviateClient), ex.Message);
    }

    [Fact]
    public void AddWeaviateContext_WithoutOptionsConstructor_StillResolves()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<SingleCtorContext>();

        // Act
        var provider = services.BuildServiceProvider();
        var context = provider.GetService<SingleCtorContext>();

        // Assert
        Assert.NotNull(context);
    }

    [Fact]
    public void AddWeaviateContext_WithoutOptions_UsesDefaults()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>();

        // Act
        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<TestStoreContext>();

        // Assert - all defaults
        Assert.False(context.Options.AutoCreateCollections);
        Assert.False(context.Options.AutoMigrate);
        Assert.False(context.Options.AllowBreakingMigrations);
    }

    [Fact]
    public void AddWeaviateContext_Idempotent_DoesNotDoubleRegister()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>();
        services.AddWeaviateContext<TestStoreContext>(); // second call

        // Act
        var provider = services.BuildServiceProvider();
        var contexts = provider.GetServices<TestStoreContext>().ToList();

        // Assert - only one registration
        Assert.Single(contexts);
    }

    [Fact]
    public void AddWeaviateContext_ExposesUnderlyingClient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>();

        // Act
        var provider = services.BuildServiceProvider();
        var context = provider.GetRequiredService<TestStoreContext>();
        var client = provider.GetRequiredService<WeaviateClient>();

        // Assert
        Assert.Same(client, context.Client);
    }

    [Fact]
    public void AddWeaviateContext_SetsIntegrationHeader()
    {
        var services = new ServiceCollection();
        services.AddWeaviateLocal(eagerInitialization: false);
        services.AddWeaviateContext<TestStoreContext>();

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<WeaviateOptions>>().Value;

        Assert.NotNull(options.Headers);
        Assert.True(options.Headers.ContainsKey("X-Weaviate-Client-Integration"));
        Assert.Matches(
            @"^weaviate-client-csharp-managed/\d+",
            options.Headers["X-Weaviate-Client-Integration"]
        );
    }

    #region Test Types

    [WeaviateCollection("Products")]
    public class Product
    {
        public string Name { get; set; } = string.Empty;
    }

    [WeaviateCollection("Categories")]
    public class Category
    {
        public string Name { get; set; } = string.Empty;
    }

    public class TestStoreContext : WeaviateContext
    {
        public TestStoreContext(WeaviateClient client)
            : base(client) { }

        public TestStoreContext(
            WeaviateClient client,
            WeaviateContextOptions<TestStoreContext> options
        )
            : base(client, options) { }

        public CollectionSet<Product> Products { get; set; } = null!;
    }

    public class SecondContext : WeaviateContext
    {
        public SecondContext(WeaviateClient client)
            : base(client) { }

        public SecondContext(WeaviateClient client, WeaviateContextOptions<SecondContext> options)
            : base(client, options) { }

        public CollectionSet<Category> Categories { get; set; } = null!;
    }

    public class OverridingContext : WeaviateContext
    {
        public OverridingContext(WeaviateClient client)
            : base(client) { }

        public OverridingContext(
            WeaviateClient client,
            WeaviateContextOptions<OverridingContext> options
        )
            : base(client, options) { }

        protected override void OnConfiguring(WeaviateContextOptionsBuilder options)
        {
            options.UseAutoMigrate(true);
        }
    }

    /// <summary>
    /// Context with only the single-parameter constructor (backward compat).
    /// </summary>
    public class SingleCtorContext : WeaviateContext
    {
        public SingleCtorContext(WeaviateClient client)
            : base(client) { }
    }

    #endregion
}
