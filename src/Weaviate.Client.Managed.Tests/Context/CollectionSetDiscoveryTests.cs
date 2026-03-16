using Xunit;

namespace Weaviate.Client.Managed.Tests.Context;

public class CollectionSetDiscoveryTests
{
    [Fact]
    public void DiscoverCollectionSets_FindsCollectionSetProperties()
    {
        // Act
        var discovered = CollectionSetDiscovery
            .DiscoverCollectionSets(typeof(TestContext))
            .ToList();

        // Assert
        Assert.Equal(2, discovered.Count);
    }

    [Fact]
    public void DiscoverCollectionSets_ExtractsCorrectEntityTypes()
    {
        // Act
        var discovered = CollectionSetDiscovery
            .DiscoverCollectionSets(typeof(TestContext))
            .ToList();

        // Assert
        Assert.Contains(discovered, d => d.EntityType == typeof(Book));
        Assert.Contains(discovered, d => d.EntityType == typeof(Author));
    }

    [Fact]
    public void DiscoverCollectionSets_UsesAttributeNameWhenSpecified()
    {
        // Act
        var discovered = CollectionSetDiscovery
            .DiscoverCollectionSets(typeof(TestContext))
            .ToList();

        // Assert
        var bookSet = discovered.First(d => d.EntityType == typeof(Book));
        Assert.Equal("Books", bookSet.CollectionName);
    }

    [Fact]
    public void DiscoverCollectionSets_FallsBackToTypeNameWhenNoAttribute()
    {
        // Act
        var discovered = CollectionSetDiscovery
            .DiscoverCollectionSets(typeof(TestContext))
            .ToList();

        // Assert
        var authorSet = discovered.First(d => d.EntityType == typeof(Author));
        Assert.Equal("Author", authorSet.CollectionName); // Uses type name since no Name on attribute
    }

    [Fact]
    public void DiscoverCollectionSets_IgnoresNonCollectionSetProperties()
    {
        // Act
        var discovered = CollectionSetDiscovery
            .DiscoverCollectionSets(typeof(ContextWithMixedProperties))
            .ToList();

        // Assert
        Assert.Single(discovered);
        Assert.Equal(typeof(Book), discovered[0].EntityType);
    }

    [Fact]
    public void DiscoverCollectionSets_EmptyForContextWithNoCollectionSets()
    {
        // Act
        var discovered = CollectionSetDiscovery
            .DiscoverCollectionSets(typeof(EmptyContext))
            .ToList();

        // Assert
        Assert.Empty(discovered);
    }

    #region Test Types

    private abstract class TestContext : WeaviateContext
    {
        protected TestContext(WeaviateClient client)
            : base(client) { }

        public CollectionSet<Book> Books { get; set; } = null!;
        public CollectionSet<Author> Authors { get; set; } = null!;
    }

    private abstract class ContextWithMixedProperties : WeaviateContext
    {
        protected ContextWithMixedProperties(WeaviateClient client)
            : base(client) { }

        public CollectionSet<Book> Books { get; set; } = null!;
        public string SomeString { get; set; } = string.Empty;
        public int SomeNumber { get; set; }
        public List<Book>? SomeList { get; set; }
    }

    private abstract class EmptyContext : WeaviateContext
    {
        protected EmptyContext(WeaviateClient client)
            : base(client) { }
    }

    [WeaviateCollection("Books")]
    private class Book
    {
        public Guid UUID { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    [WeaviateCollection]
    private class Author
    {
        public Guid UUID { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    #endregion
}
