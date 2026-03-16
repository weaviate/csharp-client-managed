using Xunit;

namespace Weaviate.Client.Managed.Tests.Integration;

/// <summary>
/// Integration tests for query projections.
/// Requires a running Weaviate instance.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Integration")]
public class ProjectionIntegrationTests : IntegrationTestBase
{
    private async Task<ManagedCollection<SampleData.Article>> SetupCollection()
    {
        var collection = await Client.Collections.CreateManaged<SampleData.Article>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Insert test data from shared dataset
        var inner = Client.Collections.Use(collection.Name);
        foreach (var article in SampleData.Articles)
        {
            await inner.Data.Insert<SampleData.Article>(article);
        }

        // Wait for data to be indexed
        await Task.Delay(500);

        return collection;
    }

    [Fact]
    public async Task Project_BasicProjection_ReturnsSubsetOfProperties()
    {
        // Arrange
        var collection = await SetupCollection();

        // Act
        var results = await collection
            .Query<ArticleTitleOnly>()
            .Limit(10)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        var items = results.ToList();
        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.False(string.IsNullOrEmpty(item.Object.Title)));
    }

    [Fact]
    public async Task Project_WithMapFrom_MapsRenamedProperties()
    {
        // Arrange
        var collection = await SetupCollection();

        // Act
        var results = await collection
            .Query<ArticleWithMapFrom>()
            .Limit(10)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        var items = results.ToList();
        Assert.Equal(3, items.Count);
        Assert.All(items, item => Assert.True(item.Object.Words > 0));
    }

    [Fact]
    public async Task Project_WithWeaviateUUID_PopulatesId()
    {
        // Arrange
        var collection = await SetupCollection();

        // Act
        var results = await collection
            .Query<ArticleWithId>()
            .Limit(10)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        var items = results.ToList();
        Assert.Equal(3, items.Count);
        Assert.All(
            items,
            item =>
            {
                Assert.NotEqual(Guid.Empty, item.Object.Id);
                Assert.False(string.IsNullOrEmpty(item.Object.Title));
            }
        );
    }

    [Fact]
    public async Task Project_WithFilter_AppliesFilter()
    {
        // Arrange
        var collection = await SetupCollection();

        // Act
        var results = await collection
            .Query<ArticleTitleOnly>()
            .Where(a => a.WordCount > 180)
            .Limit(10)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        var items = results.ToList();
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Project_WithLimit_LimitsResults()
    {
        // Arrange
        var collection = await SetupCollection();

        // Act
        var results = await collection
            .Query<ArticleTitleOnly>()
            .Limit(2)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        var items = results.ToList();
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task Project_FullProjection_CombinesAllFeatures()
    {
        // Arrange
        var collection = await SetupCollection();

        // Act
        var results = await collection
            .Query<ArticleFullProjection>()
            .Limit(10)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        var items = results.ToList();
        Assert.Equal(3, items.Count);
        Assert.All(
            items,
            item =>
            {
                Assert.NotEqual(Guid.Empty, item.Object.Id);
                Assert.False(string.IsNullOrEmpty(item.Object.Title));
                Assert.True(item.Object.Words > 0);
                Assert.True(item.Object.Cost > 0);
            }
        );
    }

    [Fact]
    public async Task Project_ChainedAfterWhere_Works()
    {
        // Arrange
        var collection = await SetupCollection();

        // Act - Query with projection can filter on full entity type
        var results = await collection
            .Query<ArticleTitleOnly>()
            .Where(a => a.Price > 30)
            .Limit(10)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        var items = results.ToList();
        Assert.Equal(2, items.Count);
    }

    #region Test Types

    [QueryProjection<SampleData.Article>]
    public class ArticleTitleOnly
    {
        public string Title { get; set; } = "";
    }

    [QueryProjection<SampleData.Article>]
    public class ArticleWithMapFrom
    {
        public string Title { get; set; } = "";

        [MapFrom("WordCount")]
        public int Words { get; set; }
    }

    [QueryProjection<SampleData.Article>]
    public class ArticleWithId
    {
        [WeaviateUUID]
        public Guid Id { get; set; }

        public string Title { get; set; } = "";
    }

    [QueryProjection<SampleData.Article>]
    public class ArticleFullProjection
    {
        [WeaviateUUID]
        public Guid Id { get; set; }

        public string Title { get; set; } = "";

        [MapFrom("WordCount")]
        public int Words { get; set; }

        [MapFrom("Price")]
        public double Cost { get; set; }
    }

    #endregion
}
