using Weaviate.Client.Models.Typed;
using Xunit;

namespace Weaviate.Client.Managed.Tests.Integration;

/// <summary>
/// Integration tests for aggregate operations using ManagedCollection.
///
/// To run these tests:
/// 1. Start Weaviate: docker-compose -f docker-compose.integration.yml up -d
/// 2. Run tests: dotnet test --filter "Category=Integration"
/// 3. Stop Weaviate: docker-compose -f docker-compose.integration.yml down
/// </summary>
[Trait("Category", "Integration")]
[Collection("Integration")]
public class AggregateIntegrationTests : IntegrationTestBase
{
    #region Test Models

    public class Product
    {
        [Property(DataType.Text)]
        public string Name { get; set; } = "";

        [Property(DataType.Text)]
        public string Category { get; set; } = "";

        [Property(DataType.Number)]
        public decimal Price { get; set; }

        [Property(DataType.Int)]
        public int Quantity { get; set; }

        [Property(DataType.Bool)]
        public bool InStock { get; set; }

        [Property(DataType.Date)]
        public DateTime CreatedAt { get; set; }

        [Vector<Vectorizer.Text2VecTransformers>(TextFields = new[] { "name", "category" })]
        public float[]? Embedding { get; set; }
    }

    [QueryAggregate<Product>]
    public class ProductStats
    {
        [Metrics(
            Metric.Number.Mean,
            Metric.Number.Sum,
            Metric.Number.Count,
            Metric.Number.Min,
            Metric.Number.Max
        )]
        public Aggregate.Number Price { get; set; } = null!;

        [Metrics(Metric.Integer.Mean, Metric.Integer.Sum, Metric.Integer.Count)]
        public Aggregate.Integer Quantity { get; set; } = null!;
    }

    [QueryAggregate<Product>]
    public class CategoryStats
    {
        [Metrics(Metric.Number.Mean, Metric.Number.Count)]
        public Aggregate.Number Price { get; set; } = null!;
    }

    // Single-metric extraction models
    [QueryAggregate<Product>]
    public class SingleMetricNumberStats
    {
        [Metrics("price", Metric.Number.Mean)]
        public double? AveragePrice { get; set; }

        [Metrics("price", Metric.Number.Count)]
        public long? PriceCount { get; set; }

        [Metrics("price", Metric.Number.Sum)]
        public double? TotalPrice { get; set; }

        [Metrics("price", Metric.Number.Min)]
        public double? MinimumPrice { get; set; }

        [Metrics("price", Metric.Number.Max)]
        public double? MaximumPrice { get; set; }
    }

    [QueryAggregate<Product>]
    public class SingleMetricIntegerStats
    {
        [Metrics("quantity", Metric.Integer.Mean)]
        public double? AverageQuantity { get; set; }

        [Metrics("quantity", Metric.Integer.Sum)]
        public long? TotalQuantity { get; set; }

        [Metrics("quantity", Metric.Integer.Count)]
        public long? QuantityCount { get; set; }

        [Metrics("quantity", Metric.Integer.Min)]
        public long? MinimumQuantity { get; set; }

        [Metrics("quantity", Metric.Integer.Max)]
        public long? MaximumQuantity { get; set; }
    }

    [QueryAggregate<Product>]
    public class SingleMetricTextStats
    {
        [Metrics("category", Metric.Text.Count)]
        public long? CategoryCount { get; set; }
    }

    [QueryAggregate<Product>]
    public class SingleMetricBooleanStats
    {
        [Metrics("inStock", Metric.Boolean.TotalTrue)]
        public long? InStockCount { get; set; }

        [Metrics("inStock", Metric.Boolean.TotalFalse)]
        public long? OutOfStockCount { get; set; }

        [Metrics("inStock", Metric.Boolean.PercentageTrue)]
        public double? InStockPercentage { get; set; }

        [Metrics("inStock", Metric.Boolean.PercentageFalse)]
        public double? OutOfStockPercentage { get; set; }
    }

