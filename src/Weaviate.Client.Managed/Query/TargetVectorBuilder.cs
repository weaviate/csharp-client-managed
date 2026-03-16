using System.Linq.Expressions;
using System.Reflection;
using Weaviate.Client.Models;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Query;

/// <summary>
/// Fluent builder for configuring multi-target vector combinations.
/// Used inside the <c>.VectorTargets(t =&gt; t.Sum(...))</c> callback on
/// <see cref="CollectionMapperQueryClient{T}"/>.
/// </summary>
/// <typeparam name="T">The entity type being queried.</typeparam>
/// <example>
/// <code>
/// // Named-vector sum (NearText, Hybrid, NearObject):
/// query.NearText("search").Targets(t => t.Sum(c => c.Embedding!, c => c.Desc!))
///
/// // Per-vector sum (NearVector):
/// query.NearVector().Targets(t => t.Sum((c => c.Embedding!, vec1), (c => c.Desc!, vec2)))
///
/// // Manual weights with names only:
/// query.NearText("search").Targets(t => t.ManualWeights(
///     (c => c.Embedding!, 0.7), (c => c.Desc!, 0.3)))
/// </code>
/// </example>
public sealed class TargetVectorBuilder<T>
    where T : class, new()
{
    internal enum BuildMode
    {
        NameOnly,
        PerVector,
    }

    internal BuildMode Mode { get; private set; } = BuildMode.NameOnly;
    internal VectorCombination Combination { get; private set; }

    // Name-only targets (Sum / Average / Minimum without per-vector data)
    internal List<string>? VectorNames { get; private set; }

    // Weighted name-only targets (ManualWeights / RelativeScore without per-vector data)
    internal List<(string name, double weight)>? Weights { get; private set; }

    // Per-target vectors without weights (Sum / Average / Minimum with per-vector data)
    internal List<(string name, Vector vector)>? PerTargetVectors { get; private set; }

    // Per-target vectors with weights (ManualWeights / RelativeScore with per-vector data)
    internal List<(string name, double weight, Vector vector)>? WeightedPerTargetVectors
    {
        get;
        private set;
    }

    #region Named-vector-only overloads (NearText / NearObject / Hybrid)

    /// <summary>Combines multiple named vectors using Sum.</summary>
    public TargetVectorBuilder<T> Sum(params Expression<Func<T, object>>[] vectors)
    {
        Combination = VectorCombination.Sum;
        Mode = BuildMode.NameOnly;
        VectorNames = vectors.Select(GetVectorName).ToList();
        return this;
    }

    /// <summary>Combines multiple named vectors using Average.</summary>
    public TargetVectorBuilder<T> Average(params Expression<Func<T, object>>[] vectors)
    {
        Combination = VectorCombination.Average;
        Mode = BuildMode.NameOnly;
        VectorNames = vectors.Select(GetVectorName).ToList();
        return this;
    }

    /// <summary>Combines multiple named vectors using Minimum (closest distance wins).</summary>
    public TargetVectorBuilder<T> Minimum(params Expression<Func<T, object>>[] vectors)
    {
        Combination = VectorCombination.Minimum;
        Mode = BuildMode.NameOnly;
        VectorNames = vectors.Select(GetVectorName).ToList();
        return this;
    }

    /// <summary>Combines multiple named vectors using relative score weights.</summary>
    public TargetVectorBuilder<T> RelativeScore(
        params (Expression<Func<T, object>> vector, double weight)[] targets
    )
    {
        Combination = VectorCombination.RelativeScore;
        Mode = BuildMode.NameOnly;
        Weights = targets.Select(t => (GetVectorName(t.vector), t.weight)).ToList();
        return this;
    }

    /// <summary>Combines multiple named vectors using explicit weights.</summary>
    public TargetVectorBuilder<T> ManualWeights(
        params (Expression<Func<T, object>> vector, double weight)[] targets
    )
    {
        Combination = VectorCombination.ManualWeights;
        Mode = BuildMode.NameOnly;
        Weights = targets.Select(t => (GetVectorName(t.vector), t.weight)).ToList();
        return this;
    }

    #endregion

    #region Per-target-vector overloads (NearVector / Hybrid with vector data)

    /// <summary>Combines per-target vectors using Sum.</summary>
    public TargetVectorBuilder<T> Sum(
        params (Expression<Func<T, object>> vector, Vector values)[] targets
    )
    {
        Combination = VectorCombination.Sum;
        Mode = BuildMode.PerVector;
        PerTargetVectors = targets.Select(t => (GetVectorName(t.vector), t.values)).ToList();
        return this;
    }

    /// <summary>Combines per-target vectors using Average.</summary>
    public TargetVectorBuilder<T> Average(
        params (Expression<Func<T, object>> vector, Vector values)[] targets
    )
    {
        Combination = VectorCombination.Average;
        Mode = BuildMode.PerVector;
        PerTargetVectors = targets.Select(t => (GetVectorName(t.vector), t.values)).ToList();
        return this;
    }

    /// <summary>Combines per-target vectors using Minimum.</summary>
    public TargetVectorBuilder<T> Minimum(
        params (Expression<Func<T, object>> vector, Vector values)[] targets
    )
    {
        Combination = VectorCombination.Minimum;
        Mode = BuildMode.PerVector;
        PerTargetVectors = targets.Select(t => (GetVectorName(t.vector), t.values)).ToList();
        return this;
    }

    /// <summary>Combines per-target vectors using relative score weights.</summary>
    public TargetVectorBuilder<T> RelativeScore(
        params (Expression<Func<T, object>> vector, Vector values, double weight)[] targets
    )
    {
        Combination = VectorCombination.RelativeScore;
        Mode = BuildMode.PerVector;
        WeightedPerTargetVectors = targets
            .Select(t => (GetVectorName(t.vector), t.weight, t.values))
            .ToList();
        return this;
    }

    /// <summary>Combines per-target vectors using explicit weights.</summary>
    public TargetVectorBuilder<T> ManualWeights(
        params (Expression<Func<T, object>> vector, Vector values, double weight)[] targets
    )
    {
        Combination = VectorCombination.ManualWeights;
        Mode = BuildMode.PerVector;
        WeightedPerTargetVectors = targets
            .Select(t => (GetVectorName(t.vector), t.weight, t.values))
            .ToList();
        return this;
    }

    #endregion

    #region Internal string-based overloads (used by ProjectionMapper.GetVectorTargetConfig)

    internal TargetVectorBuilder<T> SetNameOnly(VectorCombination combination, string[] names)
    {
        Combination = combination;
        Mode = BuildMode.NameOnly;
        VectorNames = names.ToList();
        return this;
    }

    internal TargetVectorBuilder<T> SetWeightedNameOnly(
        VectorCombination combination,
        (string name, double weight)[] weights
    )
    {
        Combination = combination;
        Mode = BuildMode.NameOnly;
        Weights = weights.ToList();
        return this;
    }

    #endregion

    private static string GetVectorName(Expression<Func<T, object>> expression)
    {
        var propName = expression.Body switch
        {
            MemberExpression me => me.Member.Name,
            UnaryExpression { Operand: MemberExpression me } => me.Member.Name,
            _ => throw new ArgumentException(
                $"Expression must reference a property directly: {expression}"
            ),
        };
        return PropertyHelper.ToCamelCase(propName);
    }
}
