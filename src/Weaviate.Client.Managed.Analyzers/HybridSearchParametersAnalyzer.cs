using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Weaviate.Client.Managed.Analyzers;

/// <summary>
/// Analyzer that validates hybrid search method calls require at least one search parameter.
/// Similar to Weaviate.Client's HybridSearchNullParametersAnalyzer but for Managed client API.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HybridSearchParametersAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "WEAVIATE009";
    private const string Category = "Usage";

    private static readonly LocalizableString Title =
        "Hybrid search requires at least one search parameter";
    private static readonly LocalizableString MessageFormat =
        "Hybrid search must provide at least one of: non-null 'query' parameter or 'vector' parameter";
    private static readonly LocalizableString Description =
        "The Hybrid() method requires at least one search parameter to be provided. Either 'query' must be a non-null string or 'vector' must be specified.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        // Check if this is a call to Hybrid() method
        if (method.Name != "Hybrid")
            return;

        // Check if the containing type is CollectionMapperQueryClient<T> or ProjectedQueryClient<T, TProjection>
        var containingType = method.ContainingType;
        if (containingType == null)
            return;

        var containingTypeName = containingType.OriginalDefinition.ToDisplayString();
        if (
            containingTypeName != "Weaviate.Client.Managed.Query.CollectionMapperQueryClient<T>"
            && containingTypeName
                != "Weaviate.Client.Managed.Query.ProjectedQueryClient<T, TProjection>"
        )
            return;

        // Get the arguments for query and vector parameters
        var queryArg = invocation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == "query");
        var vectorArg = invocation.Arguments.FirstOrDefault(arg => arg.Parameter?.Name == "vector");

        if (queryArg == null)
            return; // Shouldn't happen, but guard against it

        // Check if query is explicitly null
        bool queryIsNull = IsArgumentNull(queryArg);

        // Check if vector is explicitly null or omitted (default null)
        bool vectorIsNullOrOmitted =
            vectorArg == null
            || vectorArg.ArgumentKind == ArgumentKind.DefaultValue
            || IsArgumentNull(vectorArg);

        // If both are null/omitted, report diagnostic
        if (queryIsNull && vectorIsNullOrOmitted)
        {
            var diagnostic = Diagnostic.Create(Rule, invocation.Syntax.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsArgumentNull(IArgumentOperation argument)
    {
        var value = argument.Value;

        // Check for explicit null literal
        if (value.ConstantValue.HasValue && value.ConstantValue.Value == null)
            return true;

        // Check for null literal syntax node
        if (
            value.Syntax is LiteralExpressionSyntax literal
            && literal.Kind() == SyntaxKind.NullLiteralExpression
        )
            return true;

        // Check for default(T) where T is nullable
        if (value.Kind == OperationKind.DefaultValue)
        {
            var defaultValueOp = (IDefaultValueOperation)value;
            if (
                defaultValueOp.Type?.IsReferenceType == true
                || defaultValueOp.Type?.OriginalDefinition.SpecialType
                    == SpecialType.System_Nullable_T
            )
                return true;
        }

        // Check for conversion from null
        if (value.Kind == OperationKind.Conversion)
        {
            var conversion = (IConversionOperation)value;
            if (
                conversion.Operand.ConstantValue.HasValue
                && conversion.Operand.ConstantValue.Value == null
            )
                return true;
        }

        return false;
    }
}
