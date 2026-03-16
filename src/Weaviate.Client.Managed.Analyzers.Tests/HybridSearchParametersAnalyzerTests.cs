using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Weaviate.Client.Managed.Analyzers.Tests;

public class HybridSearchParametersAnalyzerTests
{
    [Fact]
    public async Task Hybrid_WithQueryOnly_NoDiagnostic()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            using Weaviate.Client.Managed.Query;

            public class Product
            {
                public string Name { get; set; } = "";
                public float[]? Embedding { get; set; }
            }

            public class Test
            {
                public void Method(CollectionMapperQueryClient<Product> query)
                {
                    query.Hybrid("search term");
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Hybrid_WithVectorOnly_NoDiagnostic()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            using Weaviate.Client.Managed.Query;

            public class Product
            {
                public string Name { get; set; } = "";
                public float[]? Embedding { get; set; }
            }

            public class Test
            {
                public void Method(CollectionMapperQueryClient<Product> query)
                {
                    query.Hybrid(null, vector: p => p.Embedding);
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Hybrid_WithBothQueryAndVector_NoDiagnostic()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            using Weaviate.Client.Managed.Query;

            public class Product
            {
                public string Name { get; set; } = "";
                public float[]? Embedding { get; set; }
            }

            public class Test
            {
                public void Method(CollectionMapperQueryClient<Product> query)
                {
                    query.Hybrid("search term", vector: p => p.Embedding);
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Hybrid_WithNullQueryAndNoVector_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            using Weaviate.Client.Managed.Query;

            public class Product
            {
                public string Name { get; set; } = "";
                public float[]? Embedding { get; set; }
            }

            public class Test
            {
                public void Method(CollectionMapperQueryClient<Product> query)
                {
                    {|#0:query.Hybrid(null)|};
                }
            }
            """;

        var expected = new DiagnosticResult(
            HybridSearchParametersAnalyzer.DiagnosticId,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "Hybrid search must provide at least one of: non-null 'query' parameter or 'vector' parameter"
            );

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Hybrid_WithNullQueryAndNullVector_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            using Weaviate.Client.Managed.Query;

            public class Product
            {
                public string Name { get; set; } = "";
                public float[]? Embedding { get; set; }
            }

            public class Test
            {
                public void Method(CollectionMapperQueryClient<Product> query)
                {
                    {|#0:query.Hybrid(null, vector: null)|};
                }
            }
            """;

        var expected = new DiagnosticResult(
            HybridSearchParametersAnalyzer.DiagnosticId,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "Hybrid search must provide at least one of: non-null 'query' parameter or 'vector' parameter"
            );

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Hybrid_ProjectedQueryClient_WithNullQueryAndNoVector_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            using Weaviate.Client.Managed.Query;

            public class Product
            {
                public string Name { get; set; } = "";
                public float[]? Embedding { get; set; }
            }

            public class ProductProjection
            {
                public string Name { get; set; } = "";
            }

            public class Test
            {
                public void Method(ProjectedQueryClient<Product, ProductProjection> query)
                {
                    {|#0:query.Hybrid(null)|};
                }
            }
            """;

        var expected = new DiagnosticResult(
            HybridSearchParametersAnalyzer.DiagnosticId,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "Hybrid search must provide at least one of: non-null 'query' parameter or 'vector' parameter"
            );

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Hybrid_WithDefaultKeyword_ReportsDiagnostic()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            using Weaviate.Client.Managed.Query;

            public class Product
            {
                public string Name { get; set; } = "";
                public float[]? Embedding { get; set; }
            }

            public class Test
            {
                public void Method(CollectionMapperQueryClient<Product> query)
                {
                    {|#0:query.Hybrid(default!)|};
                }
            }
            """;

        var expected = new DiagnosticResult(
            HybridSearchParametersAnalyzer.DiagnosticId,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "Hybrid search must provide at least one of: non-null 'query' parameter or 'vector' parameter"
            );

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Hybrid_WithVariableQuery_NoDiagnostic()
    {
        var test = """
            using System;
            using System.Linq.Expressions;
            using Weaviate.Client.Managed.Query;

            public class Product
            {
                public string Name { get; set; } = "";
                public float[]? Embedding { get; set; }
            }

            public class Test
            {
                public void Method(CollectionMapperQueryClient<Product> queryClient, string? searchQuery)
                {
                    // We can't determine at compile-time if searchQuery is null,
                    // so no diagnostic should be reported
                    queryClient.Hybrid(searchQuery);
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<HybridSearchParametersAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
        };

        // Add reference to the Managed client assembly
        test.TestState.AdditionalReferences.Add(
            typeof(Weaviate.Client.Managed.Query.CollectionMapperQueryClient<>).Assembly
        );
        test.TestState.AdditionalReferences.Add(typeof(Weaviate.Client.WeaviateClient).Assembly);

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}
