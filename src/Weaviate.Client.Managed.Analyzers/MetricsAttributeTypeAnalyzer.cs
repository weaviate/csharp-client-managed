using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Weaviate.Client.Managed.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MetricsAttributeTypeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "WEAVIATE003";
    private const string Category = "Usage";

    private static readonly LocalizableString Title = "Metrics attribute type mismatch";
    private static readonly LocalizableString MessageFormat =
        "[Metrics(Metric.{0}...)] can only be applied to properties of type Aggregate.{0} or matching scalar types";
    private static readonly LocalizableString Description =
        "The Metrics attribute enum type must match the property's Aggregate type or appropriate scalar type.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description
    );

    public const string SingleMetricDiagnosticId = "WEAVIATE004";
    private static readonly LocalizableString SingleMetricTitle =
        "Single metric required for scalar property";
    private static readonly LocalizableString SingleMetricMessageFormat =
        "When using [Metrics(propertyName, ...)] with scalar types, only a single metric value is allowed.";

    private static readonly DiagnosticDescriptor SingleMetricRule = new(
        SingleMetricDiagnosticId,
        SingleMetricTitle,
        SingleMetricMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Scalar properties can only extract a single metric value from aggregate results."
    );

    public const string InvalidScalarTypeDiagnosticId = "WEAVIATE005";
    private static readonly LocalizableString InvalidScalarTypeTitle =
        "Invalid scalar type for metric";
    private static readonly LocalizableString InvalidScalarTypeMessageFormat =
        "Property type '{0}' is not valid for metric '{1}'. Expected: {2}";

    private static readonly DiagnosticDescriptor InvalidScalarTypeRule = new(
        InvalidScalarTypeDiagnosticId,
        InvalidScalarTypeTitle,
        InvalidScalarTypeMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The property type must match the metric's expected scalar type."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [Rule, SingleMetricRule, InvalidScalarTypeRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var propertyDeclaration = (PropertyDeclarationSyntax)context.Node;
        var propertySymbol = context.SemanticModel.GetDeclaredSymbol(propertyDeclaration);
        if (propertySymbol == null)
            return;

        // Find [Metrics(...)] attribute
        var metricsAttr = propertySymbol
            .GetAttributes()
            .FirstOrDefault(attr =>
                attr.AttributeClass?.Name == "MetricsAttribute"
                && attr.AttributeClass.ContainingNamespace?.ToDisplayString()
                    == "Weaviate.Client.Managed.Attributes"
            );

        if (metricsAttr == null || metricsAttr.ConstructorArguments.Length == 0)
            return;

        // Check if this is single metric usage: [Metrics("propertyName", metric)]
        var isSingleMetricUsage =
            metricsAttr.ConstructorArguments.Length == 2
            && metricsAttr.ConstructorArguments[0].Kind == TypedConstantKind.Primitive
            && metricsAttr.ConstructorArguments[0].Type?.SpecialType == SpecialType.System_String;

        if (isSingleMetricUsage)
        {
            AnalyzeSingleMetricUsage(context, propertyDeclaration, propertySymbol, metricsAttr);
        }
        else
        {
            AnalyzeFullAggregateUsage(context, propertyDeclaration, propertySymbol, metricsAttr);
        }
    }

    private static void AnalyzeFullAggregateUsage(
        SyntaxNodeAnalysisContext context,
        PropertyDeclarationSyntax propertyDeclaration,
        IPropertySymbol propertySymbol,
        AttributeData metricsAttr
    )
    {
        // Get the metrics array argument (params object[] metrics)
        var metricsArg = metricsAttr.ConstructorArguments[0];

        // With params object[], the argument should be an array
        if (metricsArg.Kind != TypedConstantKind.Array || metricsArg.Values.IsEmpty)
            return;

        // Get the first metric to determine the type (all should be same enum type)
        var firstMetric = metricsArg.Values[0];
        if (firstMetric.Type is not INamedTypeSymbol metricsType)
            return;

        // Extract the metrics enum type name (e.g., "Number", "Integer", "Text", "Boolean", "Date")
        var metricsTypeName = metricsType.Name;
        if (
            metricsType.ContainingType?.Name != "Metric"
            || metricsType.ContainingNamespace?.ToDisplayString()
                != "Weaviate.Client.Managed.Attributes"
        )
            return;

        // Get property type
        var propertyType = propertySymbol.Type;
        if (propertyType is not INamedTypeSymbol namedPropertyType)
            return;

        // Check if property type is Aggregate.{MetricsTypeName}
        if (
            namedPropertyType.Name != metricsTypeName
            || namedPropertyType.ContainingType?.Name != "Aggregate"
        )
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                propertyDeclaration.GetLocation(),
                metricsTypeName
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeSingleMetricUsage(
        SyntaxNodeAnalysisContext context,
        PropertyDeclarationSyntax propertyDeclaration,
        IPropertySymbol propertySymbol,
        AttributeData metricsAttr
    )
    {
        // Get the metric enum argument (second parameter)
        var metricArg = metricsAttr.ConstructorArguments[1];
        if (metricArg.Type is not INamedTypeSymbol metricType)
            return;

        var metricsTypeName = metricType.Name; // Number, Integer, Text, Boolean, Date
        if (
            metricType.ContainingType?.Name != "Metric"
            || metricType.ContainingNamespace?.ToDisplayString()
                != "Weaviate.Client.Managed.Attributes"
        )
            return;

        // Get the specific metric name (e.g., Mean, Sum, Count)
        var metricName = GetMetricName(metricArg.Value);
        if (metricName == null)
            return;

        // Validate property type matches metric
        var propertyType = propertySymbol.Type;
        var expectedTypes = GetExpectedTypesForMetric(metricsTypeName, metricName);

        if (expectedTypes == null || expectedTypes.Length == 0)
            return;

        var actualTypeName = propertyType.ToDisplayString();
        if (!expectedTypes.Contains(actualTypeName))
        {
            var diagnostic = Diagnostic.Create(
                InvalidScalarTypeRule,
                propertyDeclaration.GetLocation(),
                actualTypeName,
                $"Metric.{metricsTypeName}.{metricName}",
                string.Join(" or ", expectedTypes)
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static string? GetMetricName(object? value)
    {
        if (value is int intValue)
        {
            // Map enum values to names based on Metrics enum definitions
            return intValue switch
            {
                1 => "Mean", // or Count for Text/Boolean/Date
                2 => "Sum", // or TopOccurrences for Text
                4 => "Count",
                8 => "Min", // or TotalFalse for Boolean
                16 => "Max", // or PercentageTrue for Boolean
                32 => "Median", // or PercentageFalse for Boolean
                64 => "Mode",
                _ => null,
            };
        }
        return null;
    }

    private static string[]? GetExpectedTypesForMetric(string metricsType, string metricName)
    {
        return metricsType switch
        {
            "Number" => metricName switch
            {
                "Mean" or "Sum" or "Min" or "Max" or "Median" or "Mode" => new[]
                {
                    "double?",
                    "float?",
                    "decimal?",
                },
                "Count" => new[] { "long?", "int?" },
                _ => null,
            },
            "Integer" => metricName switch
            {
                "Mean" => new[] { "double?", "float?" },
                "Sum" or "Min" or "Max" or "Median" or "Mode" => new[] { "long?", "int?" },
                "Count" => new[] { "long?", "int?" },
                _ => null,
            },
            "Text" => metricName switch
            {
                "Mean" => new[] { "long?", "int?" }, // Count has value 1 for Text
                "Sum" => new[] { "System.Collections.Generic.List<TopOccurrence>?" }, // TopOccurrences has value 2
                _ => null,
            },
            "Boolean" => metricName switch
            {
                "Mean" => new[] { "long?", "int?" }, // Count has value 1
                "Sum" => new[] { "long?", "int?" }, // TotalTrue has value 2
                "Count" => new[] { "long?", "int?" }, // TotalFalse has value 4
                "Min" => new[] { "double?", "float?" }, // PercentageTrue has value 8
                "Max" => new[] { "double?", "float?" }, // PercentageFalse has value 16
                _ => null,
            },
            "Date" => metricName switch
            {
                "Mean" => new[] { "long?", "int?" }, // Count has value 1
                "Sum" => new[] { "DateTime?", "DateTimeOffset?" }, // Min has value 2
                "Count" => new[] { "DateTime?", "DateTimeOffset?" }, // Max has value 4
                "Min" => new[] { "DateTime?", "DateTimeOffset?" }, // Median has value 8
                "Max" => new[] { "DateTime?", "DateTimeOffset?" }, // Mode has value 16
                _ => null,
            },
            _ => null,
        };
    }
}
