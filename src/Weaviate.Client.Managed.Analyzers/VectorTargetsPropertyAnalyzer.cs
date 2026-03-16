using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Weaviate.Client.Managed.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class VectorTargetsPropertyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "WVMTA001";
        private static readonly LocalizableString Title =
            "Only [Vector] properties allowed in VectorTargets";
        private static readonly LocalizableString MessageFormat =
            "Property '{0}' used in VectorTargets is not marked with [Vector] attribute";
        private static readonly LocalizableString Description =
            "All properties used in .VectorTargets() must be marked with [Vector] attribute.";
        private const string Category = "Usage";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
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
            var memberAccess = invocation.Expression as MemberAccessExpressionSyntax;
            if (memberAccess == null)
                return;

            // Check for .VectorTargets(...)
            if (memberAccess.Name.Identifier.Text != "VectorTargets")
                return;

            // Find all lambda expressions inside the argument list
            var lambdas = invocation
                .ArgumentList.Arguments.SelectMany(arg =>
                    arg.DescendantNodesAndSelf().Cast<SyntaxNode>()
                )
                .Where(n =>
                    n is SimpleLambdaExpressionSyntax || n is ParenthesizedLambdaExpressionSyntax
                )
                .Cast<LambdaExpressionSyntax>()
                .ToList();

            foreach (var lambda in lambdas)
            {
                // Find all property accesses in the lambda body
                var memberAccesses = lambda
                    .DescendantNodes()
                    .OfType<MemberAccessExpressionSyntax>();
                foreach (var access in memberAccesses)
                {
                    var symbol =
                        context.SemanticModel.GetSymbolInfo(access).Symbol as IPropertySymbol;
                    if (symbol == null)
                        continue;

                    // Check for [Vector] attribute
                    var hasVector = symbol
                        .GetAttributes()
                        .Any(attr =>
                            attr.AttributeClass?.Name == "VectorAttribute"
                            || attr.AttributeClass?.ToDisplayString().EndsWith(".VectorAttribute")
                                == true
                        );
                    if (!hasVector)
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule,
                            access.Name.GetLocation(),
                            symbol.Name
                        );
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
