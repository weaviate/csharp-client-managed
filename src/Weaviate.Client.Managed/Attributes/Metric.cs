using System;

namespace Weaviate.Client.Managed.Attributes
{
    /// <summary>
    /// Container for aggregate metric enums. Each nested type corresponds to a Weaviate aggregate type
    /// (e.g., Aggregate.Number, Aggregate.Integer, etc.) and specifies which metrics to compute.
    /// Use multiple values with params syntax: [Metrics(Metric.Number.Mean, Metric.Number.Sum)]
    /// </summary>
    public static class Metric
    {
        /// <summary>
        /// Metrics for numeric (float/double/decimal) aggregate properties mapped from Aggregate.Number.
        /// </summary>
        public enum Number
        {
            None = 0,
            Mean = 1,
            Sum = 2,
            Count = 4,
            Min = 8,
            Max = 16,
            Median = 32,
            Mode = 64,
        }

        /// <summary>
        /// Metrics for integer aggregate properties mapped from Aggregate.Integer.
        /// </summary>
        public enum Integer
        {
            None = 0,
            Mean = 1,
            Sum = 2,
            Count = 4,
            Min = 8,
            Max = 16,
            Median = 32,
            Mode = 64,
        }

        /// <summary>
        /// Metrics for text aggregate properties mapped from Aggregate.Text.
        /// </summary>
        public enum Text
        {
            None = 0,
            Count = 1,
            TopOccurrences = 2,
        }

        /// <summary>
        /// Metrics for boolean aggregate properties mapped from Aggregate.Boolean.
        /// </summary>
        public enum Boolean
        {
            None = 0,
            Count = 1,
            TotalTrue = 2,
            TotalFalse = 4,
            PercentageTrue = 8,
            PercentageFalse = 16,
        }

        /// <summary>
        /// Metrics for date aggregate properties mapped from Aggregate.Date.
        /// </summary>
        public enum Date
        {
            None = 0,
            Count = 1,
            Min = 2,
            Max = 4,
            Median = 8,
            Mode = 16,
        }
    }
}
