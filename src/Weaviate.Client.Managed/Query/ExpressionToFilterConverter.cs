using System.Linq.Expressions;
using System.Reflection;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Managed.Extensions;
using Weaviate.Client.Models;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Query;

/// <summary>
/// Converts C# lambda expressions to Weaviate Filter objects.
/// Supports binary operations, method calls, and nested property access.
/// </summary>
public static class ExpressionToFilterConverter
{
    /// <summary>
    /// Converts a lambda expression predicate to a Weaviate Filter.
    /// </summary>
    /// <typeparam name="T">The type being filtered.</typeparam>
    /// <param name="predicate">The predicate expression (e.g., x => x.Age > 18).</param>
    /// <returns>A Weaviate Filter object.</returns>
    /// <example>
    /// <code>
    /// var filter = ExpressionToFilterConverter.Convert&lt;Person&gt;(p => p.Age > 18);
    /// var filter2 = ExpressionToFilterConverter.Convert&lt;Article&gt;(a => a.Title.Contains("hello"));
    /// </code>
    /// </example>
    public static Filter Convert<T>(Expression<Func<T, bool>> predicate)
    {
        return ConvertExpression(predicate.Body);
    }

    /// <summary>
    /// Recursively converts an expression to a Filter.
    /// </summary>
    private static Filter ConvertExpression(Expression expression)
    {
        return expression switch
        {
            // Binary expressions: a.Size > 100, a.Name == "test", etc.
            BinaryExpression binary => ConvertBinaryExpression(binary),

            // Method calls: a.Name.Contains("test"), a.Tags.ContainsAny([...])
            MethodCallExpression method => ConvertMethodCall(method),

            // Member access: a.IsActive (bool property)
            MemberExpression member => ConvertMemberExpression(member),

            // Unary: !a.IsActive
            UnaryExpression unary => ConvertUnaryExpression(unary),

            _ => throw new NotSupportedException(
                $"Expression type '{expression.NodeType}' is not supported in filters. "
                    + $"Supported: ==, !=, >, <, >=, <=, &&, ||, !, Contains, ContainsAny, ContainsAll"
            ),
        };
    }

    /// <summary>
    /// Converts binary expressions (comparisons and logical operators).
    /// </summary>
    private static Filter ConvertBinaryExpression(BinaryExpression binary)
    {
        // Handle logical operators (&&, ||)
        if (binary.NodeType == ExpressionType.AndAlso)
        {
            return Filter.AllOf(ConvertExpression(binary.Left), ConvertExpression(binary.Right));
        }

        if (binary.NodeType == ExpressionType.OrElse)
        {
            return Filter.AnyOf(ConvertExpression(binary.Left), ConvertExpression(binary.Right));
        }

        // Handle comparison operators
        // Left side must be a property access
        if (binary.Left is not MemberExpression memberExpr)
        {
            throw new NotSupportedException(
                $"Left side of comparison must be a property access. Got: {binary.Left.NodeType}"
            );
        }

        var value = EvaluateExpression(binary.Right);

        if (TryBuildReferenceFilter(memberExpr, value, binary.NodeType, out var refFilter))
            return refFilter!;

        var propertyPath = PropertyHelper.GetNestedPropertyPath(memberExpr);
        var prop = Filter.Property(propertyPath);

        return binary.NodeType switch
        {
            ExpressionType.Equal => prop.IsEqual(value),
            ExpressionType.NotEqual => prop.IsNotEqual(value),
            ExpressionType.GreaterThan => prop.IsGreaterThan(value),
            ExpressionType.GreaterThanOrEqual => prop.IsGreaterThanEqual(value),
            ExpressionType.LessThan => prop.IsLessThan(value),
            ExpressionType.LessThanOrEqual => prop.IsLessThanEqual(value),
            _ => throw new NotSupportedException(
                $"Binary operator '{binary.NodeType}' is not supported"
            ),
        };
    }

