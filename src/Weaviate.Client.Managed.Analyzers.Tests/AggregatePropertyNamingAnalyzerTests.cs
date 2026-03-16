using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.XUnit.AnalyzerVerifier<Weaviate.Client.Managed.Analyzers.AggregatePropertyNamingAnalyzer>;

namespace Weaviate.Client.Managed.Analyzers.Tests;

public class AggregatePropertyNamingAnalyzerTests
{
    private const string TestStubCode =
        @"
using System;

namespace Weaviate.Client.Managed.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class QueryAggregateAttribute<TEntity> : Attribute where TEntity : class, new() { }

    [AttributeUsage(AttributeTargets.Property)]
    public class MetricsAttribute : Attribute
    {
        public MetricsAttribute(params object[] metrics) { }
        public MetricsAttribute(string propertyName, object metric) { }
    }
}

namespace Weaviate.Client.Managed.Attributes
{
    public static class Metric
    {
        public enum Number
        {
            None = 0,
            Mean = 1,
            Sum = 2,
            Count = 4,
            Min = 8,
            Max = 16
        }

        public enum Integer
        {
            None = 0,
            Mean = 1,
            Sum = 2,
            Count = 4,
            Min = 8,
            Max = 16
        }

        public enum Text
        {
            None = 0,
            Count = 1,
            TopOccurrences = 2
        }
    }
}

namespace Weaviate.Client.Models.Typed
{
    public class Aggregate
    {
        public class Number { }
        public class Integer { }
        public class Text { }
    }
}

