using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Weaviate.Client.Managed.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class VectorConfigMethodSignatureAnalyzer : DiagnosticAnalyzer
{
    public const string WrongReturnTypeId = "WCM001";
    public const string WrongSignatureId = "WCM002";
    public const string NotStaticId = "WCM003";

    private static readonly DiagnosticDescriptor WrongReturnTypeRule = new(
        WrongReturnTypeId,
        "ConfigMethod must return the vectorizer type",
        "ConfigMethod '{0}' must return {1} (the vectorizer type)",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ConfigMethod specified in VectorAttribute must return the same type as the vectorizer type parameter."
    );

    private static readonly DiagnosticDescriptor WrongSignatureRule = new(
        WrongSignatureId,
        "ConfigMethod has invalid signature",
        "ConfigMethod '{0}' must have signature: TVectorizer MethodName(string vectorName, TVectorizer prebuilt)",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ConfigMethod must accept string vectorName and TVectorizer prebuilt parameters."
    );

    private static readonly DiagnosticDescriptor NotStaticRule = new(
        NotStaticId,
        "ConfigMethod must be static",
        "ConfigMethod '{0}' must be static",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ConfigMethod specified in VectorAttribute must be a static method."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(WrongReturnTypeRule, WrongSignatureRule, NotStaticRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attributeSyntax = (AttributeSyntax)context.Node;

        // Check if this is a VectorAttribute<T>
        var attributeSymbol = context.SemanticModel.GetTypeInfo(attributeSyntax).Type;
        if (attributeSymbol == null)
            return;

        // Check if it's VectorAttribute<T> (generic attribute with name ending in VectorAttribute)
        if (!IsVectorAttribute(attributeSymbol))
            return;

        // Get the type argument (TVectorizer)
        var namedType = attributeSymbol as INamedTypeSymbol;
        if (namedType?.TypeArguments.Length != 1)
            return;

        var vectorizerType = namedType.TypeArguments[0];

        // Find the ConfigMethod argument
        var configMethodArg = FindConfigMethodArgument(attributeSyntax);
        if (configMethodArg == null)
            return;

        // Get the method name from the ConfigMethod argument
        var methodName = GetMethodNameFromArgument(context.SemanticModel, configMethodArg);
        if (string.IsNullOrEmpty(methodName))
            return;

        // Find the containing type
        var containingType = GetContainingType(attributeSyntax, context.SemanticModel);
        if (containingType == null)
            return;

        // Find the method in the containing type
        var configMethod = FindMethod(containingType, methodName!);
        if (configMethod == null)
            return;

        // Get the location for the diagnostic (the ConfigMethod argument value)
        var diagnosticLocation = GetDiagnosticLocation(configMethodArg);

        // Validate the method
        ValidateMethod(context, configMethod, vectorizerType, diagnosticLocation);
    }

    private static bool IsVectorAttribute(ITypeSymbol typeSymbol)
    {
        // Check if the type is VectorAttribute<T> from Weaviate.Client.Managed.Attributes
        var fullName = typeSymbol.ToDisplayString();
        return fullName.StartsWith("Weaviate.Client.Managed.Attributes.VectorAttribute<");
    }

    private static AttributeArgumentSyntax? FindConfigMethodArgument(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList == null)
            return null;

        foreach (var arg in attribute.ArgumentList.Arguments)
        {
            if (arg.NameEquals?.Name.Identifier.Text == "ConfigMethod")
                return arg;
        }

        return null;
    }

    private static string? GetMethodNameFromArgument(
        SemanticModel semanticModel,
        AttributeArgumentSyntax argument
    )
    {
        // Handle nameof(MethodName)
        if (argument.Expression is InvocationExpressionSyntax invocation)
        {
            if (
                invocation.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.Text == "nameof"
            )
            {
                if (invocation.ArgumentList.Arguments.Count == 1)
                {
                    var nameofArg = invocation.ArgumentList.Arguments[0].Expression;

                    // Could be just MethodName or TypeName.MethodName
                    if (nameofArg is IdentifierNameSyntax methodIdentifier)
                    {
                        return methodIdentifier.Identifier.Text;
                    }

                    if (nameofArg is MemberAccessExpressionSyntax memberAccess)
                    {
                        return memberAccess.Name.Identifier.Text;
                    }
                }
            }
        }

        // Handle string literal "MethodName"
        if (argument.Expression is LiteralExpressionSyntax literal)
        {
            if (literal.Token.Value is string methodName)
                return methodName;
        }

        return null;
    }

    private static INamedTypeSymbol? GetContainingType(
        AttributeSyntax attribute,
        SemanticModel semanticModel
    )
    {
        var propertyDeclaration = attribute.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        if (propertyDeclaration == null)
            return null;

        var propertySymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration);
        return propertySymbol?.ContainingType;
    }

    private static IMethodSymbol? FindMethod(INamedTypeSymbol containingType, string methodName)
    {
        foreach (var member in containingType.GetMembers())
        {
            if (member is IMethodSymbol method && method.Name == methodName)
                return method;
        }

        return null;
    }

    private static Location GetDiagnosticLocation(AttributeArgumentSyntax argument)
    {
        // Try to get the location of the method name specifically
        if (argument.Expression is InvocationExpressionSyntax invocation)
        {
            if (invocation.ArgumentList.Arguments.Count == 1)
            {
                return invocation.ArgumentList.Arguments[0].GetLocation();
            }
        }

        return argument.Expression.GetLocation();
    }

    private static void ValidateMethod(
        SyntaxNodeAnalysisContext context,
        IMethodSymbol method,
        ITypeSymbol vectorizerType,
        Location location
    )
    {
        // Check if method is static
        if (!method.IsStatic)
        {
            var diagnostic = Diagnostic.Create(NotStaticRule, location, method.Name);
            context.ReportDiagnostic(diagnostic);
            return; // Don't report other errors if not static
        }

        // Check return type
        if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, vectorizerType))
        {
            var diagnostic = Diagnostic.Create(
                WrongReturnTypeRule,
                location,
                method.Name,
                vectorizerType.ToDisplayString()
            );
            context.ReportDiagnostic(diagnostic);
            return; // Don't report signature errors if return type is wrong
        }

        // Check parameters: (string, TVectorizer)
        if (method.Parameters.Length != 2)
        {
            var diagnostic = Diagnostic.Create(WrongSignatureRule, location, method.Name);
            context.ReportDiagnostic(diagnostic);
            return;
        }

        var param1 = method.Parameters[0];
        var param2 = method.Parameters[1];

        // First parameter must be string
        if (param1.Type.SpecialType != SpecialType.System_String)
        {
            var diagnostic = Diagnostic.Create(WrongSignatureRule, location, method.Name);
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Second parameter must be TVectorizer
        if (!SymbolEqualityComparer.Default.Equals(param2.Type, vectorizerType))
        {
            var diagnostic = Diagnostic.Create(WrongSignatureRule, location, method.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