    /// <summary>
    /// Converts method call expressions.
    /// </summary>
    private static Filter ConvertMethodCall(MethodCallExpression method)
    {
        var methodName = method.Method.Name;

        // String.Contains
        if (methodName == "Contains" && method.Object is MemberExpression member)
        {
            var propertyPath = PropertyHelper.GetNestedPropertyPath(member);
            var value = EvaluateExpression(method.Arguments[0]);
            return Filter.Property(propertyPath).IsLike($"%{value}%");
        }

        // List/Array Contains (for checking if a collection contains a value)
        if (methodName == "Contains" && method.Arguments.Count == 2)
        {
            var propertyPath = PropertyHelper.GetNestedPropertyPath(
                (MemberExpression)method.Arguments[0]
            );
            var value = EvaluateExpression(method.Arguments[1]);
            return Filter.Property(propertyPath).ContainsAny(new[] { value });
        }

        // GeoCoordinate.IsWithinGeoRange(lat, lon, distance) or IsWithinGeoRange(constraint)
        if (methodName == nameof(GeoCoordinateExtensions.IsWithinGeoRange))
        {
            // The instance is the GeoCoordinate property (first arg for extension methods)
            var instanceExpr =
                method.Object
                ?? (method.Arguments.Count > 0 ? method.Arguments[0] : null) as MemberExpression;

            if (instanceExpr is not MemberExpression geoMember)
                throw new NotSupportedException(
                    "IsWithinGeoRange must be called on a GeoCoordinate property expression."
                );

            var propertyPath = PropertyHelper.GetNestedPropertyPath(geoMember);
            var prop = Filter.Property(propertyPath);

            // extension method: args[0]=GeoCoordinate, remaining args are the actual params
            int argOffset = method.Object == null ? 1 : 0;

            if (method.Arguments.Count == argOffset + 1)
            {
                // IsWithinGeoRange(GeoCoordinateConstraint)
                var constraint =
                    EvaluateExpression(method.Arguments[argOffset]) as GeoCoordinateConstraint
                    ?? throw new NotSupportedException(
                        "IsWithinGeoRange argument must evaluate to a GeoCoordinateConstraint."
                    );
                return prop.IsWithinGeoRange(constraint);
            }

            if (method.Arguments.Count == argOffset + 3)
            {
                // IsWithinGeoRange(float latitude, float longitude, float distance)
                var lat = System.Convert.ToSingle(EvaluateExpression(method.Arguments[argOffset]));
                var lon = System.Convert.ToSingle(
                    EvaluateExpression(method.Arguments[argOffset + 1])
                );
                var dist = System.Convert.ToSingle(
                    EvaluateExpression(method.Arguments[argOffset + 2])
                );
                return prop.IsWithinGeoRange(new GeoCoordinate(lat, lon), dist);
            }

            throw new NotSupportedException(
                "IsWithinGeoRange must be called with (float lat, float lon, float distance) "
                    + "or (GeoCoordinateConstraint constraint)."
            );
        }

        // Extension methods or custom methods
        // ContainsAny, ContainsAll, ContainsNone
        if (methodName is "ContainsAny" or "ContainsAll" or "ContainsNone")
        {
            if (method.Arguments.Count < 2)
                throw new NotSupportedException(
                    $"Method '{methodName}' requires at least 2 arguments"
                );

            var propertyPath = PropertyHelper.GetNestedPropertyPath(
                (MemberExpression)method.Arguments[0]
            );
            var values =
                EvaluateExpression(method.Arguments[1]) as System.Collections.IEnumerable
                ?? throw new NotSupportedException(
                    $"Second argument to '{methodName}' must be enumerable"
                );

            var valueList = values.Cast<object>().ToList();

            return methodName switch
            {
                "ContainsAny" => Filter.Property(propertyPath).ContainsAny(valueList),
                "ContainsAll" => Filter.Property(propertyPath).ContainsAll(valueList),
                "ContainsNone" => Filter.Property(propertyPath).ContainsNone(valueList),
                _ => throw new NotSupportedException(),
            };
        }

        throw new NotSupportedException(
            $"Method '{methodName}' is not supported in filters. "
                + $"Supported methods: Contains, ContainsAny, ContainsAll, ContainsNone, IsWithinGeoRange"
        );
    }

    /// <summary>
    /// Converts member expressions (bool properties).
    /// </summary>
    private static Filter ConvertMemberExpression(MemberExpression member)
    {
        // For boolean properties: a.IsActive => a.IsActive == true
        var propertyPath = PropertyHelper.GetNestedPropertyPath(member);
        return Filter.Property(propertyPath).IsEqual(true);
    }

    /// <summary>
    /// Converts unary expressions (negation).
    /// </summary>
    private static Filter ConvertUnaryExpression(UnaryExpression unary)
    {
        if (unary.NodeType == ExpressionType.Not)
        {
            return Filter.Not(ConvertExpression(unary.Operand));
        }

        throw new NotSupportedException($"Unary operator '{unary.NodeType}' is not supported");
    }

