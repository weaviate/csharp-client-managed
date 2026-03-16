using System.Linq.Expressions;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed.Aggregates;

/// <summary>
/// Type-safe builder for constructing aggregate metrics using expression trees.
/// Provides compile-time safety and refactoring support for property selection.
/// </summary>
/// <typeparam name="TModel">The model type being aggregated.</typeparam>
/// <example>
/// <code>
/// var stats = await products.Aggregate
///     .WithMetrics(
///         m => m.Property(p => p.Price, Metric.Number.Mean, Metric.Number.Min),
///         m => m.Property(p => p.Quantity, Metric.Integer.Sum)
///     );
/// </code>
/// </example>
public class MetricsBuilder<TModel>
    where TModel : class
{
    /// <summary>
    /// Creates an aggregate metric for a property using a type-safe expression.
    /// The property name is extracted from the expression and converted to camelCase.
    /// </summary>
    /// <typeparam name="TProp">The property type.</typeparam>
    /// <param name="property">Expression selecting the property to aggregate.</param>
    /// <param name="metrics">One or more metrics to compute for the property.</param>
    /// <returns>An aggregate metric for the specified property.</returns>
    /// <example>
    /// <code>
    /// var stats = await products.Aggregate
    ///     .WithMetrics(
    ///         m => m.Property(p => p.Price, Metric.Number.Mean, Metric.Number.Min, Metric.Number.Max),
    ///         m => m.Property(p => p.Quantity, Metric.Integer.Sum),
    ///         m => m.Property(p => p.InStock, Metric.Boolean.TotalTrue)
    ///     );
    /// </code>
    /// </example>
    public Aggregate.Metric Property<TProp>(
        Expression<Func<TModel, TProp>> property,
        params object[] metrics
    )
    {
        if (metrics == null || metrics.Length == 0)
        {
            throw new ArgumentException("At least one metric must be specified", nameof(metrics));
        }

        var propertyName = PropertyHelper.GetPropertyName(property);
        var camelCaseName = PropertyHelper.ToCamelCase(propertyName);

        // Use the MetricsExtractor logic to create the metric
        return MetricsExtractor.CreateMetricFromEnums(camelCaseName, metrics);
    }
}
