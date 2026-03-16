using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Weaviate.Client.Managed.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SortWithVectorSearchAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "WCM004";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Sort cannot be used with vector/hybrid search",
        "'{0}' cannot be used with '{1}' — Weaviate only supports sorting in non-vector (Fetch) queries",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Sort, OrderBy, ThenBy and their variants are only valid for Fetch queries. "
            + "Weaviate ignores sort criteria for NearText, NearVector, Hybrid, BM25, NearObject, and NearMedia searches."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    private static readonly ImmutableHashSet<string> SortMethods = ImmutableHashSet.Create(
        "Sort",
        "OrderBy",
        "OrderByDescending",
        "ThenBy",
        "ThenByDescending"
    );

    private static readonly ImmutableHashSet<string> VectorSearchMethods = ImmutableHashSet.Create(
        "NearText",
        "NearVector",
        "Hybrid",
        "BM25",
        "NearObject",
        "NearMedia"
    );

    private static readonly ImmutableHashSet<string> QueryClientTypes = ImmutableHashSet.Create(
        "CollectionMapperQueryClient",
        "ProjectedQueryClient"
    );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is a Sort/OrderBy/ThenBy call
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.Text;
        if (!SortMethods.Contains(methodName))
            return;

        // Verify it's on a query client type via semantic model
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            return;

        var containingTypeName = methodSymbol.ContainingType?.Name ?? string.Empty;
        if (!IsQueryClientType(containingTypeName))
            return;

        // Walk the invocation chain to find a vector search method
        var conflictingMethod = FindConflictingVectorSearchMethod(memberAccess.Expression);
        if (conflictingMethod is null)
            return;

        var diagnostic = Diagnostic.Create(
            Rule,
            memberAccess.Name.GetLocation(),
            methodName,
            conflictingMethod
        );
        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsQueryClientType(string typeName)
    {
        foreach (var clientType in QueryClientTypes)
        {
            if (typeName == clientType || typeName.StartsWith(clientType + "`"))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Walks the fluent invocation chain from the receiver of the sort call upward,
    /// returning the name of the first vector search method found, or null if none.
    /// </summary>
    private static string? FindConflictingVectorSearchMethod(ExpressionSyntax receiver)
    {
        var current = receiver;
        while (current is InvocationExpressionSyntax parentInvocation)
        {
            if (parentInvocation.Expression is MemberAccessExpressionSyntax parentMember)
            {
                var name = parentMember.Name.Identifier.Text;
                if (VectorSearchMethods.Contains(name))
                    return name;
                current = parentMember.Expression;
            }
            else
            {
                break;
            }
        }
        return null;
    }
}