    /// <summary>
    /// Decomposes a member expression chain into a top-down list of PropertyInfo segments.
    /// Example: r.Product.UUID → [PropertyInfo(Product on ProductReview), PropertyInfo(UUID on Product)]
    /// </summary>
    private static List<PropertyInfo> DecomposeMemberChain(MemberExpression member)
    {
        var parts = new Stack<PropertyInfo>();
        var current = member;

        while (current != null)
        {
            if (current.Member is not PropertyInfo prop)
                return [];

            parts.Push(prop);
            current = current.Expression as MemberExpression;
        }

        return parts.ToList(); // Stack enumerates top-first → top-down order
    }

    /// <summary>
    /// Attempts to build a ReferenceFilter when the member expression crosses a [Reference] property.
    /// Returns false if no reference boundary is detected, falling back to the standard property path.
    /// </summary>
    private static bool TryBuildReferenceFilter(
        MemberExpression memberExpr,
        object value,
        ExpressionType op,
        out Filter? filter
    )
    {
        var chain = DecomposeMemberChain(memberExpr);

        // Need at least 2 segments: one reference hop + one property to filter on
        if (chain.Count < 2)
        {
            filter = null;
            return false;
        }

        // Find the first segment (not the last) that has [ReferenceAttribute]
        int firstRefIdx = -1;
        for (int i = 0; i < chain.Count - 1; i++)
        {
            if (chain[i].GetCustomAttribute<ReferenceAttribute>() != null)
            {
                firstRefIdx = i;
                break;
            }
        }

        if (firstRefIdx < 0)
        {
            filter = null;
            return false;
        }

        // Build the reference filter chain for all reference hops
        var refFilter = Filter.Reference(PropertyHelper.ToCamelCase(chain[firstRefIdx].Name));
        for (int i = firstRefIdx + 1; i < chain.Count - 1; i++)
            refFilter = refFilter.Reference(PropertyHelper.ToCamelCase(chain[i].Name));

        // Apply the operator to the final property
        var lastProp = chain[^1];
        bool isId = lastProp.GetCustomAttribute<WeaviateUUIDAttribute>() != null;

        if (isId)
        {
            var uuid = refFilter.UUID;
            filter = op switch
            {
                ExpressionType.Equal => uuid.IsEqual((Guid)value),
                ExpressionType.NotEqual => uuid.IsNotEqual((Guid)value),
                _ => throw new NotSupportedException(
                    $"Operator '{op}' is not supported for UUID reference filters. Use == or !=."
                ),
            };
        }
        else
        {
            var propFilter = refFilter.Property(PropertyHelper.ToCamelCase(lastProp.Name));
            filter = op switch
            {
                ExpressionType.Equal => propFilter.IsEqual(value),
                ExpressionType.NotEqual => propFilter.IsNotEqual(value),
                ExpressionType.GreaterThan => propFilter.IsGreaterThan(value),
                ExpressionType.GreaterThanOrEqual => propFilter.IsGreaterThanEqual(value),
                ExpressionType.LessThan => propFilter.IsLessThan(value),
                ExpressionType.LessThanOrEqual => propFilter.IsLessThanEqual(value),
                _ => throw new NotSupportedException(
                    $"Operator '{op}' is not supported in reference filters"
                ),
            };
        }

        return true;
    }

    /// <summary>
    /// Evaluates an expression to get its constant value.
    /// </summary>
    private static object EvaluateExpression(Expression expression)
    {
        // Handle constants directly
        if (expression is ConstantExpression constant)
        {
            return constant.Value
                ?? throw new InvalidOperationException("Filter value cannot be null");
        }

        // Handle member access (variables, fields, properties)
        if (expression is MemberExpression memberExpr)
        {
            var lambda = Expression.Lambda(memberExpr);
            var value = lambda.Compile().DynamicInvoke();
            return value ?? throw new InvalidOperationException("Filter value cannot be null");
        }

        // For more complex expressions, compile and evaluate
        try
        {
            var lambda = Expression.Lambda(expression);
            var value = lambda.Compile().DynamicInvoke();
            return value ?? throw new InvalidOperationException("Filter value cannot be null");
        }
        catch (Exception ex)
        {
            throw new NotSupportedException(
                $"Unable to evaluate expression: {expression}. "
                    + $"Filters must use constant values or variables, not computed values. "
                    + $"Error: {ex.Message}",
                ex
            );
        }
    }
}
