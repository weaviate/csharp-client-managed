using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;
using Xunit;

namespace Weaviate.Client.Managed.Analyzers.Tests;

public class VectorConfigMethodSignatureAnalyzerTests
{
    [Fact]
    public async Task ConfigMethod_WithCorrectSignature_NoDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;
            using Weaviate.Client.Models;

            public class Article
            {
                [Vector<Vectorizer.Text2VecOpenAI>(ConfigMethod = nameof(ConfigureVector))]
                public float[]? Embedding { get; set; }

                public static Vectorizer.Text2VecOpenAI ConfigureVector(
                    string vectorName,
                    Vectorizer.Text2VecOpenAI prebuilt)
                {
                    prebuilt.VectorizeCollectionName = false;
                    return prebuilt;
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ConfigMethod_WithWrongReturnType_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;
            using Weaviate.Client.Models;

            public class Article
            {
                [Vector<Vectorizer.Text2VecOpenAI>(ConfigMethod = nameof({|#0:ConfigureVector|}))]
                public float[]? Embedding { get; set; }

                public static string ConfigureVector(
                    string vectorName,
                    Vectorizer.Text2VecOpenAI prebuilt)
                {
                    return "wrong";
                }
            }
            """;

        var expected = new DiagnosticResult(
            "WCM001",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "ConfigMethod 'ConfigureVector' must return Weaviate.Client.Models.Vectorizer.Text2VecOpenAI (the vectorizer type)"
            );

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ConfigMethod_WithMissingParameters_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;
            using Weaviate.Client.Models;

            public class Article
            {
                [Vector<Vectorizer.Text2VecOpenAI>(ConfigMethod = nameof({|#0:ConfigureVector|}))]
                public float[]? Embedding { get; set; }

                public static Vectorizer.Text2VecOpenAI ConfigureVector(string vectorName)
                {
                    return null!;
                }
            }
            """;

        var expected = new DiagnosticResult(
            "WCM002",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage(
                "ConfigMethod 'ConfigureVector' must have signature: TVectorizer MethodName(string vectorName, TVectorizer prebuilt)"
            );

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ConfigMethod_NotStatic_ReportsDiagnostic()
    {
        var test = """
            using Weaviate.Client.Managed.Attributes;
            using Weaviate.Client.Models;

            public class Article
            {
                [Vector<Vectorizer.Text2VecOpenAI>(ConfigMethod = nameof({|#0:ConfigureVector|}))]
                public float[]? Embedding { get; set; }

                public Vectorizer.Text2VecOpenAI ConfigureVector(
                    string vectorName,
                    Vectorizer.Text2VecOpenAI prebuilt)
                {
                    return prebuilt;
                }
            }
            """;

        var expected = new DiagnosticResult(
            "WCM003",
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error
        )
            .WithLocation(0)
            .WithMessage("ConfigMethod 'ConfigureVector' must be static");

        await VerifyAnalyzerAsync(test, expected);
    }

    private static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<VectorConfigMethodSignatureAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
        };

        // Add references to the Weaviate assemblies so the test code can compile
        test.TestState.AdditionalReferences.Add(typeof(VectorAttribute<>).Assembly);
        test.TestState.AdditionalReferences.Add(typeof(VectorConfig).Assembly);

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }
}
