using Weaviate.Client.Models.Typed;
using Xunit;

namespace Weaviate.Client.Managed.Tests.Mapping;

public class ProjectionMapperTests
{
    #region MapToProjection Tests

    [Fact]
    public void MapToProjection_MapsPropertiesByConvention()
    {
        // Arrange
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["title"] = "Test Title", ["price"] = 19.99 }
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, SimpleProjection>(wo);

        // Assert
        Assert.Equal("Test Title", result.Title);
        Assert.Equal(19.99, result.Price);
    }

    [Fact]
    public void MapToProjection_MapsPropertiesWithMapFrom()
    {
        // Arrange
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["wordCount"] = 42 }
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, ProjectionWithMapFrom>(wo);

        // Assert
        Assert.Equal(42, result.Words);
    }

    [Fact]
    public void MapToProjection_InjectsWeaviateUUID()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["title"] = "Test" },
            uuid: expectedId
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, ProjectionWithId>(wo);

        // Assert
        Assert.Equal(expectedId, result.Id);
    }

    [Fact]
    public void MapToProjection_InjectsNullableWeaviateUUID()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["title"] = "Test" },
            uuid: expectedId
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, ProjectionWithNullableId>(wo);

        // Assert
        Assert.Equal(expectedId, result.Id);
    }

    [Fact]
    public void MapToProjection_InjectsMetadata()
    {
        // Arrange
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["title"] = "Test" },
            metadata: new Metadata { Score = 0.95 }
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, ProjectionWithMetadata>(wo);

        // Assert
        Assert.Equal(0.95, result.Score);
    }

    [Fact]
    public void MapToProjection_InjectsMetadataWithExplicitFieldName()
    {
        // Arrange
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["title"] = "Test" },
            metadata: new Metadata { Distance = 0.15 }
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, ProjectionWithNamedMetadata>(
            wo
        );

        // Assert
        Assert.Equal(0.15, result.Dist);
    }

    [Fact]
    public void MapToProjection_InjectsVectors()
    {
        // Arrange
        var vectors = new Vectors { { "embedding", new float[] { 1.0f, 2.0f, 3.0f } } };
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["title"] = "Test" },
            vectors: vectors
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, ProjectionWithVector>(wo);

        // Assert
        Assert.NotNull(result.Embedding);
        Assert.Equal(new float[] { 1.0f, 2.0f, 3.0f }, result.Embedding);
    }

    [Fact]
    public void MapToProjection_InjectsVectorsWithExplicitName()
    {
        // Arrange
        var vectors = new Vectors { { "titleEmbedding", new float[] { 4.0f, 5.0f } } };
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["title"] = "Test" },
            vectors: vectors
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, ProjectionWithNamedVector>(wo);

        // Assert
        Assert.NotNull(result.MyVector);
        Assert.Equal(new float[] { 4.0f, 5.0f }, result.MyVector);
    }

    [Fact]
    public void MapToProjection_HandlesEmptyProperties()
    {
        // Arrange
        var wo = CreateWeaviateObject<SourceEntity>(new Dictionary<string, object?>());

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, SimpleProjection>(wo);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("", result.Title);
        Assert.Equal(0.0, result.Price);
    }

    [Fact]
    public void MapToProjection_HandlesMissingProperties()
    {
        // Arrange - only has title, not price
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["title"] = "Test" }
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, SimpleProjection>(wo);

        // Assert
        Assert.Equal("Test", result.Title);
        Assert.Equal(0.0, result.Price);
    }

    [Fact]
    public void MapToProjection_CombinesAllFeatures()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var vectors = new Vectors { { "embedding", new float[] { 1.0f, 2.0f } } };
        var wo = CreateWeaviateObject<SourceEntity>(
            new Dictionary<string, object?> { ["title"] = "Combined Test", ["wordCount"] = 100 },
            uuid: expectedId,
            metadata: new Metadata { Score = 0.88 },
            vectors: vectors
        );

        // Act
        var result = ProjectionMapper.MapToProjection<SourceEntity, FullProjection>(wo);

        // Assert
        Assert.Equal(expectedId, result.Id);
        Assert.Equal("Combined Test", result.Title);
        Assert.Equal(100, result.Words);
        Assert.Equal(0.88, result.Score);
        Assert.NotNull(result.Embedding);
        Assert.Equal(new float[] { 1.0f, 2.0f }, result.Embedding);
    }

    #endregion

    #region GetSourcePropertyNames Tests

    [Fact]
    public void GetSourcePropertyNames_ReturnsOnlyDataProperties()
    {
        // Act
        var names = ProjectionMapper.GetSourcePropertyNames<SimpleProjection>();

        // Assert
        Assert.Contains("title", names);
        Assert.Contains("price", names);
        Assert.Equal(2, names.Count);
    }

    [Fact]
    public void GetSourcePropertyNames_ExcludesMetadataVectorAndIdProperties()
    {
        // Act
        var names = ProjectionMapper.GetSourcePropertyNames<FullProjection>();

        // Assert
        Assert.Contains("title", names);
        Assert.Contains("wordCount", names); // MapFrom resolves to source name
        Assert.DoesNotContain("id", names);
        Assert.DoesNotContain("score", names);
        Assert.DoesNotContain("embedding", names);
    }

    [Fact]
    public void GetSourcePropertyNames_ResolvesMapFromToSourceName()
    {
        // Act
        var names = ProjectionMapper.GetSourcePropertyNames<ProjectionWithMapFrom>();

        // Assert - should use source name "wordCount", not "words"
        Assert.Contains("wordCount", names);
    }

    #endregion

    #region GetVectorNames Tests

    [Fact]
    public void GetVectorNames_ReturnsVectorNames()
    {
        // Act
        var names = ProjectionMapper.GetVectorNames<ProjectionWithVector>();

        // Assert
        Assert.Single(names);
        Assert.Equal("embedding", names[0]);
    }

    [Fact]
    public void GetVectorNames_ReturnsExplicitVectorName()
    {
        // Act
        var names = ProjectionMapper.GetVectorNames<ProjectionWithNamedVector>();

        // Assert
        Assert.Single(names);
        Assert.Equal("titleEmbedding", names[0]);
    }

    [Fact]
    public void GetVectorNames_ReturnsEmptyWhenNoVectors()
    {
        // Act
        var names = ProjectionMapper.GetVectorNames<SimpleProjection>();

        // Assert
        Assert.Empty(names);
    }

    #endregion

    #region GetMetadataOptions Tests

    [Fact]
    public void GetMetadataOptions_ReturnsCorrectFlags()
    {
        // Act
        var options = ProjectionMapper.GetMetadataOptions<ProjectionWithMetadata>();

        // Assert
        Assert.Equal(MetadataOptions.Score, options);
    }

    [Fact]
    public void GetMetadataOptions_ReturnsCorrectFlagsForExplicitFieldName()
    {
        // Act
        var options = ProjectionMapper.GetMetadataOptions<ProjectionWithNamedMetadata>();

        // Assert
        Assert.Equal(MetadataOptions.Distance, options);
    }

    [Fact]
    public void GetMetadataOptions_ReturnsNoneWhenNoMetadata()
    {
        // Act
        var options = ProjectionMapper.GetMetadataOptions<SimpleProjection>();

        // Assert
        Assert.Equal(MetadataOptions.None, options);
    }

    #endregion

    #region Helpers

    private static WeaviateObject<T> CreateWeaviateObject<T>(
        IDictionary<string, object?> properties,
        Guid? uuid = null,
        Metadata? metadata = null,
        Vectors? vectors = null
    )
        where T : class, new()
    {
        var untyped = new WeaviateObject
        {
            UUID = uuid,
            Properties = properties,
            Metadata = metadata ?? new Metadata(),
            Vectors = vectors ?? new Vectors(),
        };
        return WeaviateObject<T>.FromUntyped(untyped);
    }

    #endregion

    #region Test Types

    [WeaviateCollection("SourceEntity")]
    public class SourceEntity
    {
        [WeaviateUUID]
        public Guid Id { get; set; }

        [Property]
        public string Title { get; set; } = "";

        [Property]
        public double Price { get; set; }

        [Property]
        public int WordCount { get; set; }
    }

    public class SimpleProjection
    {
        public string Title { get; set; } = "";
        public double Price { get; set; }
    }

    public class ProjectionWithMapFrom
    {
        [MapFrom("WordCount")]
        public int Words { get; set; }
    }

    public class ProjectionWithId
    {
        [WeaviateUUID]
        public Guid Id { get; set; }

        public string Title { get; set; } = "";
    }

    public class ProjectionWithNullableId
    {
        [WeaviateUUID]
        public Guid? Id { get; set; }
    }

    public class ProjectionWithMetadata
    {
        [MetadataProperty]
        public double? Score { get; set; }
    }

    public class ProjectionWithNamedMetadata
    {
        [MetadataProperty(MetadataField = "Distance")]
        public double? Dist { get; set; }
    }

    public class ProjectionWithVector
    {
        [Vector]
        public float[]? Embedding { get; set; }
    }

    public class ProjectionWithNamedVector
    {
        [Vector(VectorName = "titleEmbedding")]
        public float[]? MyVector { get; set; }
    }

    [QueryProjection<SourceEntity>]
    public class FullProjection
    {
        [WeaviateUUID]
        public Guid Id { get; set; }

        public string Title { get; set; } = "";

        [MapFrom("WordCount")]
        public int Words { get; set; }

        [MetadataProperty]
        public double? Score { get; set; }

        [Vector]
        public float[]? Embedding { get; set; }
    }

    #endregion
}
