using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Query;

/// <summary>
/// Overlay config for NearText queries returned by <c>ConfigureNearText</c> on projection classes.
/// Only non-null properties override the values already set on the query.
/// </summary>
public record NearTextConfig
{
    /// <summary>Minimum certainty threshold (0–1).</summary>
    public float? Certainty { get; init; }

    /// <summary>Maximum distance (0–2 for cosine; higher is more lenient).</summary>
    public float? Distance { get; init; }
}

/// <summary>
/// Overlay config for NearVector queries returned by <c>ConfigureNearVector</c> on projection classes.
/// </summary>
public record NearVectorConfig
{
    /// <summary>Minimum certainty threshold (0–1).</summary>
    public float? Certainty { get; init; }

    /// <summary>Maximum distance.</summary>
    public float? Distance { get; init; }
}

/// <summary>
/// Overlay config for Hybrid queries returned by <c>ConfigureHybrid</c> on projection classes.
/// </summary>
public record HybridConfig
{
    /// <summary>
    /// Alpha controls the blend between BM25 (0.0) and vector search (1.0).
    /// Defaults to 0.75 in Weaviate if not set.
    /// </summary>
    public float? Alpha { get; init; }

    /// <summary>The fusion algorithm to use when merging BM25 and vector scores.</summary>
    public HybridFusion? FusionType { get; init; }

    /// <summary>Maximum vector distance for the vector component of the hybrid search.</summary>
    public float? MaxVectorDistance { get; init; }
}

/// <summary>
/// Overlay config for NearObject queries returned by <c>ConfigureNearObject</c> on projection classes.
/// </summary>
public record NearObjectConfig
{
    /// <summary>Minimum certainty threshold (0–1).</summary>
    public float? Certainty { get; init; }

    /// <summary>Maximum distance.</summary>
    public float? Distance { get; init; }
}

/// <summary>
/// Overlay config for NearMedia queries returned by <c>ConfigureNearMedia</c> on projection classes.
/// </summary>
public record NearMediaConfig
{
    /// <summary>Minimum certainty threshold (0–1).</summary>
    public float? Certainty { get; init; }

    /// <summary>Maximum distance.</summary>
    public float? Distance { get; init; }
}
