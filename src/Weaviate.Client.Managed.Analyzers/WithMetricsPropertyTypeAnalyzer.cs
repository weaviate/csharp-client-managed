using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Weaviate.Client.Managed.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class WithMetricsPropertyTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "WEAVIATE010";
    private const string Category = "Usage";

    private static readonly LocalizableString Title = "Property type does not match metric type";
    private static readonly LocalizableString MessageFormat =
        "Property '{0}' of type '{1}' cannot be used with {2}. Expected types: {3}.";
    private static readonly LocalizableString Description =
        "The property type must match the metric type in WithMetrics calls.";

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
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check if this is a .WithMetrics(...) call
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        if (memberAccess.Name.Identifier.Text != "WithMetrics")
            return;

        // Get arguments passed to WithMetrics
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count == 0)
            return;

        // Each argument should be a lambda: m => m.Property(p => p.Prop, Metric.Type.Value, ...)
        foreach (var argument in arguments)
        {
            if (argument.Expression is not SimpleLambdaExpressionSyntax lambda)
                continue;

            // lambda.Body should be an invocation of m.Property(...)
            if (lambda.Body is not InvocationExpressionSyntax propertyInvocation)
                continue;

            if (
                propertyInvocation.Expression
                is not MemberAccessExpressionSyntax propertyMemberAccess
            )
                continue;

            if (propertyMemberAccess.Name.Identifier.Text != "Property")
                continue;

            // Get the arguments to m.Property(...)
            var propertyArgs = propertyInvocation.ArgumentList.Arguments;
            if (propertyArgs.Count < 2)
                continue;

            // First argument: p => p.Prop (property selector)
            // Second+ arguments: Metric enums
            var propertySelector = propertyArgs[0].Expression;
            var metricArgs = propertyArgs.Skip(1).ToArray();

            // Analyze the property selector lambda to get the property type
            if (propertySelector is not SimpleLambdaExpressionSyntax propertySelectorLambda)
                continue;

            var propertySymbol = GetPropertySymbolFromLambda(
                context.SemanticModel,
                propertySelectorLambda
            );
            if (propertySymbol == null)
                continue;

            var propertyType = propertySymbol.Type;
            var propertyName = propertySymbol.Name;

            // Analyze each metric argument
            foreach (var metricArg in metricArgs)
            {
                if (metricArg.Expression is not MemberAccessExpressionSyntax metricMemberAccess)
                    continue;

                // Get the metric type info (e.g., Metric.Number.Mean)
                var symbolInfo = context.SemanticModel.GetSymbolInfo(metricMemberAccess);
                if (symbolInfo.Symbol is not IFieldSymbol metricField)
                    continue;

                var metricEnumType = metricField.ContainingType;
                if (metricEnumType == null)
                    continue;

                // Check if this is a Metric.* enum
                if (
                    metricEnumType.ContainingType?.Name != "Metric"
                    || metricEnumType.ContainingNamespace?.ToDisplayString()
                        != "Weaviate.Client.Managed.Attributes"
                )
                    continue;

                var metricTypeName = metricEnumType.Name; // Number, Integer, Text, Boolean, Date

                // Validate property type matches metric type
                var expectedTypes = GetExpectedTypesForMetric(metricTypeName);
                if (expectedTypes == null || expectedTypes.Length == 0)
                    continue;

                var actualTypeName = propertyType.ToDisplayString();
                if (!expectedTypes.Contains(actualTypeName))
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        metricArg.GetLocation(),
                        propertyName,
                        actualTypeName,
                        $"Metric.{metricTypeName}.*",
                        string.Join(", ", expectedTypes)
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static IPropertySymbol? GetPropertySymbolFromLambda(
        SemanticModel semanticModel,
        SimpleLambdaExpressionSyntax lambda
    )
    {
        // Lambda body should be: p.Property or similar member access
        if (lambda.Body is not MemberAccessExpressionSyntax memberAccess)
            return null;

        var symbolInfo = semanticModel.GetSymbolInfo(memberAccess);
        return symbolInfo.Symbol as IPropertySymbol;
    }

    private static string[]? GetExpectedTypesForMetric(string metricType)
    {
        return metricType switch
        {
            "Number" => new[]
            {
                "double",
                "float",
                "decimal",
                "double?",
                "float?",
                "decimal?",
                "System.Double",
                "System.Single",
                "System.Decimal",
            },
            "Integer" => new[]
            {
                "int",
                "long",
                "short",
                "byte",
                "int?",
                "long?",
                "short?",
                "byte?",
                "System.Int32",
                "System.Int64",
                "System.Int16",
                "System.Byte",
            },
            "Text" => new[] { "string", "string?", "System.String" },
            "Boolean" => new[] { "bool", "bool?", "System.Boolean" },
            "Date" => new[]
            {
                "System.DateTime",
                "System.DateTime?",
                "System.DateTimeOffset",
                "System.DateTimeOffset?",
            },
            _ => null,
        };
    }
}
