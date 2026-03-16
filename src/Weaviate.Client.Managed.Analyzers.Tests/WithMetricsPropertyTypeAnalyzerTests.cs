using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Weaviate.Client.Managed.Aggregates;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;
using Xunit;

namespace Weaviate.Client.Managed.Analyzers.Tests;

public class WithMetricsPropertyTypeAnalyzerTests
{
    [Fact]
    public async Task ValidNumberMetric_WithDecimalProperty_NoDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Aggregates;
            using Weaviate.Client.Managed.Attributes;

            public class Product
            {
                public decimal Price { get; set; }
                public int Quantity { get; set; }
            }

            public class Test
            {
                public void Method(AggregateStarter<Product> aggregate)
                {
                    aggregate.WithMetrics(
                        m => m.Property(p => p.Price, Metric.Number.Mean)
                    );
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidIntegerMetric_WithIntProperty_NoDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Aggregates;
            using Weaviate.Client.Managed.Attributes;

            public class Product
            {
                public int Quantity { get; set; }
            }

            public class Test
            {
                public void Method(AggregateStarter<Product> aggregate)
                {
                    aggregate.WithMetrics(
                        m => m.Property(p => p.Quantity, Metric.Integer.Sum)
                    );
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidMultipleMetrics_NoDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Aggregates;
            using Weaviate.Client.Managed.Attributes;

            public class Product
            {
                public decimal Price { get; set; }
                public int Quantity { get; set; }
                public bool InStock { get; set; }
            }

            public class Test
            {
                public void Method(AggregateStarter<Product> aggregate)
                {
                    aggregate.WithMetrics(
                        m => m.Property(p => p.Price, Metric.Number.Mean, Metric.Number.Min),
                        m => m.Property(p => p.Quantity, Metric.Integer.Sum),
                        m => m.Property(p => p.InStock, Metric.Boolean.TotalTrue)
                    );
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task InvalidMetricType_NumberWithIntProperty_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Aggregates;
            using Weaviate.Client.Managed.Attributes;

            public class Product
            {
                public int Price { get; set; }
            }

            public class Test
            {
                public void Method(AggregateStarter<Product> aggregate)
                {
                    aggregate.WithMetrics(
                        m => m.Property(p => p.Price, {|#0:Metric.Number.Mean|})
                    );
                }
            }
            """;

        var expected = DiagnosticResult
            .CompilerError(WithMetricsPropertyTypeAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments(
                "Price",
                "int",
                "Metric.Number.*",
                "double, float, decimal, double?, float?, decimal?, System.Double, System.Single, System.Decimal"
            );

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InvalidMetricType_IntegerWithDecimalProperty_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Aggregates;
            using Weaviate.Client.Managed.Attributes;

            public class Product
            {
                public decimal Quantity { get; set; }
            }

            public class Test
            {
                public void Method(AggregateStarter<Product> aggregate)
                {
                    aggregate.WithMetrics(
                        m => m.Property(p => p.Quantity, {|#0:Metric.Integer.Sum|})
                    );
                }
            }
            """;

        var expected = DiagnosticResult
            .CompilerError(WithMetricsPropertyTypeAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments(
                "Quantity",
                "decimal",
                "Metric.Integer.*",
                "int, long, short, byte, int?, long?, short?, byte?, System.Int32, System.Int64, System.Int16, System.Byte"
            );

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InvalidMetricType_TextWithIntProperty_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Aggregates;
            using Weaviate.Client.Managed.Attributes;

            public class Product
            {
                public int Name { get; set; }
            }

            public class Test
            {
                public void Method(AggregateStarter<Product> aggregate)
                {
                    aggregate.WithMetrics(
                        m => m.Property(p => p.Name, {|#0:Metric.Text.Count|})
                    );
                }
            }
            """;

        var expected = DiagnosticResult
            .CompilerError(WithMetricsPropertyTypeAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Name", "int", "Metric.Text.*", "string, string?, System.String");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InvalidMetricType_BooleanWithStringProperty_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Aggregates;
            using Weaviate.Client.Managed.Attributes;

            public class Product
            {
                public string InStock { get; set; }
            }

            public class Test
            {
                public void Method(AggregateStarter<Product> aggregate)
                {
                    aggregate.WithMetrics(
                        m => m.Property(p => p.InStock, {|#0:Metric.Boolean.TotalTrue|})
                    );
                }
            }
            """;

        var expected = DiagnosticResult
            .CompilerError(WithMetricsPropertyTypeAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("InStock", "string", "Metric.Boolean.*", "bool, bool?, System.Boolean");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task MultipleMetrics_OneInvalid_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Aggregates;
            using Weaviate.Client.Managed.Attributes;

            public class Product
            {
                public decimal Price { get; set; }
                public int Quantity { get; set; }
            }

            public class Test
            {
                public void Method(AggregateStarter<Product> aggregate)
                {
                    aggregate.WithMetrics(
                        m => m.Property(p => p.Price, Metric.Number.Mean),
                        m => m.Property(p => p.Quantity, {|#0:Metric.Number.Sum|})
                    );
                }
            }
            """;

        var expected = DiagnosticResult
            .CompilerError(WithMetricsPropertyTypeAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments(
                "Quantity",
                "int",
                "Metric.Number.*",
                "double, float, decimal, double?, float?, decimal?, System.Double, System.Single, System.Decimal"
            );

        await VerifyAnalyzerAsync(test, expected);
    }

    private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<WithMetricsPropertyTypeAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
        };

        // Add references to necessary assemblies
        test.TestState.AdditionalReferences.Add(typeof(Metric).Assembly); // Weaviate.Client.Managed
        test.TestState.AdditionalReferences.Add(typeof(VectorConfig).Assembly); // Weaviate.Client

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync();
    }
}