    [QueryAggregate<Product>]
    public class SingleMetricDateStats
    {
        [Metrics("createdAt", Metric.Date.Min)]
        public DateTime? EarliestDate { get; set; }

        [Metrics("createdAt", Metric.Date.Max)]
        public DateTime? LatestDate { get; set; }

        [Metrics("createdAt", Metric.Date.Count)]
        public long? DateCount { get; set; }
    }

    [QueryAggregate<Product>]
    public class MixedMetricsStats
    {
        // Combine full aggregate types with single-metric extraction
        [Metrics(Metric.Number.Mean, Metric.Number.Sum)]
        public Aggregate.Number Price { get; set; } = null!;

        [Metrics("quantity", Metric.Integer.Sum)]
        public long? TotalQuantity { get; set; }

        [Metrics("inStock", Metric.Boolean.TotalTrue)]
        public long? InStockCount { get; set; }
    }

    #endregion

    [Fact]
    public async Task Aggregate_OverAll_ReturnsTypedStats()
    {
        // Arrange
        var products = await Client.Collections.CreateManaged<Product>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        var testData = new[]
        {
            new Product
            {
                Name = "Widget A",
                Category = "Electronics",
                Price = 19.99m,
                Quantity = 100,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Widget B",
                Category = "Electronics",
                Price = 29.99m,
                Quantity = 50,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Gadget C",
                Category = "Home",
                Price = 49.99m,
                Quantity = 25,
                InStock = false,
                CreatedAt = DateTime.UtcNow,
            },
        };

        await products.InsertMany(testData, TestContext.Current.CancellationToken);

        // Act
        var result = await products
            .Aggregate.WithMetrics<ProductStats>()
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Properties);
        Assert.Equal(3, result.TotalCount);

        // Verify price statistics
        Assert.NotNull(result.Properties.Price.Mean);
        Assert.InRange(result.Properties.Price.Mean.Value, 33.0, 34.0); // Average of 19.99, 29.99, 49.99

        Assert.NotNull(result.Properties.Price.Sum);
        Assert.InRange(result.Properties.Price.Sum.Value, 99.0, 100.0); // Sum ~99.97

        Assert.Equal(3, result.Properties.Price.Count);
        Assert.InRange(result.Properties.Price.Minimum!.Value, 19.0, 20.0);
        Assert.InRange(result.Properties.Price.Maximum!.Value, 49.0, 50.0);

        // Verify quantity statistics
        Assert.NotNull(result.Properties.Quantity.Mean);
        Assert.InRange(result.Properties.Quantity.Mean.Value, 58.0, 59.0); // Average of 100, 50, 25

