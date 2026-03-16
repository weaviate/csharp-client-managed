using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Weaviate.Client.Managed.Analyzers.Tests;

public class SortWithVectorSearchAnalyzerTests
{
    // Stub preamble: usings + query client stub definition.
    // The test-specific class is appended after this.
    private const string Preamble = """
        using System;
        using System.Linq.Expressions;
        using System.Threading;
        using System.Threading.Tasks;

        public class CollectionMapperQueryClient<T>
        {
            public CollectionMapperQueryClient<T> NearText(string text) => this;
            public CollectionMapperQueryClient<T> NearVector(float[] v) => this;
            public CollectionMapperQueryClient<T> Hybrid(string query) => this;
            public CollectionMapperQueryClient<T> BM25(string query) => this;
            public CollectionMapperQueryClient<T> NearObject(Guid id) => this;
            public CollectionMapperQueryClient<T> NearMedia(object media) => this;
            public CollectionMapperQueryClient<T> Sort<TProp>(Expression<Func<T, TProp>> prop, bool descending = false) => this;
            public CollectionMapperQueryClient<T> OrderBy<TProp>(Expression<Func<T, TProp>> prop) => this;
            public CollectionMapperQueryClient<T> OrderByDescending<TProp>(Expression<Func<T, TProp>> prop) => this;
            public CollectionMapperQueryClient<T> ThenBy<TProp>(Expression<Func<T, TProp>> prop) => this;
            public CollectionMapperQueryClient<T> ThenByDescending<TProp>(Expression<Func<T, TProp>> prop) => this;
            public Task Execute(CancellationToken ct = default) => Task.CompletedTask;
        }

        """;

    [Fact]
    public async Task Sort_AfterNearText_ReportsDiagnostic()
    {
        var source =
            Preamble
            + """
                public class Item { public int Count { get; set; } }

                public class TestUsage
                {
                    public void UseIt(CollectionMapperQueryClient<Item> q)
                    {
                        q.NearText("foo").{|#0:Sort|}(t => t.Count);
                    }
                }
                """;

        var expected = new DiagnosticResult(
            SortWithVectorSearchAnalyzer.DiagnosticId,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "'Sort' cannot be used with 'NearText' — Weaviate only supports sorting in non-vector (Fetch) queries"
            );

        await VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task OrderBy_AfterHybrid_ReportsDiagnostic()
    {
        var source =
            Preamble
            + """
                public class Item { public string Name { get; set; } = ""; }

                public class TestUsage
                {
                    public void UseIt(CollectionMapperQueryClient<Item> q)
                    {
                        q.Hybrid("query").{|#0:OrderBy|}(t => t.Name);
                    }
                }
                """;

        var expected = new DiagnosticResult(
            SortWithVectorSearchAnalyzer.DiagnosticId,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "'OrderBy' cannot be used with 'Hybrid' — Weaviate only supports sorting in non-vector (Fetch) queries"
            );

        await VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ThenBy_AfterBM25_ReportsDiagnostic()
    {
        var source =
            Preamble
            + """
                public class Item { public string Name { get; set; } = ""; }

                public class TestUsage
                {
                    public void UseIt(CollectionMapperQueryClient<Item> q)
                    {
                        q.BM25("query").{|#0:ThenBy|}(t => t.Name);
                    }
                }
                """;

        var expected = new DiagnosticResult(
            SortWithVectorSearchAnalyzer.DiagnosticId,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "'ThenBy' cannot be used with 'BM25' — Weaviate only supports sorting in non-vector (Fetch) queries"
            );

        await VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ThenByDescending_AfterNearVector_ReportsDiagnostic()
    {
        var source =
            Preamble
            + """
                public class Item { public int Count { get; set; } }

                public class TestUsage
                {
                    public void UseIt(CollectionMapperQueryClient<Item> q)
                    {
                        q.NearVector(new float[] { 1f }).{|#0:ThenByDescending|}(t => t.Count);
                    }
                }
                """;

        var expected = new DiagnosticResult(
            SortWithVectorSearchAnalyzer.DiagnosticId,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "'ThenByDescending' cannot be used with 'NearVector' — Weaviate only supports sorting in non-vector (Fetch) queries"
            );

        await VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task Sort_WithoutVectorSearch_NoDiagnostic()
    {
        var source =
            Preamble
            + """
                public class Item { public int Count { get; set; } }

                public class TestUsage
                {
                    public void UseIt(CollectionMapperQueryClient<Item> q)
                    {
                        q.Sort(t => t.Count, descending: true);
                    }
                }
                """;

        await VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task OrderByThenBy_WithoutVectorSearch_NoDiagnostic()
    {
        var source =
            Preamble
            + """
                public class Item { public string Name { get; set; } = ""; public int Count { get; set; } }

                public class TestUsage
                {
                    public void UseIt(CollectionMapperQueryClient<Item> q)
                    {
                        q.OrderBy(t => t.Name).ThenByDescending(t => t.Count);
                    }
                }
                """;

        await VerifyAnalyzerAsync(source);
    }

    private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<SortWithVectorSearchAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}
