using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;
using Xunit;

namespace Weaviate.Client.Managed.Tests.Aggregates;

public class MetricsExtractorTests
{
    [Fact]
    public void FromType_ConventionBased_ExtractsNumberMetrics()
    {
        // Arrange - Result type using naming convention WITHOUT attributes
        var metrics = MetricsExtractor.FromType<ConventionBasedStats>().ToList();

        // Assert - Should extract metrics based on property names
        // PriceMean, PriceMin, PriceMax, StockCount = 4 metrics
        Assert.Equal(4, metrics.Count);
    }

    [Fact]
    public void FromType_AttributeBased_ExtractsFromMetricsAttribute()
    {
        // Arrange - Result type using [Metrics] attributes
        var metrics = MetricsExtractor.FromType<AttributeBasedStats>().ToList();

        // Assert - Should extract metrics from attributes
        // AveragePrice, TotalStock = 2 metrics
        Assert.Equal(2, metrics.Count);
    }

    [Fact]
    public void FromType_MixedApproach_SupportsBoth()
    {
        // Arrange - Result type mixing both approaches
        var metrics = MetricsExtractor.FromType<MixedStats>().ToList();

        // Assert - Should support both attribute and convention-based
        // PriceMean (convention) + NumberOfPrices (attribute) = 2 metrics
        Assert.Equal(2, metrics.Count);
    }

    [Fact]
    public void FromType_NoMatchingConvention_ReturnsEmpty()
    {
        // Arrange - Properties that don't match any convention
        var metrics = MetricsExtractor.FromType<NoConventionStats>().ToList();

        // Assert - Should return empty for non-matching names
        Assert.Empty(metrics);
    }

    // Test data classes demonstrating convention-based mapping (PRIMARY approach)
    public class ConventionBasedStats
    {
        // No attributes needed - naming convention maps these automatically
        public double? PriceMean { get; set; } // → price.mean
        public long? StockCount { get; set; } // → stock.count
        public double? PriceMin { get; set; } // → price.min
        public double? PriceMax { get; set; } // → price.max
    }

    // Test data classes demonstrating attribute-based mapping (SECONDARY approach)
    public class AttributeBasedStats
    {
        [Metrics("price", Metric.Number.Mean)]
        public double? AveragePrice { get; set; }

        [Metrics("stock", Metric.Integer.Sum)]
        public long? TotalStock { get; set; }
    }

    // Test data demonstrating mixed approach
    public class MixedStats
    {
        // Convention-based (primary)
        public double? PriceMean { get; set; }

        // Attribute-based (secondary, for custom names)
        [Metrics("price", Metric.Number.Count)]
        public long? NumberOfPrices { get; set; }
    }

    // Test data with no matching conventions
    public class NoConventionStats
    {
        public string? SomeText { get; set; }
        public int RandomNumber { get; set; }
    }
}
