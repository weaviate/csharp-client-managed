using System.Linq.Expressions;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;

namespace Weaviate.Client.Managed.Query;

internal enum WeaviateSearchMode
{
    Fetch,
    NearText,
    NearVector,
    Hybrid,
    BM25,
    NearObject,
    NearMedia,
}

internal record WeaviateQueryConfig
{
    public WeaviateSearchMode SearchMode { get; init; } = WeaviateSearchMode.Fetch;

    // NearText
    public string? NearTextQuery { get; init; }
    public LambdaExpression? VectorProperty { get; init; }

    // NearVector
    public float[]? NearVector { get; init; }

    // NearObject
    public Guid? NearObjectId { get; init; }

    // Hybrid
    public string? HybridQuery { get; init; }
    public float? HybridAlpha { get; init; }
    public HybridFusion? FusionType { get; init; }
    public VectorSearchInput? HybridVectorSearch { get; init; }

    // BM25
    public string? Bm25Query { get; init; }
    public string[]? Bm25SearchFields { get; init; }
    public BM25Operator? Bm25Operator { get; init; }

    // NearMedia
    public NearMediaInput.FactoryFn? NearMedia { get; init; }

    // Shared search params
    public float? Distance { get; init; }
    public float? Certainty { get; init; }
    public float? MaxVectorDistance { get; init; }
    public TargetVectors? TargetVectors { get; init; }

    // Result control
    public string[] IncludeVectors { get; init; } = [];
    public string[] IncludeReferences { get; init; } = [];
    public MetadataQuery? Metadata { get; init; }
    public Rerank? Rerank { get; init; }
}