        Assert.Equal(175, result.Properties.Quantity.Sum); // 100 + 50 + 25
        Assert.Equal(3, result.Properties.Quantity.Count);
    }

    [Fact]
    public async Task Aggregate_WithFilter_ReturnsFilteredStats()
    {
        // Arrange
        var products = await Client.Collections.CreateManaged<Product>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        var testData = new[]
        {
            new Product
            {
                Name = "Widget A",
                Category = "Electronics",
                Price = 19.99m,
                Quantity = 100,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Widget B",
                Category = "Electronics",
                Price = 29.99m,
                Quantity = 50,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Gadget C",
                Category = "Home",
                Price = 49.99m,
                Quantity = 25,
                InStock = false,
                CreatedAt = DateTime.UtcNow,
            },
        };

        await products.InsertMany(
            testData,
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Act - Only aggregate products in stock
        var result = await products
            .Aggregate.Where(p => p.InStock == true)
            .WithMetrics<ProductStats>()
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount); // Only 2 products in stock

        // Price mean should only include in-stock items (19.99 + 29.99) / 2 = 24.99
        Assert.NotNull(result.Properties.Price.Mean);
        Assert.InRange(result.Properties.Price.Mean.Value, 24.0, 25.0);

        // Quantity sum should only include in-stock items (100 + 50 = 150)
        Assert.Equal(150, result.Properties.Quantity.Sum);
    }

    [Fact]
    public async Task Aggregate_GroupBy_ReturnsGroupedStats()
    {
        // Arrange
        var products = await Client.Collections.CreateManaged<Product>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        var testData = new[]
        {
            new Product
            {
                Name = "Widget A",
                Category = "Electronics",
                Price = 19.99m,
                Quantity = 100,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Widget B",
                Category = "Electronics",
                Price = 29.99m,
                Quantity = 50,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Gadget C",
                Category = "Home",
                Price = 49.99m,
                Quantity = 25,
                InStock = false,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Gadget D",
                Category = "Home",
                Price = 39.99m,
                Quantity = 30,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
        };

        await products.InsertMany(testData, TestContext.Current.CancellationToken);

        // Act - Group by category
        var result = await products
            .Aggregate.WithMetrics<CategoryStats>()
            .GroupBy(p => p.Category)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Groups.Count); // Electronics and Home

        // Find Electronics group
        var electronicsGroup = result.Groups.FirstOrDefault(g =>
            g.GroupedBy.Property == "category" && g.GroupedBy.Value.ToString() == "Electronics"
        );
        Assert.NotNull(electronicsGroup);
        Assert.Equal(2, electronicsGroup.TotalCount); // 2 products in Electronics

        Assert.NotNull(electronicsGroup.Properties.Price.Mean);
        Assert.InRange(electronicsGroup.Properties.Price.Mean.Value, 24.0, 25.0); // (19.99 + 29.99) / 2

        // Find Home group
        var homeGroup = result.Groups.FirstOrDefault(g =>
            g.GroupedBy.Property == "category" && g.GroupedBy.Value.ToString() == "Home"
        );
        Assert.NotNull(homeGroup);
        Assert.Equal(2, homeGroup.TotalCount); // 2 products in Home

        Assert.NotNull(homeGroup.Properties.Price.Mean);
        Assert.InRange(homeGroup.Properties.Price.Mean.Value, 44.0, 45.0); // (49.99 + 39.99) / 2
    }

    [Fact]
    public async Task Aggregate_GroupByWithFilter_ReturnsFilteredGroupedStats()
    {
        // Arrange
        var products = await Client.Collections.CreateManaged<Product>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        var testData = new[]
        {
            new Product
            {
                Name = "Widget A",
                Category = "Electronics",
                Price = 19.99m,
                Quantity = 100,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Widget B",
                Category = "Electronics",
                Price = 29.99m,
                Quantity = 50,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Gadget C",
                Category = "Home",
                Price = 49.99m,
                Quantity = 25,
                InStock = false,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Gadget D",
                Category = "Home",
                Price = 39.99m,
                Quantity = 30,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
        };

        await products.InsertMany(testData, TestContext.Current.CancellationToken);

        // Act - Group by category, but only for in-stock items
        var result = await products
            .Aggregate.Where(p => p.InStock == true)
            .WithMetrics<CategoryStats>()
            .GroupBy(p => p.Category)
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Groups.Count); // Still 2 groups, but filtered

        // Electronics group should have 2 items (both in stock)
        var electronicsGroup = result.Groups.FirstOrDefault(g =>
            g.GroupedBy.Property == "category" && g.GroupedBy.Value.ToString() == "Electronics"
        );
        Assert.NotNull(electronicsGroup);
        Assert.Equal(2, electronicsGroup.TotalCount);

        // Home group should have only 1 item (only Gadget D is in stock)
        var homeGroup = result.Groups.FirstOrDefault(g =>
            g.GroupedBy.Property == "category" && g.GroupedBy.Value.ToString() == "Home"
        );
        Assert.NotNull(homeGroup);
        Assert.Equal(1, homeGroup.TotalCount);
        Assert.NotNull(homeGroup.Properties.Price.Mean);
        Assert.InRange(homeGroup.Properties.Price.Mean.Value, 39.0, 40.0); // Only Gadget D
    }

    [Fact]
    public async Task Aggregate_NoMetrics_ReturnsCountOnly()
    {
        // Arrange
        var products = await Client.Collections.CreateManaged<Product>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        var testData = new[]
        {
            new Product
            {
                Name = "Widget A",
                Category = "Electronics",
                Price = 19.99m,
                Quantity = 100,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Widget B",
                Category = "Electronics",
                Price = 29.99m,
                Quantity = 50,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
        };

        await products.InsertMany(testData, TestContext.Current.CancellationToken);

        // Act - Aggregate without specifying metrics
        var result = await products
            .Aggregate.WithMetrics<CategoryStats>()
            .Execute(TestContext.Current.CancellationToken); // Using a minimal type

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.NotNull(result.Properties);
    }

    [Fact]
    public async Task CreateManaged_Managed_WorksTogether()
    {
        // Arrange - Create collection with CreateManaged
        var products1 = await Client.Collections.CreateManaged<Product>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        var testData = new Product
        {
            Name = "Widget",
            Category = "Electronics",
            Price = 19.99m,
            Quantity = 100,
            InStock = true,
            CreatedAt = DateTime.UtcNow,
        };

        await products1.Insert(testData, cancellationToken: TestContext.Current.CancellationToken);

        // Act - Get the same collection with Managed
        var products2 = await Client.Collections.Managed<Product>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        var result = await products2
            .Aggregate.WithMetrics<ProductStats>()
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(products1.Name, products2.Name);
    }

    [Fact]
    public async Task Managed_NonExistentCollection_ThrowsException()
    {
        // Act & Assert — the resolved prefixed name won't exist
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Client.Collections.Managed<Product>(
                cancellationToken: TestContext.Current.CancellationToken
            );
        });
    }

    [Fact]
    public async Task AsManaged_ConvertsExistingCollection()
    {
        // Arrange - Create collection the traditional way
        var collection = await Client.Collections.CreateFromClass<Product>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        // Act - Convert to mapped collection
        var products = collection.AsManaged<Product>();

        var testData = new Product
        {
            Name = "Widget",
            Category = "Electronics",
            Price = 19.99m,
            Quantity = 100,
            InStock = true,
            CreatedAt = DateTime.UtcNow,
        };

        await products.Insert(testData, cancellationToken: TestContext.Current.CancellationToken);

        var result = await products
            .Aggregate.WithMetrics<ProductStats>()
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(collection.Name, products.Name);
    }

    [Fact]
    public async Task ManagedCollection_QueryIntegration_Works()
    {
        // Arrange
        var products = await Client.Collections.CreateManaged<Product>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        var testData = new[]
        {
            new Product
            {
                Name = "Expensive Widget",
                Category = "Electronics",
                Price = 99.99m,
                Quantity = 10,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Name = "Cheap Widget",
                Category = "Electronics",
                Price = 9.99m,
                Quantity = 100,
                InStock = true,
                CreatedAt = DateTime.UtcNow,
            },
        };

        await products.InsertMany(testData, TestContext.Current.CancellationToken);

        // Act - Use both Query and Aggregate on the same ManagedCollection
        var queryResults = await products
            .Query()
            .Where(p => p.Price > 50)
            .Execute(TestContext.Current.CancellationToken);

        var aggregateResults = await products
            .Aggregate.Where(p => p.Price > 50)
            .WithMetrics<ProductStats>()
            .Execute(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(queryResults); // Only one product > 50
        Assert.Equal("Expensive Widget", queryResults.First().Object.Name);

        Assert.Equal(1, aggregateResults.TotalCount); // Same filter should match
        Assert.NotNull(aggregateResults.Properties.Price.Mean);
        Assert.InRange(aggregateResults.Properties.Price.Mean.Value, 99.0, 100.0);
    }
}
