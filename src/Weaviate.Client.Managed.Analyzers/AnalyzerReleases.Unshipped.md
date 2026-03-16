### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
WVMTA001 | Usage | Error | Only [Vector] properties allowed in VectorTargets
WEAVIATE001 | Usage | Error | Auto[] array usage in NearX searches (prefer explicit vector specification)
WEAVIATE003 | Usage | Error | Metrics attribute type mismatch (must match property Aggregate or scalar type)
WEAVIATE004 | Usage | Error | Single metric required for scalar property
WEAVIATE005 | Usage | Error | Invalid scalar type for metric
WEAVIATE006 | Naming | Error | [Metrics] attribute requires [QueryAggregate<T>] on class
WEAVIATE007 | Naming | Error | Property name in [Metrics] does not exist in entity
WEAVIATE008 | Naming | Warning | Property name does not follow suffix convention (PropertyName + MetricSuffix)
WEAVIATE009 | Usage | Error | Hybrid search requires at least one search parameter (query or vector)
WEAVIATE010 | Usage | Error | Property type does not match metric type in WithMetrics calls
