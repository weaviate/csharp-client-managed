using Xunit;

namespace Weaviate.Client.Managed.Tests.Integration;

/// <summary>
/// Integration tests for WeaviateContext and related functionality.
/// Requires a running Weaviate instance.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Integration")]
public class WeaviateContextIntegrationTests : IntegrationTestBase
{
    private TestBookstoreContext CreateContext()
    {
        return new TestBookstoreContext(Client);
    }

    private async Task CreateCollections()
    {
        await Client.Collections.CreateFromClass<ContextTestBook>(
            cancellationToken: TestContext.Current.CancellationToken
        );
    }

    private async Task CreateAllCollections()
    {
        await Client.Collections.CreateFromClass<ContextTestBook>(
            cancellationToken: TestContext.Current.CancellationToken
        );
        await Client.Collections.CreateFromClass<ContextTestAuthor>(
            cancellationToken: TestContext.Current.CancellationToken
        );
    }

    #region Context CRUD Tests

    [Fact]
    public async Task Context_Insert_InsertsEntityAndAssignsId()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        var book = new ContextTestBook { Title = "Foundation", Price = 19.99 };

        // Act
        await context.Insert(book, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, book.Id);

        // Verify it was inserted by querying
        var found = await context.Books.Find(book.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Equal("Foundation", found.Title);
    }

    [Fact]
    public async Task Context_Update_UpdatesExistingEntity()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        var book = new ContextTestBook { Title = "Foundation", Price = 19.99 };
        await context.Insert(book, TestContext.Current.CancellationToken);

        // Act
        book.Price = 24.99;
        await context.Update(book, TestContext.Current.CancellationToken);

        // Assert
        var found = await context.Books.Find(book.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(found);
        Assert.Equal(24.99, found.Price);
    }

    [Fact]
    public async Task Context_Delete_RemovesEntity()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        var book = new ContextTestBook { Title = "Foundation", Price = 19.99 };
        await context.Insert(book, TestContext.Current.CancellationToken);
        var insertedId = book.Id;

        // Act
        await context.Delete<ContextTestBook>(insertedId);

