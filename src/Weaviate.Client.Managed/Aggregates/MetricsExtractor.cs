using System.Reflection;
using Humanizer;
using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;
using PropertyHelper = Weaviate.Client.Managed.Internal.PropertyHelper;

namespace Weaviate.Client.Managed;

/// <summary>
/// Extracts aggregate metrics from a result type by examining attributes and property names.
/// Supports both attribute-based and convention-based property mapping.
/// </summary>
internal static class MetricsExtractor
{
    /// <summary>
    /// Extracts metrics from a result type using both [Metrics] attributes and naming conventions.
    /// </summary>
    /// <typeparam name="TResult">The result type containing aggregate properties.</typeparam>
    /// <returns>Collection of metrics to request from Weaviate.</returns>
    public static IEnumerable<Aggregate.Metric> FromType<TResult>()
        where TResult : class, new()
    {
        var resultType = typeof(TResult);
        var properties = resultType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var metrics = new List<Aggregate.Metric>();

        foreach (var prop in properties)
        {
            // Try attribute-based first
            var metricsAttr = prop.GetCustomAttribute<MetricsAttribute>();
            if (metricsAttr != null)
            {
                metrics.AddRange(ExtractFromAttribute(prop, metricsAttr));
                continue;
            }

            // Fall back to convention-based
            var conventionMetrics = ExtractFromConvention(prop);
            if (conventionMetrics != null)
            {
                metrics.AddRange(conventionMetrics);
            }
        }

        return metrics;
    }

