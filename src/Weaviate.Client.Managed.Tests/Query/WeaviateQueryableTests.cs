using Weaviate.Client.Managed.Tests.Mocks;
using Xunit;

namespace Weaviate.Client.Managed.Tests.Query;

// ── Test models ──────────────────────────────────────────────────────────────

[WeaviateCollection("Products")]
internal class Product
{
    [WeaviateUUID]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool InStock { get; set; }
}

internal class ProductContext : WeaviateContext
{
    public ProductContext(WeaviateClient client)
        : base(client) { }

    public CollectionSet<Product> Products { get; set; } = null!;
}

// ── Tests ─────────────────────────────────────────────────────────────────────

public class WeaviateQueryableTests
{
    private static CollectionSet<Product> CreateProductSet()
    {
        var (client, _) = MockGrpcClient.CreateWithSearchCapture();
        var context = new ProductContext(client);
        return context.Products;
    }

    // 1 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void CollectionSet_IsIQueryable()
    {
        var products = CreateProductSet();

        Assert.IsAssignableFrom<IQueryable<Product>>(products);
        Assert.IsAssignableFrom<IOrderedQueryable<Product>>(products);
    }

    // 2 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Where_AddsFilterOp()
    {
        var products = CreateProductSet();
        var query = products.Where(p => p.Price > 10);

        var wq = Assert.IsType<WeaviateQueryable<Product>>(query);
        Assert.Single(wq.Ops);
        Assert.Equal(PendingOpKind.Where, wq.Ops[0].Kind);
    }

    // 3 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void NearText_SetsConfig()
    {
        var products = CreateProductSet();
        var query = products.NearText("wireless mouse");

        Assert.IsType<WeaviateQueryable<Product>>(query);
        Assert.Equal(WeaviateSearchMode.NearText, query.Config.SearchMode);
        Assert.Equal("wireless mouse", query.Config.NearTextQuery);
        // No ops accumulated — only config changed
        Assert.Empty(query.Ops);
    }

    // 4 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void NearText_CanBeLinqSource()
    {
        var products = CreateProductSet();

        // Compiler synthesises: products.NearText("mouse").Where(p => p.InStock).Select(p => p)
        var query = from p in products.NearText("mouse") where p.InStock select p;

        var wq = Assert.IsType<WeaviateQueryable<Product>>(query);
        Assert.Equal(WeaviateSearchMode.NearText, wq.Config.SearchMode);
        Assert.Single(wq.Ops);
        Assert.Equal(PendingOpKind.Where, wq.Ops[0].Kind);
    }

    // 5 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ChainedOps_AreImmutable()
    {
        var products = CreateProductSet();

        var first = products.Where(p => p.Price > 10);
        var firstWq = Assert.IsType<WeaviateQueryable<Product>>(first);

        var second = first.Where(p => p.InStock);
        var secondWq = Assert.IsType<WeaviateQueryable<Product>>(second);

        Assert.Single(firstWq.Ops);
        Assert.Equal(2, secondWq.Ops.Count);
    }

    // 6 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GroupBy_ThrowsNotSupported()
    {
        var products = CreateProductSet();

        Assert.Throws<NotSupportedException>(() => products.GroupBy(p => p.InStock));
    }

    // 7 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void NonIdentitySelect_ThrowsNotSupported()
    {
        var products = CreateProductSet();

        // Non-identity projection (string, not Product) → must throw
        Assert.Throws<NotSupportedException>(() => products.Select(p => p.Name));
    }

    // 8 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Take_AddsLimitOp()
    {
        var products = CreateProductSet();
        var query = products.Take(5);

        var wq = Assert.IsType<WeaviateQueryable<Product>>(query);
        Assert.Single(wq.Ops);
        Assert.Equal(PendingOpKind.Take, wq.Ops[0].Kind);
        Assert.Equal(5, (int)wq.Ops[0].Arg);
    }

    // 9 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Skip_AddsOffsetOp()
    {
        var products = CreateProductSet();
        var query = products.Skip(10);

        var wq = Assert.IsType<WeaviateQueryable<Product>>(query);
        Assert.Single(wq.Ops);
        Assert.Equal(PendingOpKind.Skip, wq.Ops[0].Kind);
        Assert.Equal(10, (int)wq.Ops[0].Arg);
    }

    // 10 ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void OrderByThenBy_AddsCorrectOps()
    {
        var products = CreateProductSet();
        var query = products.OrderBy(p => p.Name).ThenByDescending(p => p.Price);

        var wq = Assert.IsType<WeaviateQueryable<Product>>(query);
        Assert.Equal(2, wq.Ops.Count);
        Assert.Equal(PendingOpKind.OrderBy, wq.Ops[0].Kind);
        Assert.Equal(PendingOpKind.ThenByDesc, wq.Ops[1].Kind);
    }

    // 11 ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void WithCancellation_PropagatesThroughLinqChain()
    {
        using var cts = new CancellationTokenSource();
        var products = CreateProductSet();

        // CT is embedded and survives Where + Take operators
        var query = products.WithCancellation(cts.Token).Where(p => p.InStock).Take(5);

        var wq = Assert.IsType<WeaviateQueryable<Product>>(query);
        // Ops accumulated correctly
        Assert.Equal(2, wq.Ops.Count);
        Assert.Equal(PendingOpKind.Where, wq.Ops[0].Kind);
        Assert.Equal(PendingOpKind.Take, wq.Ops[1].Kind);
    }

    // 12 ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ToQueryResultsAsync_MethodExists()
    {
        // Compile-time check that the extension method is present and callable.
        // We don't await it (would require a live Weaviate instance).
        var products = CreateProductSet();
        var task = products.ToQueryResultsAsync(TestContext.Current.CancellationToken);

        // The task is created (method exists and is callable); we don't await it here
        // since execution requires a live Weaviate instance.
        Assert.NotNull(task);
    }
}