namespace TestData
{
    public class Product
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; }
    }
}
";

    #region WEAVIATE006 - Missing [QueryAggregate<T>] Tests

    [Fact]
    public async Task MetricsWithoutQueryAggregate_ReportsDiagnostic()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;

    // Missing [QueryAggregate<Product>]
    public class ProductStats
    {
        [Metrics(""price"", Metric.Number.Mean)]
        public double? {|#0:PriceMean|} { get; set; }
    }
}";

        var expected = VerifyCS
            .Diagnostic(AggregatePropertyNamingAnalyzer.MissingContextDiagnosticId)
            .WithLocation(0)
            .WithArguments("PriceMean", "ProductStats");

        await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task MetricsWithQueryAggregate_NoDiagnostic()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;
    using TestData;

    [QueryAggregate<Product>]
    public class ProductStats
    {
        [Metrics(""price"", Metric.Number.Mean)]
        public double? PriceMean { get; set; }
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    #endregion

    #region WEAVIATE007 - Invalid Property Name Tests

    [Fact]
    public async Task InvalidPropertyName_ReportsDiagnostic()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;
    using TestData;

    [QueryAggregate<Product>]
    public class ProductStats
    {
        [Metrics(""invalidProperty"", Metric.Number.Mean)]
        public double? {|#0:InvalidPropertyMean|} { get; set; }
    }
}";

        var expected = VerifyCS
            .Diagnostic(AggregatePropertyNamingAnalyzer.InvalidPropertyNameDiagnosticId)
            .WithLocation(0)
            .WithArguments("InvalidPropertyMean", "invalidProperty", "Product");

        await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task ValidPropertyName_NoDiagnostic()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;
    using TestData;

    [QueryAggregate<Product>]
    public class ProductStats
    {
        [Metrics(""price"", Metric.Number.Mean)]
        public double? PriceMean { get; set; }

        [Metrics(""stock"", Metric.Integer.Sum)]
        public long? StockSum { get; set; }

        [Metrics(""category"", Metric.Text.Count)]
        public long? CategoryCount { get; set; }
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task PropertyNameCaseInsensitive_NoDiagnostic()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;
    using TestData;

    [QueryAggregate<Product>]
    public class ProductStats
    {
        // ""Price"" should match property ""Price"" (case-insensitive)
        [Metrics(""Price"", Metric.Number.Mean)]
        public double? PriceMean { get; set; }

        // ""STOCK"" should match property ""Stock"" (case-insensitive)
        [Metrics(""STOCK"", Metric.Integer.Sum)]
        public long? StockSum { get; set; }
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    #endregion

    #region WEAVIATE008 - Missing Suffix Convention Tests

    [Fact]
    public async Task WrongPropertyNameSuffix_ReportsDiagnostic()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;
    using TestData;

    [QueryAggregate<Product>]
    public class ProductStats
    {
        [Metrics(""price"", Metric.Number.Mean)]
        public double? {|#0:AveragePrice|} { get; set; }
    }
}";

        var expected = VerifyCS
            .Diagnostic(AggregatePropertyNamingAnalyzer.MissingSuffixDiagnosticId)
            .WithLocation(0)
            .WithArguments("AveragePrice", "price", "Metric", "Number.Mean", "PriceMean");

        await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
    }

    [Fact]
    public async Task CorrectPropertyNameSuffix_NoDiagnostic()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;
    using TestData;

    [QueryAggregate<Product>]
    public class ProductStats
    {
        [Metrics(""price"", Metric.Number.Mean)]
        public double? PriceMean { get; set; }

        [Metrics(""price"", Metric.Number.Sum)]
        public double? PriceSum { get; set; }

        [Metrics(""price"", Metric.Number.Count)]
        public long? PriceCount { get; set; }

        [Metrics(""stock"", Metric.Integer.Max)]
        public int? StockMax { get; set; }

        [Metrics(""category"", Metric.Text.Count)]
        public long? CategoryCount { get; set; }
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    [Fact]
    public async Task MissingSuffixWithWrongCase_ReportsDiagnostic()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;
    using TestData;

    [QueryAggregate<Product>]
    public class ProductStats
    {
        [Metrics(""price"", Metric.Number.Mean)]
        public double? {|#0:Pricemean|} { get; set; }
    }
}";

        var expected = VerifyCS
            .Diagnostic(AggregatePropertyNamingAnalyzer.MissingSuffixDiagnosticId)
            .WithLocation(0)
            .WithArguments("Pricemean", "price", "Metric", "Number.Mean", "PriceMean");

        await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
    }

    #endregion

    #region Full Aggregate Type Tests (Should Not Trigger)

    [Fact]
    public async Task FullAggregateType_NoDiagnostic()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;
    using Weaviate.Client.Models.Typed;
    using TestData;

    [QueryAggregate<Product>]
    public class ProductStats
    {
        // Full aggregate types should not trigger naming validation
        [Metrics(Metric.Number.Mean, Metric.Number.Sum)]
        public Aggregate.Number Price { get; set; }

        [Metrics(Metric.Integer.Mean, Metric.Integer.Count)]
        public Aggregate.Integer Stock { get; set; }
    }
}";

        await VerifyCS.VerifyAnalyzerAsync(testCode);
    }

    #endregion

    #region Multiple Diagnostics Tests

    [Fact]
    public async Task MultipleErrors_ReportsAll()
    {
        var testCode =
            TestStubCode
            + @"
namespace Test
{
    using Weaviate.Client.Managed.Attributes;
    using TestData;

    [QueryAggregate<Product>]
    public class ProductStats
    {
        // Wrong suffix
        [Metrics(""price"", Metric.Number.Mean)]
        public double? {|#0:AvgPrice|} { get; set; }

        // Invalid property name
        [Metrics(""invalidProp"", Metric.Number.Sum)]
        public double? {|#1:InvalidPropSum|} { get; set; }
    }

    // Missing QueryAggregate
    public class OtherStats
    {
        [Metrics(""price"", Metric.Number.Mean)]
        public double? {|#2:PriceMean|} { get; set; }
    }
}";

        var expected = new[]
        {
            VerifyCS
                .Diagnostic(AggregatePropertyNamingAnalyzer.MissingSuffixDiagnosticId)
                .WithLocation(0)
                .WithArguments("AvgPrice", "price", "Metric", "Number.Mean", "PriceMean"),
            VerifyCS
                .Diagnostic(AggregatePropertyNamingAnalyzer.InvalidPropertyNameDiagnosticId)
                .WithLocation(1)
                .WithArguments("InvalidPropSum", "invalidProp", "Product"),
            VerifyCS
                .Diagnostic(AggregatePropertyNamingAnalyzer.MissingContextDiagnosticId)
                .WithLocation(2)
                .WithArguments("PriceMean", "OtherStats"),
        };

        await VerifyCS.VerifyAnalyzerAsync(testCode, expected);
    }

    #endregion
}
