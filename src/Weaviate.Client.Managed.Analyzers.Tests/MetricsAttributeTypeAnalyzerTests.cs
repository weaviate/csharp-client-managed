using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;
using Xunit;

namespace Weaviate.Client.Managed.Analyzers.Tests;

public class MetricsAttributeTypeAnalyzerTests
{
    #region Single Metric Extraction Tests

    [Fact]
    public async Task ValidSingleMetricNumber_Mean_NoDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;

            public class ProductStats
            {
                [Metrics("price", Metric.Number.Mean)]
                public double? AveragePrice { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidSingleMetricNumber_Count_NoDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;

            public class ProductStats
            {
                [Metrics("price", Metric.Number.Count)]
                public long? PriceCount { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidSingleMetricInteger_Sum_NoDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;

            public class ProductStats
            {
                [Metrics("stock", Metric.Integer.Sum)]
                public long? TotalStock { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidSingleMetricInteger_Mean_NoDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;

            public class ProductStats
            {
                [Metrics("stock", Metric.Integer.Mean)]
                public double? AverageStock { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SingleMetricNumberMean_WrongType_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;

            public class ProductStats
            {
                {|#0:[Metrics("price", Metric.Number.Mean)]
                public long? AveragePrice { get; set; }|}
            }
            """;

        var expected = DiagnosticResult
            .CompilerError(MetricsAttributeTypeAnalyzer.InvalidScalarTypeDiagnosticId)
            .WithLocation(0)
            .WithArguments("long?", "Metric.Number.Mean", "double? or float? or decimal?");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task SingleMetricNumberCount_WrongType_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;

            public class ProductStats
            {
                {|#0:[Metrics("price", Metric.Number.Count)]
                public double? PriceCount { get; set; }|}
            }
            """;

        var expected = DiagnosticResult
            .CompilerError(MetricsAttributeTypeAnalyzer.InvalidScalarTypeDiagnosticId)
            .WithLocation(0)
            .WithArguments("double?", "Metric.Number.Count", "long? or int?");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task SingleMetricIntegerSum_WrongType_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;

            public class ProductStats
            {
                {|#0:[Metrics("stock", Metric.Integer.Sum)]
                public double? StockSum { get; set; }|}
            }
            """;

        var expected = DiagnosticResult
            .CompilerError(MetricsAttributeTypeAnalyzer.InvalidScalarTypeDiagnosticId)
            .WithLocation(0)
            .WithArguments("double?", "Metric.Integer.Sum", "long? or int?");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task SingleMetricIntegerMean_WrongType_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;

            public class ProductStats
            {
                {|#0:[Metrics("stock", Metric.Integer.Mean)]
                public long? AverageStock { get; set; }|}
            }
            """;

        var expected = DiagnosticResult
            .CompilerError(MetricsAttributeTypeAnalyzer.InvalidScalarTypeDiagnosticId)
            .WithLocation(0)
            .WithArguments("long?", "Metric.Integer.Mean", "double? or float?");

        await VerifyAnalyzerAsync(test, expected);
    }

    #endregion

    private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<MetricsAttributeTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
        };

        // Add references to the Weaviate assemblies so the test code can compile
        test.TestState.AdditionalReferences.Add(typeof(MetricsAttribute).Assembly);
        test.TestState.AdditionalReferences.Add(typeof(VectorConfig).Assembly);

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}
