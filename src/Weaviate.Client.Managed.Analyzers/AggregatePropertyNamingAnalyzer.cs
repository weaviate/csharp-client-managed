using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Weaviate.Client.Managed.Analyzers;

/// <summary>
/// Analyzer that validates aggregate property naming conventions and attribute usage.
/// Replicates Weaviate.Client's AggregatePropertySuffixAnalyzer rules for the Managed client.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AggregatePropertyNamingAnalyzer : DiagnosticAnalyzer
{
    public const string MissingContextDiagnosticId = "WEAVIATE006";
    public const string InvalidPropertyNameDiagnosticId = "WEAVIATE007";
    public const string MissingSuffixDiagnosticId = "WEAVIATE008";

    private const string Category = "Naming";

    private static readonly LocalizableString MissingContextTitle =
        "[Metrics] attribute requires [QueryAggregate<T>] on class";
    private static readonly LocalizableString MissingContextMessageFormat =
        "Property '{0}' has [Metrics] attribute but class '{1}' is missing [QueryAggregate<T>] attribute";
    private static readonly LocalizableString MissingContextDescription =
        "The [Metrics] attribute can only be used on properties in classes decorated with [QueryAggregate<TEntity>].";

    private static readonly LocalizableString InvalidPropertyNameTitle =
        "Property name in [Metrics] does not exist in entity";
    private static readonly LocalizableString InvalidPropertyNameMessageFormat =
        "Property '{0}' references '{1}' which does not exist in entity type '{2}'";
    private static readonly LocalizableString InvalidPropertyNameDescription =
        "The property name specified in [Metrics(\"propertyName\", metric)] must reference a valid property in the entity type from [QueryAggregate<TEntity>].";

    private static readonly LocalizableString MissingSuffixTitle =
        "Property name does not follow suffix convention";
    private static readonly LocalizableString MissingSuffixMessageFormat =
        "Property '{0}' with [Metrics(\"{1}\", {2}.{3})] should be named '{4}' to follow the convention";
    private static readonly LocalizableString MissingSuffixDescription =
        "Properties with single-metric extraction should follow the naming convention: PropertyName + MetricSuffix (e.g., 'price' + 'Mean' = 'PriceMean').";

    private static readonly DiagnosticDescriptor MissingContextRule = new(
        MissingContextDiagnosticId,
        MissingContextTitle,
        MissingContextMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: MissingContextDescription
    );

    private static readonly DiagnosticDescriptor InvalidPropertyNameRule = new(
        InvalidPropertyNameDiagnosticId,
        InvalidPropertyNameTitle,
        InvalidPropertyNameMessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: InvalidPropertyNameDescription
    );

    private static readonly DiagnosticDescriptor MissingSuffixRule = new(
        MissingSuffixDiagnosticId,
        MissingSuffixTitle,
        MissingSuffixMessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: MissingSuffixDescription
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MissingContextRule, InvalidPropertyNameRule, MissingSuffixRule);

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

        // Check if property has [Metrics] attribute
        var metricsAttribute = propertySymbol
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "MetricsAttribute");
        if (metricsAttribute == null)
            return;

        var containingType = propertySymbol.ContainingType;
        if (containingType == null)
            return;

        // WEAVIATE006: Verify containing class has [QueryAggregate<T>] attribute
        var queryAggregateAttr = containingType
            .GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "QueryAggregateAttribute");

        if (queryAggregateAttr == null)
        {
            var diagnostic = Diagnostic.Create(
                MissingContextRule,
                propertyDeclaration.Identifier.GetLocation(),
                propertySymbol.Name,
                containingType.Name
            );
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Get TEntity from QueryAggregate<TEntity>
        var entityType =
            queryAggregateAttr.AttributeClass?.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
        if (entityType == null)
            return;

        // Check if this is single-metric extraction (2 arguments: propertyName, metric)
        if (metricsAttribute.ConstructorArguments.Length == 2)
        {
            var propertyNameArg = metricsAttribute.ConstructorArguments[0];
            var metricArg = metricsAttribute.ConstructorArguments[1];

            if (propertyNameArg.Value is not string sourcePropertyName)
                return;

            // WEAVIATE007: Validate that the source property exists in TEntity
            var sourceProperty = entityType
                .GetMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p =>
                    string.Equals(
                        p.Name,
                        sourcePropertyName,
                        System.StringComparison.OrdinalIgnoreCase
                    )
                );

            if (sourceProperty == null)
            {
                var diagnostic = Diagnostic.Create(
                    InvalidPropertyNameRule,
                    propertyDeclaration.Identifier.GetLocation(),
                    propertySymbol.Name,
                    sourcePropertyName,
                    entityType.Name
                );
                context.ReportDiagnostic(diagnostic);
                return;
            }

            // WEAVIATE008: Validate property name follows suffix convention
            // Extract metric name from enum (e.g., Metric.Number.Mean -> "Mean")
            if (metricArg.Value is int metricValue)
            {
                var metricType = metricArg.Type as INamedTypeSymbol;
                if (metricType != null)
                {
                    var metricName = GetMetricName(metricType, metricValue);
                    if (metricName != null)
                    {
                        var expectedPropertyName = ToPascalCase(sourcePropertyName) + metricName;
                        if (
                            !string.Equals(
                                propertySymbol.Name,
                                expectedPropertyName,
                                System.StringComparison.Ordinal
                            )
                        )
                        {
                            var metricTypeName = metricType.Name; // e.g., "Number", "Integer"
                            var diagnostic = Diagnostic.Create(
                                MissingSuffixRule,
                                propertyDeclaration.Identifier.GetLocation(),
                                propertySymbol.Name,
                                sourcePropertyName,
                                "Metric",
                                metricTypeName + "." + metricName,
                                expectedPropertyName
                            );
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }
    }

    private static string? GetMetricName(INamedTypeSymbol enumType, int value)
    {
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (member.ConstantValue is int memberValue && memberValue == value)
            {
                return member.Name;
            }
        }
        return null;
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // Handle all-uppercase: STOCK -> Stock
        bool isAllUpper = input.All(c => !char.IsLetter(c) || char.IsUpper(c));
        if (isAllUpper && input.Length > 1)
        {
            return char.ToUpperInvariant(input[0]) + input.Substring(1).ToLowerInvariant();
        }

        // Handle all-lowercase: stock -> Stock
        bool isAllLower = input.All(c => !char.IsLetter(c) || char.IsLower(c));
        if (isAllLower)
        {
            return char.ToUpperInvariant(input[0]) + (input.Length > 1 ? input.Substring(1) : "");
        }

        // Already in correct case or mixed case - capitalize first letter
        return char.ToUpperInvariant(input[0]) + (input.Length > 1 ? input.Substring(1) : "");
    }
}
