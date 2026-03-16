using System;

namespace Weaviate.Client.Managed.Attributes
{
    /// <summary>
    /// Specifies which aggregate metrics to compute for an aggregate property.
    /// Use with Metric.Number, Metric.Integer, Metric.Text, Metric.Boolean, or Metric.Date enums.
    /// <para>
    /// Two usage patterns:
    /// <list type="bullet">
    /// <item><description>Full aggregate type: [Metrics(Metric.Number.Mean, Metric.Number.Sum)] on Aggregate.Number property</description></item>
    /// <item><description>Single metric: [Metrics("price", Metric.Number.Mean)] on double? property for scalar extraction</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// [QueryAggregate&lt;Product&gt;]
    /// public class ProductStats
    /// {
    ///     // Full aggregate type with multiple metrics
    ///     [Metrics(Metric.Number.Mean, Metric.Number.Sum)]
    ///     public Aggregate.Number Price { get; set; }
    ///
    ///     // Individual metric extraction to scalar property
    ///     [Metrics("price", Metric.Number.Mean)]
    ///     public double? AveragePrice { get; set; }
    ///
    ///     [Metrics("price", Metric.Number.Count)]
    ///     public long? PriceCount { get; set; }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public class MetricsAttribute : Attribute
    {
        /// <summary>
        /// Gets the source property name in the collection (camelCase). Null when using full aggregate types.
        /// </summary>
        public string? PropertyName { get; }

        /// <summary>
        /// Gets the metrics to compute. Array of Metric.Number, Metric.Integer, Metric.Text, Metric.Boolean, or Metric.Date values.
        /// When PropertyName is set, this should contain exactly one metric value.
        /// </summary>
        public object[] MetricValues { get; }

        /// <summary>
        /// Creates a new MetricsAttribute for full aggregate type usage.
        /// </summary>
        /// <param name="metrics">The metrics to compute (e.g., Metric.Number.Mean, Metric.Number.Sum).</param>
        public MetricsAttribute(params object[] metrics)
        {
            MetricValues = metrics ?? throw new ArgumentNullException(nameof(metrics));
            PropertyName = null;
        }

        /// <summary>
        /// Creates a new MetricsAttribute for single metric extraction to scalar property.
        /// </summary>
        /// <param name="propertyName">The source property name in the collection (will be converted to camelCase).</param>
        /// <param name="metric">The single metric to extract (e.g., Metric.Number.Mean).</param>
        public MetricsAttribute(string propertyName, object metric)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            MetricValues = new[] { metric ?? throw new ArgumentNullException(nameof(metric)) };
        }
    }
}