        // Assert
        var found = await context.Books.Find(insertedId, TestContext.Current.CancellationToken);
        Assert.Null(found);
    }

    [Fact]
    public async Task Context_Query_ReturnsAllEntities()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        await context
            .Insert(
                new ContextTestBook { Title = "Foundation", Price = 19.99 },
                new ContextTestBook { Title = "I, Robot", Price = 14.99 },
                new ContextTestBook { Title = "Dune", Price = 24.99 }
            )
            .Execute(TestContext.Current.CancellationToken);

        // Wait for data to be indexed
        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Act - query without filter to get all entities
        var results = await context
            .Query<ContextTestBook>()
            .Limit(10)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        var books = results.Select(r => r.Object).ToList();
        Assert.Equal(3, books.Count);
        Assert.Contains(books, b => b.Title == "Foundation");
        Assert.Contains(books, b => b.Title == "I, Robot");
        Assert.Contains(books, b => b.Title == "Dune");
    }

    #endregion

    #region CollectionSet Tests

    [Fact]
    public async Task CollectionSet_Insert_InsertsMultipleEntities()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        var books = new[]
        {
            new ContextTestBook { Title = "Foundation", Price = 19.99 },
            new ContextTestBook { Title = "I, Robot", Price = 14.99 },
        };

        // Act
        await context.Books.Insert(books);

        // Assert
        foreach (var book in books)
        {
            Assert.NotEqual(Guid.Empty, book.Id);
            var found = await context.Books.Find(book.Id, TestContext.Current.CancellationToken);
            Assert.NotNull(found);
        }
    }

    [Fact]
    public async Task CollectionSet_Query_ReturnsAllEntities()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        await context.Books.Insert(
            new ContextTestBook { Title = "Cheap Book", Price = 5.99 },
            new ContextTestBook { Title = "Expensive Book", Price = 99.99 }
        );

        // Wait for data to be indexed
        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Act - query without filter to verify insert works
        var results = await context
            .Books.Query()
            .Limit(10)
            .Execute(TestContext.Current.CancellationToken);

        // Assert - both books should be present
        var books = results.Select(r => r.Object).ToList();
        Assert.Equal(2, books.Count);
        Assert.Contains(books, b => b.Title == "Cheap Book");
        Assert.Contains(books, b => b.Title == "Expensive Book");
    }

    #endregion

    #region Batch Operations Tests

    [Fact]
    public async Task Insert_MultipleCollections_AssignsIds()
    {
        // Arrange
        await CreateAllCollections();
        var context = CreateContext();

        var book1 = new ContextTestBook { Title = "Foundation", Price = 19.99 };
        var book2 = new ContextTestBook { Title = "I, Robot", Price = 14.99 };
        var author = new ContextTestAuthor { Name = "Isaac Asimov" };

        // Act — each collection inserted independently
        await context.Books.Insert(book1, book2).Execute(TestContext.Current.CancellationToken);
        await context.Authors.Insert(author, TestContext.Current.CancellationToken);

        // Assert - IDs should be assigned
        Assert.NotEqual(Guid.Empty, book1.Id);
        Assert.NotEqual(Guid.Empty, book2.Id);
        Assert.NotEqual(Guid.Empty, author.Id);

        // Wait for data to be indexed
        await Task.Delay(500, TestContext.Current.CancellationToken);

        var foundBook = await context.Books.Find(book1.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(foundBook);

        var foundAuthor = await context.Authors.Find(
            author.Id,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(foundAuthor);
    }

    [Fact]
    public async Task Insert_ChainedBatches_ExecutesInOrder()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        var book1 = new ContextTestBook { Title = "Foundation", Price = 19.99 };
        var book2 = new ContextTestBook { Title = "I, Robot", Price = 14.99 };
        var book3 = new ContextTestBook { Title = "Dune", Price = 24.99 };

        // Act — two separate InsertMany calls executed in order
        var allBooks = await context
            .Books.Insert(book1, book2)
            .Insert(book3)
            .Execute(TestContext.Current.CancellationToken);

        // Assert - all IDs assigned, all books returned
        Assert.Equal(3, allBooks.Length);
        Assert.NotEqual(Guid.Empty, book1.Id);
        Assert.NotEqual(Guid.Empty, book2.Id);
        Assert.NotEqual(Guid.Empty, book3.Id);
    }

    [Fact]
    public async Task Delete_RemovesSpecificEntity()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        var book1 = new ContextTestBook { Title = "Book 1", Price = 9.99 };
        var book2 = new ContextTestBook { Title = "Book 2", Price = 19.99 };
        await context.Books.Insert(book1, book2).Execute(TestContext.Current.CancellationToken);

        // Act
        await context.Delete<ContextTestBook>(book1.Id);

        // Assert
        var found1 = await context.Books.Find(book1.Id, TestContext.Current.CancellationToken);
        var found2 = await context.Books.Find(book2.Id, TestContext.Current.CancellationToken);
        Assert.Null(found1);
        Assert.NotNull(found2);
    }

    [Fact]
    public async Task Batch_CrossCollection_InsertsAllEntities()
    {
        // Arrange
        await CreateAllCollections();
        var context = CreateContext();

        var author = new ContextTestAuthor { Name = "Isaac Asimov" };
        var book1 = new ContextTestBook { Title = "Foundation", Price = 19.99 };
        var book2 = new ContextTestBook { Title = "I, Robot", Price = 14.99 };

        // Act — cross-collection batch: all entities inserted, IDs assigned
        await context
            .Batch()
            .Insert(book1, book2)
            .Insert(author)
            .Execute(TestContext.Current.CancellationToken);

        // Assert — all IDs assigned
        Assert.NotEqual(Guid.Empty, book1.Id);
        Assert.NotEqual(Guid.Empty, book2.Id);
        Assert.NotEqual(Guid.Empty, author.Id);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        var foundBook = await context.Books.Find(book1.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(foundBook);

        var foundAuthor = await context.Authors.Find(
            author.Id,
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(foundAuthor);
    }

    #endregion

    #region Scoping Tests

    [Fact]
    public void ForTenant_ReturnsSameContextType()
    {
        var context = CreateContext();

        var scoped = context.ForTenant("tenant1");

        Assert.IsType<TestBookstoreContext>(scoped);
    }

    [Fact]
    public void ForTenant_SetsTenantProperty()
    {
        var context = CreateContext();

        var scoped = context.ForTenant("acme");

        Assert.Equal("acme", scoped.Tenant);
        Assert.Null(context.Tenant);
    }

    [Fact]
    public void WithConsistencyLevel_SetsConsistencyLevelProperty()
    {
        var context = CreateContext();

        var scoped = context.WithConsistencyLevel(ConsistencyLevels.All);

        Assert.Equal(ConsistencyLevels.All, scoped.ConsistencyLevel);
        Assert.Null(context.ConsistencyLevel);
    }

    [Fact]
    public void ForTenant_CreatesIndependentCollectionSets()
    {
        var context = CreateContext();
        var scoped = (TestBookstoreContext)context.ForTenant("tenant1");

        Assert.NotSame(context.Books, scoped.Books);
        Assert.NotSame(context.Authors, scoped.Authors);
    }

    [Fact]
    public void Scoping_ChainsCorrectly()
    {
        var context = CreateContext();

        var scoped = context.ForTenant("acme").WithConsistencyLevel(ConsistencyLevels.Quorum);

        Assert.Equal("acme", scoped.Tenant);
        Assert.Equal(ConsistencyLevels.Quorum, scoped.ConsistencyLevel);
    }

    [Fact]
    public void ForTenant_PreservesConsistencyLevel()
    {
        var context = CreateContext();

        var scoped = context.WithConsistencyLevel(ConsistencyLevels.All).ForTenant("acme");

        Assert.Equal("acme", scoped.Tenant);
        Assert.Equal(ConsistencyLevels.All, scoped.ConsistencyLevel);
    }

    #endregion

    #region LINQ / IQueryable Tests

    [Fact]
    public async Task LINQ_QuerySyntax_FiltersCorrectly()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        await context
            .Books.Insert(
                new ContextTestBook { Title = "Cheap", Price = 5.99 },
                new ContextTestBook { Title = "Expensive", Price = 199.99 },
                new ContextTestBook { Title = "Mid", Price = 50.00 }
            )
            .Execute(TestContext.Current.CancellationToken);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Act — LINQ query syntax with a filter
        var results = await (from b in context.Books where b.Price > 10 select b);

        // Assert — only Expensive and Mid should match
        var titles = results.Select(b => b.Title).ToList();
        Assert.DoesNotContain("Cheap", titles);
        Assert.Contains("Expensive", titles);
        Assert.Contains("Mid", titles);
    }

    [Fact]
    public async Task LINQ_Take_LimitsResults()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        await context
            .Books.Insert(
                new ContextTestBook { Title = "A", Price = 1.00 },
                new ContextTestBook { Title = "B", Price = 2.00 },
                new ContextTestBook { Title = "C", Price = 3.00 },
                new ContextTestBook { Title = "D", Price = 4.00 }
            )
            .Execute(TestContext.Current.CancellationToken);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Act
        var results = await context.Books.Take(2);

        // Assert
        Assert.Equal(2, results.Count());
    }

    [Fact]
    public async Task LINQ_ToQueryResultsAsync_HasUUIDs()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        var book = new ContextTestBook { Title = "Foundation", Price = 19.99 };
        await context.Books.Insert(book, TestContext.Current.CancellationToken);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Act
        var results = await context.Books.ToQueryResultsAsync(
            TestContext.Current.CancellationToken
        );

        // Assert — UUIDs are populated
        Assert.All(results, r => Assert.NotEqual(Guid.Empty, r.UUID));
    }

    [Fact]
    public async Task LINQ_FirstOrDefaultAsync_ReturnsFirst()
    {
        // Arrange
        await CreateCollections();
        var context = CreateContext();

        await context.Books.Insert(
            new ContextTestBook { Title = "Only Book", Price = 9.99 },
            TestContext.Current.CancellationToken
        );

        await Task.Delay(500, TestContext.Current.CancellationToken);

        // Act
        var book = await context.Books.FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(book);
        Assert.Equal("Only Book", book.Title);
    }

    #endregion

    #region Test Context and Entities

    private class TestBookstoreContext(WeaviateClient client) : WeaviateContext(client)
    {
        public CollectionSet<ContextTestBook> Books { get; set; } = null!;
        public CollectionSet<ContextTestAuthor> Authors { get; set; } = null!;
    }

    #endregion
}

/// <summary>
/// Test entity for integration tests.
/// </summary>
[WeaviateCollection("ContextTestBook")]
public class ContextTestBook
{
    [WeaviateUUID]
    public Guid Id { get; set; }

    [Property]
    public string Title { get; set; } = string.Empty;

    [Property]
    public double Price { get; set; }
}

/// <summary>
/// Test author entity for integration tests.
/// </summary>
[WeaviateCollection("ContextTestAuthor")]
public class ContextTestAuthor
{
    [WeaviateUUID]
    public Guid Id { get; set; }

    [Property]
    public string Name { get; set; } = string.Empty;
}
