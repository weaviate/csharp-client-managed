using System.Linq.Expressions;
using Weaviate.Client.Managed.Context;

namespace Weaviate.Client.Managed.Query;

internal sealed class WeaviateQueryProvider<T> : IQueryProvider
    where T : class, new()
{
    private readonly CollectionSet<T> _root;
    private readonly Func<CollectionMapperQueryClient<T>> _factory;

    internal WeaviateQueryProvider(
        CollectionSet<T> root,
        Func<CollectionMapperQueryClient<T>> factory
    )
    {
        _root = root;
        _factory = factory;
    }

    public IQueryable CreateQuery(Expression expression) => CreateQuery<T>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        if (expression is not MethodCallExpression methodCall)
            throw new ArgumentException("Expected a MethodCallExpression.", nameof(expression));

        // Extract the source queryable and its config/ops
        var sourceExpr = methodCall.Arguments[0];
        WeaviateQueryConfig config;
        IReadOnlyList<PendingOp> ops;

        if (sourceExpr is ConstantExpression { Value: CollectionSet<T> })
        {
            config = new WeaviateQueryConfig();
            ops = [];
        }
        else if (sourceExpr is ConstantExpression { Value: WeaviateQueryable<T> wq })
        {
            config = wq.Config;
            ops = wq.Ops;
        }
        else
        {
            throw new NotSupportedException(
                $"WeaviateQueryProvider<{typeof(T).Name}> does not support the expression: {expression}"
            );
        }

        var methodName = methodCall.Method.Name;

        PendingOp? newOp = methodName switch
        {
            "Where" => new PendingOp(
                PendingOpKind.Where,
                (Expression<Func<T, bool>>)UnquoteLambda(methodCall.Arguments[1])
            ),
            "OrderBy" => new PendingOp(
                PendingOpKind.OrderBy,
                UnquoteLambda(methodCall.Arguments[1])
            ),
            "OrderByDescending" => new PendingOp(
                PendingOpKind.OrderByDesc,
                UnquoteLambda(methodCall.Arguments[1])
            ),
            "ThenBy" => new PendingOp(PendingOpKind.ThenBy, UnquoteLambda(methodCall.Arguments[1])),
            "ThenByDescending" => new PendingOp(
                PendingOpKind.ThenByDesc,
                UnquoteLambda(methodCall.Arguments[1])
            ),
            "Take" => new PendingOp(PendingOpKind.Take, EvalConstant<int>(methodCall.Arguments[1])),
            "Skip" => new PendingOp(PendingOpKind.Skip, EvalConstant<int>(methodCall.Arguments[1])),
            "Select" => HandleSelect(methodCall),
            "GroupBy" => throw new NotSupportedException(
                "GroupBy is not supported. Use Aggregate() for aggregation queries."
            ),
            "Join" => throw new NotSupportedException(
                "Join is not supported. Use WithReferences() for cross-collection queries."
            ),
            "Distinct" => throw new NotSupportedException(
                "Distinct is not supported by WeaviateQueryable<T>."
            ),
            "Sum" or "Min" or "Max" or "Average" => throw new NotSupportedException(
                $"'{methodName}' is not supported. Use context.{typeof(T).Name}.Aggregate() for numeric aggregations."
            ),
            _ => throw new NotSupportedException(
                $"LINQ method '{methodName}' is not supported by WeaviateQueryable<T>. "
                    + "Supported methods: Where, OrderBy, OrderByDescending, ThenBy, ThenByDescending, Take, Skip."
            ),
        };

        var newOps = newOp != null ? (IReadOnlyList<PendingOp>)[.. ops, newOp] : ops;
        var result = new WeaviateQueryable<T>(config, newOps, _factory, this);

        // Cast to the requested element type — for identity Select, TElement == T
        if (result is IQueryable<TElement> typed)
            return typed;

        throw new InvalidOperationException(
            $"Cannot return WeaviateQueryable<{typeof(T).Name}> as IQueryable<{typeof(TElement).Name}>."
        );
    }

    public object? Execute(Expression expression) =>
        throw new InvalidOperationException(
            "Use 'await' to execute a WeaviateQueryable<T> query. Synchronous execution is not supported."
        );

    public TResult Execute<TResult>(Expression expression) =>
        throw new InvalidOperationException(
            "Use 'await' to execute a WeaviateQueryable<T> query. Synchronous execution is not supported."
        );

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static LambdaExpression UnquoteLambda(Expression quoted) =>
        quoted is UnaryExpression { NodeType: ExpressionType.Quote } u
            ? (LambdaExpression)u.Operand
            : (LambdaExpression)quoted;

    private static TValue EvalConstant<TValue>(Expression expr) =>
        (TValue)Expression.Lambda(expr).Compile().DynamicInvoke()!;

    private static PendingOp? HandleSelect(MethodCallExpression methodCall)
    {
        var lambda = UnquoteLambda(methodCall.Arguments[1]);
        if (IsIdentityLambda(lambda))
            return null; // identity select → no-op

        throw new NotSupportedException(
            "Select projections are not supported in the IQueryable API. "
                + "Use Query().Project<TProjection>() for typed projections."
        );
    }

    private static bool IsIdentityLambda(LambdaExpression lambda) =>
        lambda.Body is ParameterExpression p && p == lambda.Parameters[0];
}
