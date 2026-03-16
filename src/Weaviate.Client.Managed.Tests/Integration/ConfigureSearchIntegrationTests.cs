using Weaviate.Client.Managed.Tests.TestData;
using Xunit;

namespace Weaviate.Client.Managed.Tests.Integration;

[Trait("Category", "Integration")]
[Collection("Integration")]
public class ConfigureSearchIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task ManagedCollection_ConfigureSearch_AppliesLimit()
    {
        var collection = await Client.Collections.CreateManaged<SampleData.Article>(
            cancellationToken: TestContext.Current.CancellationToken
        );
        var inner = Client.Collections.Use(collection.Name);
        foreach (var article in SampleData.Articles)
            await inner.Data.Insert<SampleData.Article>(
                article,
                cancellationToken: TestContext.Current.CancellationToken
            );
        await Task.Delay(500, TestContext.Current.CancellationToken);

        var results = await collection
            .Query<ArticleWithConfigureSearch>()
            .Execute(TestContext.Current.CancellationToken);
        var items = results.ToList();
        Assert.Equal(2, items.Count); // Should be limited by ConfigureSearch
    }

    [Fact]
    public async Task WeaviateContext_ConfigureSearch_AppliesLimit()
    {
        var context = new TestBookstoreContext(Client);
        await Client.Collections.CreateFromClass<SampleData.Article>(
            cancellationToken: TestContext.Current.CancellationToken
        );
        foreach (var article in SampleData.Articles)
            await context.Books.Insert(
                article,
                cancellationToken: TestContext.Current.CancellationToken
            );
        await Task.Delay(500, TestContext.Current.CancellationToken);

        var results = await context
            .Books.Query()
            .Project<ArticleWithConfigureSearch>()
            .Execute(TestContext.Current.CancellationToken);
        var items = results.ToList();
        Assert.Equal(2, items.Count); // Should be limited by ConfigureSearch
    }

    [QueryProjection<SampleData.Article>]
    public class ArticleWithConfigureSearch
    {
        public string Title { get; set; } = "";

        public static void ConfigureSearch(QueryConfig<SampleData.Article> q) => q.Limit(2u);
    }

    [Fact]
    public async Task EntityLevel_ConfigureSearch_AppliesDefaults()
    {
        var collection = await Client.Collections.CreateManaged<EntityWithDefaults>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        foreach (var article in SampleData.Articles)
            await collection.Insert(
                new EntityWithDefaults { Title = article.Title },
                cancellationToken: TestContext.Current.CancellationToken
            );
        await Task.Delay(500, TestContext.Current.CancellationToken);

        var results = await collection.Query().Execute(TestContext.Current.CancellationToken);
        var items = results.ToList();
        Assert.Equal(3, items.Count); // Should be limited by entity's ConfigureSearch
    }

    [Fact]
    public async Task Precedence_ProjectionOverridesEntity()
    {
        var collection = await Client.Collections.CreateManaged<EntityWithDefaults>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        foreach (var article in SampleData.Articles)
            await collection.Insert(
                new EntityWithDefaults { Title = article.Title },
                cancellationToken: TestContext.Current.CancellationToken
            );
        await Task.Delay(500, TestContext.Current.CancellationToken);

        var results = await collection
            .Query<ProjectionWithDifferentLimit>()
            .Execute(TestContext.Current.CancellationToken);
        var items = results.ToList();
        Assert.Equal(1, items.Count); // Projection limit (1) overrides entity limit (3)
    }

    [Fact]
    public async Task Precedence_ExplicitCallOverridesEverything()
    {
        var collection = await Client.Collections.CreateManaged<EntityWithDefaults>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        foreach (var article in SampleData.Articles)
            await collection.Insert(
                new EntityWithDefaults { Title = article.Title },
                cancellationToken: TestContext.Current.CancellationToken
            );
        await Task.Delay(500, TestContext.Current.CancellationToken);

        var results = await collection
            .Query<ProjectionWithDifferentLimit>()
            .Limit(2u) // Explicit call overrides projection (1) and entity (3)
            .Execute(TestContext.Current.CancellationToken);
        var items = results.ToList();
        Assert.Equal(2, items.Count); // Explicit limit wins (2 instead of projection's 1 or entity's 3)
    }

    [Fact]
    public async Task ConfigureSearch_WithWhereFilter_AppliesFilter()
    {
        var collection = await Client.Collections.CreateManaged<EntityWithWhereFilter>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        await collection.Insert(
            new EntityWithWhereFilter { Title = "Active Article", WordCount = 100 },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await collection.Insert(
            new EntityWithWhereFilter { Title = "Short Article", WordCount = 50 },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await collection.Insert(
            new EntityWithWhereFilter { Title = "Another Active", WordCount = 150 },
            cancellationToken: TestContext.Current.CancellationToken
        );
        await Task.Delay(500, TestContext.Current.CancellationToken);

        var results = await collection.Query().Execute(TestContext.Current.CancellationToken);
        var items = results.ToList();
        Assert.Equal(2, items.Count); // Should only return items with WordCount > 75
        Assert.All(items, item => Assert.True(item.Object.WordCount > 75));
    }

    [WeaviateCollection("EntityWithDefaults")]
    public class EntityWithDefaults
    {
        [Property]
        public string Title { get; set; } = "";

        public static void ConfigureSearch(QueryConfig<EntityWithDefaults> q) => q.Limit(3u);
    }

    [WeaviateCollection("EntityWithWhereFilter")]
    public class EntityWithWhereFilter
    {
        [Property]
        public string Title { get; set; } = "";

        [Property]
        public int WordCount { get; set; }

        public static void ConfigureSearch(QueryConfig<EntityWithWhereFilter> q) =>
            q.Where(e => e.WordCount > 75).OrderByDescending(e => e.WordCount);
    }

    [QueryProjection<EntityWithDefaults>]
    public class ProjectionWithDifferentLimit
    {
        public string Title { get; set; } = "";

        public static void ConfigureSearch(QueryConfig<EntityWithDefaults> q) => q.Limit(1u);
    }

    public class TestBookstoreContext(WeaviateClient client) : WeaviateContext(client)
    {
        public CollectionSet<SampleData.Article> Books { get; set; } = null!;
    }
}