    /// <summary>
    /// Extracts metrics from a [Metrics] attribute.
    /// </summary>
    private static IEnumerable<Aggregate.Metric> ExtractFromAttribute(
        PropertyInfo prop,
        MetricsAttribute attr
    )
    {
        // Check if it's a full aggregate type (Pattern 1)
        if (IsAggregateType(prop.PropertyType))
        {
            // Extract property name from the result class property
            var propertyName = PropertyHelper.ToCamelCase(prop.Name);

            // For aggregate types, we need to group all metrics for the same property
            // and create a single Aggregate.Metric with all booleans set
            return [CreateCombinedMetric(propertyName, attr.MetricValues)];
        }
        else
        {
            // Single metric extraction (Pattern 2)
            if (attr.PropertyName == null || attr.MetricValues.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Property '{prop.Name}' uses scalar type but [Metrics] attribute is invalid. "
                        + "Use [Metrics(\"propertyName\", Metric.xxx.Yyy)] for scalar properties."
                );
            }

            var propertyName = PropertyHelper.ToCamelCase(attr.PropertyName);
            var metric = attr.MetricValues[0];
            return [CreateMetricForProperty(propertyName, metric)];
        }
    }

    /// <summary>
    /// Extracts metrics from property name using convention: PropertyName + MetricSuffix.
    /// Example: PriceMean → price.mean, StockCount → stock.count
    /// </summary>
    private static IEnumerable<Aggregate.Metric>? ExtractFromConvention(PropertyInfo prop)
    {
        // Skip if it's a full Aggregate type (those require attributes)
        if (IsAggregateType(prop.PropertyType))
        {
            return null;
        }

        // Try to parse property name: PropertyName + MetricSuffix
        var parsed = ParsePropertyNameConvention(prop.Name);
        if (parsed == null)
        {
            return null;
        }

        var (sourceProperty, metricType, metricName) = parsed.Value;
        var propertyName = PropertyHelper.ToCamelCase(sourceProperty);

        // Create the appropriate metric enum value
        object? metricEnum = metricType switch
        {
            "Number" => ParseNumberMetric(metricName),
            "Integer" => ParseIntegerMetric(metricName),
            "Text" => ParseTextMetric(metricName),
            "Boolean" => ParseBooleanMetric(metricName),
            "Date" => ParseDateMetric(metricName),
            _ => null,
        };

        if (metricEnum == null)
        {
            return null;
        }

        return [CreateMetricForProperty(propertyName, metricEnum)];
    }

    /// <summary>
    /// Parses a property name into (sourceProperty, metricType, metricName).
    /// Example: "PriceMean" → ("Price", "Number", "Mean")
    /// </summary>
    private static (
        string sourceProperty,
        string metricType,
        string metricName
    )? ParsePropertyNameConvention(string propertyName)
    {
        // Known metric suffixes for each type
        var numericSuffixes = new[] { "Mean", "Median", "Mode", "Max", "Min", "Count", "Sum" };
        var textSuffixes = new[] { "Count", "TopOccurrences" };
        var booleanSuffixes = new[]
        {
            "Count",
            "TotalTrue",
            "TotalFalse",
            "PercentageTrue",
            "PercentageFalse",
        };

        // Try to match against known suffixes
        foreach (var suffix in numericSuffixes)
        {
            if (propertyName.EndsWith(suffix, StringComparison.Ordinal))
            {
                var sourceProperty = propertyName.Substring(0, propertyName.Length - suffix.Length);
                if (!string.IsNullOrEmpty(sourceProperty))
                {
                    // Determine if it's Number or Integer based on property type
                    // For convention, we'll default to Number for numeric metrics
                    var metricType =
                        suffix == "Mean" ? "Number"
                        : IsIntegerMetric(suffix) ? "Integer"
                        : "Number";
                    return (sourceProperty, metricType, suffix);
                }
            }
        }

        foreach (var suffix in textSuffixes)
        {
            if (
                propertyName.EndsWith(suffix, StringComparison.Ordinal)
                && suffix != "Count"
                && suffix != "Type"
            )
            {
                var sourceProperty = propertyName.Substring(0, propertyName.Length - suffix.Length);
                if (!string.IsNullOrEmpty(sourceProperty))
                {
                    return (sourceProperty, "Text", suffix);
                }
            }
        }

        foreach (var suffix in booleanSuffixes)
        {
            if (propertyName.EndsWith(suffix, StringComparison.Ordinal))
            {
                var sourceProperty = propertyName.Substring(0, propertyName.Length - suffix.Length);
                if (!string.IsNullOrEmpty(sourceProperty))
                {
                    return (sourceProperty, "Boolean", suffix);
                }
            }
        }

        return null;
    }

    private static bool IsIntegerMetric(string suffix)
    {
        return suffix == "Count" || suffix == "Sum";
    }

    private static bool IsAggregateType(Type type)
    {
        // Check if it's one of the Aggregate.* types by checking assembly and type name patterns
        // The Aggregate types are in Weaviate.Client assembly
        var assemblyName = type.Assembly.GetName().Name;
        if (assemblyName != "Weaviate.Client")
        {
            return false;
        }

        // Check type name and namespace patterns
        var fullName = type.FullName;
        var typeName = type.Name;

        // Try various patterns
        if (
            typeName == "Number"
            || typeName == "Integer"
            || typeName == "Text"
            || typeName == "Boolean"
            || typeName == "Date"
        )
        {
            // Check if it's in the right namespace/declaring type structure
            if (fullName != null && fullName.Contains("Aggregate"))
            {
                return true;
            }

            var declaringType = type.DeclaringType;
            if (declaringType != null && declaringType.Name == "Aggregate")
            {
                return true;
            }
        }

        return false;
    }

    private static Aggregate.Metric CreateCombinedMetric(string propertyName, object[] metricEnums)
    {
        if (metricEnums.Length == 0)
        {
            throw new ArgumentException("At least one metric must be specified");
        }

        // Group by metric type (Number, Integer, Text, etc.)
        var enumType = metricEnums[0].GetType();
        var metricTypeName = enumType.Name;

        // Verify all metrics are of the same type
        if (metricEnums.Any(m => m.GetType().Name != metricTypeName))
        {
            throw new InvalidOperationException(
                "All metrics for a property must be of the same type (Number, Integer, Text, Boolean, or Date)"
            );
        }

        // Create a single metric call with all booleans set
        return metricTypeName switch
        {
            "Number" => CreateCombinedNumberMetric(propertyName, metricEnums),
            "Integer" => CreateCombinedIntegerMetric(propertyName, metricEnums),
            "Text" => CreateCombinedTextMetric(propertyName, metricEnums),
            "Boolean" => CreateCombinedBooleanMetric(propertyName, metricEnums),
            "Date" => CreateCombinedDateMetric(propertyName, metricEnums),
            _ => throw new NotSupportedException($"Unsupported metric type: {metricTypeName}"),
        };
    }

    /// <summary>
    /// Creates an aggregate metric from property name and metric enum values.
    /// Used by MetricsBuilder for type-safe metric construction.
    /// </summary>
    internal static Aggregate.Metric CreateMetricFromEnums(
        string propertyName,
        object[] metricEnums
    )
    {
        return CreateCombinedMetric(propertyName, metricEnums);
    }

    private static Aggregate.Metric CreateCombinedNumberMetric(
        string propertyName,
        object[] metricEnums
    )
    {
        bool mean = false,
            median = false,
            mode = false,
            maximum = false,
            minimum = false,
            count = false,
            sum = false;

        foreach (var metricEnum in metricEnums)
        {
            var enumName = Enum.GetName(metricEnum.GetType(), metricEnum);
            switch (enumName)
            {
                case "Mean":
                    mean = true;
                    break;
                case "Median":
                    median = true;
                    break;
                case "Mode":
                    mode = true;
                    break;
                case "Max":
                    maximum = true;
                    break;
                case "Min":
                    minimum = true;
                    break;
                case "Count":
                    count = true;
                    break;
                case "Sum":
                    sum = true;
                    break;
            }
        }

        return Metrics
            .ForProperty(propertyName)
            .Number(
                mean: mean,
                median: median,
                mode: mode,
                maximum: maximum,
                minimum: minimum,
                count: count,
                sum: sum
            );
    }

    private static Aggregate.Metric CreateCombinedIntegerMetric(
        string propertyName,
        object[] metricEnums
    )
    {
        bool mean = false,
            median = false,
            mode = false,
            maximum = false,
            minimum = false,
            count = false,
            sum = false;

        foreach (var metricEnum in metricEnums)
        {
            var enumName = Enum.GetName(metricEnum.GetType(), metricEnum);
            switch (enumName)
            {
                case "Mean":
                    mean = true;
                    break;
                case "Median":
                    median = true;
                    break;
                case "Mode":
                    mode = true;
                    break;
                case "Max":
                    maximum = true;
                    break;
                case "Min":
                    minimum = true;
                    break;
                case "Count":
                    count = true;
                    break;
                case "Sum":
                    sum = true;
                    break;
            }
        }

        return Metrics
            .ForProperty(propertyName)
            .Integer(
                mean: mean,
                median: median,
                mode: mode,
                maximum: maximum,
                minimum: minimum,
                count: count,
                sum: sum
            );
    }

    private static Aggregate.Metric CreateCombinedTextMetric(
        string propertyName,
        object[] metricEnums
    )
    {
        bool count = false,
            topOccurrencesCount = false;

        foreach (var metricEnum in metricEnums)
        {
            var enumName = Enum.GetName(metricEnum.GetType(), metricEnum);
            switch (enumName)
            {
                case "Count":
                    count = true;
                    break;
                case "TopOccurrences":
                    topOccurrencesCount = true;
                    break;
            }
        }

        return Metrics
            .ForProperty(propertyName)
            .Text(count: count, topOccurrencesCount: topOccurrencesCount);
    }

    private static Aggregate.Metric CreateCombinedBooleanMetric(
        string propertyName,
        object[] metricEnums
    )
    {
        bool count = false,
            totalTrue = false,
            totalFalse = false,
            percentageTrue = false,
            percentageFalse = false;

        foreach (var metricEnum in metricEnums)
        {
            var enumName = Enum.GetName(metricEnum.GetType(), metricEnum);
            switch (enumName)
            {
                case "Count":
                    count = true;
                    break;
                case "TotalTrue":
                    totalTrue = true;
                    break;
                case "TotalFalse":
                    totalFalse = true;
                    break;
                case "PercentageTrue":
                    percentageTrue = true;
                    break;
                case "PercentageFalse":
                    percentageFalse = true;
                    break;
            }
        }

        return Metrics
            .ForProperty(propertyName)
            .Boolean(
                count: count,
                totalTrue: totalTrue,
                totalFalse: totalFalse,
                percentageTrue: percentageTrue,
                percentageFalse: percentageFalse
            );
    }

    private static Aggregate.Metric CreateCombinedDateMetric(
        string propertyName,
        object[] metricEnums
    )
    {
        bool count = false,
            minimum = false,
            maximum = false,
            median = false,
            mode = false;

        foreach (var metricEnum in metricEnums)
        {
            var enumName = Enum.GetName(metricEnum.GetType(), metricEnum);
            switch (enumName)
            {
                case "Count":
                    count = true;
                    break;
                case "Min":
                    minimum = true;
                    break;
                case "Max":
                    maximum = true;
                    break;
                case "Median":
                    median = true;
                    break;
                case "Mode":
                    mode = true;
                    break;
            }
        }

        return Metrics
            .ForProperty(propertyName)
            .Date(count: count, minimum: minimum, maximum: maximum, median: median, mode: mode);
    }

    private static Aggregate.Metric CreateMetricForProperty(string propertyName, object metricEnum)
    {
        var enumType = metricEnum.GetType();
        var enumName = Enum.GetName(enumType, metricEnum);
        if (enumName == null)
        {
            throw new InvalidOperationException($"Invalid metric enum value: {metricEnum}");
        }

        // Determine metric type from enum type
        var metricTypeName = enumType.Name; // Number, Integer, Text, Boolean, Date

        return metricTypeName switch
        {
            "Number" => CreateNumberMetric(propertyName, enumName),
            "Integer" => CreateIntegerMetric(propertyName, enumName),
            "Text" => CreateTextMetric(propertyName, enumName),
            "Boolean" => CreateBooleanMetric(propertyName, enumName),
            "Date" => CreateDateMetric(propertyName, enumName),
            _ => throw new NotSupportedException($"Unsupported metric type: {metricTypeName}"),
        };
    }

    private static Aggregate.Metric CreateNumberMetric(string propertyName, string metricName)
    {
        return metricName switch
        {
            "Mean" => Metrics.ForProperty(propertyName).Number(mean: true),
            "Median" => Metrics.ForProperty(propertyName).Number(median: true),
            "Mode" => Metrics.ForProperty(propertyName).Number(mode: true),
            "Max" => Metrics.ForProperty(propertyName).Number(maximum: true),
            "Min" => Metrics.ForProperty(propertyName).Number(minimum: true),
            "Count" => Metrics.ForProperty(propertyName).Number(count: true),
            "Sum" => Metrics.ForProperty(propertyName).Number(sum: true),
            _ => throw new ArgumentException($"Unknown Number metric: {metricName}"),
        };
    }

    private static Aggregate.Metric CreateIntegerMetric(string propertyName, string metricName)
    {
        return metricName switch
        {
            "Mean" => Metrics.ForProperty(propertyName).Integer(mean: true),
            "Median" => Metrics.ForProperty(propertyName).Integer(median: true),
            "Mode" => Metrics.ForProperty(propertyName).Integer(mode: true),
            "Max" => Metrics.ForProperty(propertyName).Integer(maximum: true),
            "Min" => Metrics.ForProperty(propertyName).Integer(minimum: true),
            "Count" => Metrics.ForProperty(propertyName).Integer(count: true),
            "Sum" => Metrics.ForProperty(propertyName).Integer(sum: true),
            _ => throw new ArgumentException($"Unknown Integer metric: {metricName}"),
        };
    }

    private static Aggregate.Metric CreateTextMetric(string propertyName, string metricName)
    {
        return metricName switch
        {
            "Count" => Metrics.ForProperty(propertyName).Text(count: true),
            "TopOccurrences" => Metrics.ForProperty(propertyName).Text(topOccurrencesCount: true),
            _ => throw new ArgumentException($"Unknown Text metric: {metricName}"),
        };
    }

    private static Aggregate.Metric CreateBooleanMetric(string propertyName, string metricName)
    {
        return metricName switch
        {
            "Count" => Metrics.ForProperty(propertyName).Boolean(count: true),
            "TotalTrue" => Metrics.ForProperty(propertyName).Boolean(totalTrue: true),
            "TotalFalse" => Metrics.ForProperty(propertyName).Boolean(totalFalse: true),
            "PercentageTrue" => Metrics.ForProperty(propertyName).Boolean(percentageTrue: true),
            "PercentageFalse" => Metrics.ForProperty(propertyName).Boolean(percentageFalse: true),
            _ => throw new ArgumentException($"Unknown Boolean metric: {metricName}"),
        };
    }

    private static Aggregate.Metric CreateDateMetric(string propertyName, string metricName)
    {
        return metricName switch
        {
            "Count" => Metrics.ForProperty(propertyName).Date(count: true),
            "Min" => Metrics.ForProperty(propertyName).Date(minimum: true),
            "Max" => Metrics.ForProperty(propertyName).Date(maximum: true),
            "Median" => Metrics.ForProperty(propertyName).Date(median: true),
            "Mode" => Metrics.ForProperty(propertyName).Date(mode: true),
            _ => throw new ArgumentException($"Unknown Date metric: {metricName}"),
        };
    }

    private static Metric.Number? ParseNumberMetric(string metricName)
    {
        return metricName switch
        {
            "Mean" => Metric.Number.Mean,
            "Median" => Metric.Number.Median,
            "Mode" => Metric.Number.Mode,
            "Max" => Metric.Number.Max,
            "Min" => Metric.Number.Min,
            "Count" => Metric.Number.Count,
            "Sum" => Metric.Number.Sum,
            _ => null,
        };
    }

    private static Metric.Integer? ParseIntegerMetric(string metricName)
    {
        return metricName switch
        {
            "Mean" => Metric.Integer.Mean,
            "Median" => Metric.Integer.Median,
            "Mode" => Metric.Integer.Mode,
            "Max" => Metric.Integer.Max,
            "Min" => Metric.Integer.Min,
            "Count" => Metric.Integer.Count,
            "Sum" => Metric.Integer.Sum,
            _ => null,
        };
    }

    private static Metric.Text? ParseTextMetric(string metricName)
    {
        return metricName switch
        {
            "Count" => Metric.Text.Count,
            "TopOccurrences" => Metric.Text.TopOccurrences,
            _ => null,
        };
    }

    private static Metric.Boolean? ParseBooleanMetric(string metricName)
    {
        return metricName switch
        {
            "Count" => Metric.Boolean.Count,
            "TotalTrue" => Metric.Boolean.TotalTrue,
            "TotalFalse" => Metric.Boolean.TotalFalse,
            "PercentageTrue" => Metric.Boolean.PercentageTrue,
            "PercentageFalse" => Metric.Boolean.PercentageFalse,
            _ => null,
        };
    }

    private static Metric.Date? ParseDateMetric(string metricName)
    {
        return metricName switch
        {
            "Count" => Metric.Date.Count,
            "Min" => Metric.Date.Min,
            "Max" => Metric.Date.Max,
            "Median" => Metric.Date.Median,
            "Mode" => Metric.Date.Mode,
            _ => null,
        };
    }
}
