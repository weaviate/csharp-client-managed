using Weaviate.Client.Managed.Tests.Mocks;
using Xunit;
using V1 = Weaviate.Client.Grpc.Protobuf.V1;

namespace Weaviate.Client.Managed.Tests.Query;

public class TestModel
{
    public string Title { get; set; } = string.Empty;
    public int WordCount { get; set; }
    public float Price { get; set; }
    public GeoCoordinate? Location { get; set; }
    public float[]? Embedding { get; set; }
    public float[]? Embedding2 { get; set; }
}

public class AuthorModel
{
    [WeaviateUUID]
    public Guid UUID { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ArticleWithRef
{
    public string Title { get; set; } = string.Empty;

    [Reference]
    public AuthorModel? Author { get; set; }
}

public class QueryBuilderTests : IAsyncLifetime
{
    private const string CollectionName = "TestCollection";
    private Func<V1.SearchRequest?> _getRequest = null!;
    private CollectionClient _collection = null!;

    public ValueTask InitializeAsync()
    {
        var (client, getRequest) = MockGrpcClient.CreateWithSearchCapture();
        _getRequest = getRequest;
        _collection = client.Collections.Use(CollectionName);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    private CollectionMapperQueryClient<TestModel> Query() =>
        Weaviate.Client.Managed.Extensions.CollectionClientExtensions.Query<TestModel>(_collection);

    #region Fetch (default mode)

    [Fact]
    public async Task Fetch_Default_ProducesValidRequest()
    {
        await Query().Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(CollectionName, request.Collection);
        Assert.Null(request.NearText);
        Assert.Null(request.NearVector);
        Assert.Null(request.HybridSearch);
        Assert.Null(request.Bm25Search);
        Assert.Null(request.NearObject);
    }

    [Fact]
    public async Task Fetch_WithLimit_SetsLimit()
    {
        await Query().Limit(25).Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(25u, request.Limit);
    }

    [Fact]
    public async Task Fetch_WithOffset_SetsOffset()
    {
        await Query().Limit(10).Offset(20).Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(10u, request.Limit);
        Assert.Equal(20u, request.Offset);
    }

    [Fact]
    public async Task Fetch_WithAfter_SetsCursor()
    {
        var cursor = Guid.Parse("11111111-2222-3333-4444-555555555555");
        await Query().After(cursor).Limit(10).Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(cursor.ToString(), request.After);
    }

    [Fact]
    public async Task Fetch_WithRerank_SetsRerank()
    {
        await _collection
            .Query<TestModel>()
            .Rerank(new Rerank { Property = "title", Query = "important" })
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Rerank);
        Assert.Equal("title", request.Rerank.Property);
        Assert.Equal("important", request.Rerank.Query);
    }

    [Fact]
    public async Task Fetch_WithSort_SetsSortBy()
    {
        await _collection
            .Query<TestModel>()
            .Sort<int>(t => t.WordCount, descending: true)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Single(request.SortBy);
        Assert.Equal("wordCount", request.SortBy[0].Path[0]);
        Assert.False(request.SortBy[0].Ascending);
    }

    [Fact]
    public async Task Fetch_WithOrderByThenBy_SetsMultipleSortCriteria()
    {
        await _collection
            .Query<TestModel>()
            .OrderBy<string>(t => t.Title)
            .ThenByDescending<int>(t => t.WordCount)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(2, request.SortBy.Count);
        Assert.Equal("title", request.SortBy[0].Path[0]);
        Assert.True(request.SortBy[0].Ascending);
        Assert.Equal("wordCount", request.SortBy[1].Path[0]);
        Assert.False(request.SortBy[1].Ascending);
    }

    [Fact]
    public async Task Fetch_OrderByDescending_SetsSingleDescendingSort()
    {
        await _collection
            .Query<TestModel>()
            .OrderByDescending<int>(t => t.WordCount)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Single(request.SortBy);
        Assert.Equal("wordCount", request.SortBy[0].Path[0]);
        Assert.False(request.SortBy[0].Ascending);
    }

    [Fact]
    public async Task Fetch_ThenByWithoutOrderBy_AppendsSortCriteria()
    {
        await _collection
            .Query<TestModel>()
            .ThenBy<string>(t => t.Title)
            .ThenByDescending<int>(t => t.WordCount)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(2, request.SortBy.Count);
        Assert.Equal("title", request.SortBy[0].Path[0]);
        Assert.True(request.SortBy[0].Ascending);
        Assert.Equal("wordCount", request.SortBy[1].Path[0]);
        Assert.False(request.SortBy[1].Ascending);
    }

    [Fact]
    public async Task Fetch_SecondOrderBy_ReplacesAllPreviousSortCriteria()
    {
        await _collection
            .Query<TestModel>()
            .OrderBy<string>(t => t.Title)
            .ThenBy<int>(t => t.WordCount)
            .OrderByDescending<string>(t => t.Title) // replaces both
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Single(request.SortBy);
        Assert.Equal("title", request.SortBy[0].Path[0]);
        Assert.False(request.SortBy[0].Ascending);
    }

    #endregion

    #region NearText

    [Fact]
    public async Task NearText_Simple_ProducesValidRequest()
    {
        await Query().NearText("hello world").Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearText);
        Assert.Contains("hello world", request.NearText.Query);
    }

    [Fact]
    public async Task NearText_WithCertaintyAndDistance_SetsValues()
    {
        await _collection
            .Query<TestModel>()
            .NearText("hello", certainty: 0.8f, distance: 0.2f)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearText);
        Assert.Equal(0.8, request.NearText.Certainty, precision: 5);
        Assert.Equal(0.2, request.NearText.Distance, precision: 5);
    }

    [Fact]
    public async Task NearText_WithTargetVector_SetsTargets()
    {
        await _collection
            .Query<TestModel>()
            .NearText("hello", vector: t => t.Embedding!)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearText);
        Assert.NotNull(request.NearText.Targets);
        Assert.Contains("embedding", request.NearText.Targets.TargetVectors);
    }

    [Fact]
    public async Task NearText_WithOffsetAndAutoLimit_PassesValues()
    {
        await _collection
            .Query<TestModel>()
            .NearText("hello")
            .Offset(5)
            .AutoLimit(3)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(5u, request.Offset);
        Assert.Equal(3u, request.Autocut);
    }

    [Fact]
    public async Task NearText_WithRerank_PassesRerank()
    {
        await _collection
            .Query<TestModel>()
            .NearText("hello")
            .Rerank(new Rerank { Property = "title" })
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Rerank);
        Assert.Equal("title", request.Rerank.Property);
    }

    #endregion

    #region NearVector

    [Fact]
    public async Task NearVector_Simple_ProducesValidRequest()
    {
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        await Query().NearVector(vector).Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearVector);
    }

    [Fact]
    public async Task NearVector_WithCertaintyAndOffset_SetsValues()
    {
        var vector = new float[] { 0.1f, 0.2f, 0.3f };
        await _collection
            .Query<TestModel>()
            .NearVector(vector, certainty: 0.9f)
            .Offset(10)
            .AutoLimit(2)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearVector);
        Assert.Equal(10u, request.Offset);
        Assert.Equal(2u, request.Autocut);
    }

    #endregion

    #region Hybrid

    [Fact]
    public async Task Hybrid_Simple_ProducesValidRequest()
    {
        await _collection
            .Query<TestModel>()
            .Hybrid("search query", alpha: 0.7f)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.HybridSearch);
        Assert.Equal("search query", request.HybridSearch.Query);
        Assert.Equal(0.7f, request.HybridSearch.Alpha, precision: 5);
    }

    [Fact]
    public async Task Hybrid_WithFusionType_SetsFusionType()
    {
        await _collection
            .Query<TestModel>()
            .Hybrid("search", fusionType: HybridFusion.RelativeScore)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.HybridSearch);
        Assert.Equal(V1.Hybrid.Types.FusionType.RelativeScore, request.HybridSearch.FusionType);
    }

    [Fact]
    public async Task Hybrid_WithMaxVectorDistance_SetsThreshold()
    {
        await _collection
            .Query<TestModel>()
            .Hybrid("search", maxVectorDistance: 0.5f)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.HybridSearch);
        Assert.Equal(0.5f, request.HybridSearch.VectorDistance, precision: 5);
    }

    [Fact]
    public async Task Hybrid_WithOffsetAndAutoLimit_PassesValues()
    {
        await _collection
            .Query<TestModel>()
            .Hybrid("search")
            .Offset(15)
            .AutoLimit(4)
            .Limit(20)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(15u, request.Offset);
        Assert.Equal(4u, request.Autocut);
        Assert.Equal(20u, request.Limit);
    }

    #endregion

    #region BM25

    [Fact]
    public async Task BM25_Simple_ProducesValidRequest()
    {
        await Query().BM25("mouse").Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Bm25Search);
        Assert.Equal("mouse", request.Bm25Search.Query);
    }

    [Fact]
    public async Task BM25_WithSearchFields_SetsProperties()
    {
        await _collection
            .Query<TestModel>()
            .BM25("mouse", searchFields: [t => t.Title])
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Bm25Search);
        Assert.Equal("mouse", request.Bm25Search.Query);
        Assert.Contains("title", request.Bm25Search.Properties);
    }

    [Fact]
    public async Task BM25_WithOffsetAndAutoLimit_PassesValues()
    {
        await _collection
            .Query<TestModel>()
            .BM25("mouse")
            .Offset(5)
            .AutoLimit(2)
            .Limit(10)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(5u, request.Offset);
        Assert.Equal(2u, request.Autocut);
        Assert.Equal(10u, request.Limit);
    }

    [Fact]
    public async Task BM25_WithAfter_SetsCursor()
    {
        var cursor = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await Query().BM25("mouse").After(cursor).Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(cursor.ToString(), request.After);
    }

    [Fact]
    public async Task BM25_WithRerank_SetsRerank()
    {
        await _collection
            .Query<TestModel>()
            .BM25("mouse")
            .Rerank(new Rerank { Property = "title" })
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Rerank);
        Assert.Equal("title", request.Rerank.Property);
    }

    #endregion

    #region NearObject

    [Fact]
    public async Task NearObject_Simple_ProducesValidRequest()
    {
        var objectId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        await Query().NearObject(objectId).Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearObject);
        Assert.Equal(objectId.ToString(), request.NearObject.Id);
    }

    [Fact]
    public async Task NearObject_WithCertaintyAndDistance_SetsValues()
    {
        var objectId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        await _collection
            .Query<TestModel>()
            .NearObject(objectId, certainty: 0.85f, distance: 0.15f)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearObject);
        Assert.Equal(0.85, request.NearObject.Certainty, precision: 5);
        Assert.Equal(0.15, request.NearObject.Distance, precision: 5);
    }

    [Fact]
    public async Task NearObject_WithTargetVector_SetsTargets()
    {
        var objectId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        await _collection
            .Query<TestModel>()
            .NearObject(objectId, vector: t => t.Embedding!)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearObject);
        Assert.NotNull(request.NearObject.Targets);
        Assert.Contains("embedding", request.NearObject.Targets.TargetVectors);
    }

    [Fact]
    public async Task NearObject_WithOffsetAndAutoLimit_PassesValues()
    {
        var objectId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        await _collection
            .Query<TestModel>()
            .NearObject(objectId)
            .Offset(3)
            .AutoLimit(5)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Equal(3u, request.Offset);
        Assert.Equal(5u, request.Autocut);
    }

    #endregion

    #region NearMedia

    [Fact]
    public async Task NearMedia_Image_ProducesValidRequest()
    {
        var imageBytes = new byte[] { 1, 2, 3, 4, 5 };
        await _collection
            .Query<TestModel>()
            .NearMedia(m => m.Image(imageBytes).Build())
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearImage);
        Assert.Equal(Convert.ToBase64String(imageBytes), request.NearImage.Image);
    }

    #endregion

    #region Filter (Where)

    [Fact]
    public async Task Where_SingleFilter_ProducesValidRequest()
    {
        await _collection
            .Query<TestModel>()
            .Where(t => t.WordCount > 100)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Filters);
    }

    [Fact]
    public async Task Where_CombinedWithSearch_ProducesValidRequest()
    {
        await _collection
            .Query<TestModel>()
            .Where(t => t.Price > 10)
            .BM25("mouse")
            .Offset(5)
            .Limit(10)
            .Rerank(new Rerank { Property = "title" })
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Filters);
        Assert.NotNull(request.Bm25Search);
        Assert.Equal("mouse", request.Bm25Search.Query);
        Assert.Equal(5u, request.Offset);
        Assert.Equal(10u, request.Limit);
        Assert.NotNull(request.Rerank);
    }

    #endregion

    #region Chaining

    [Fact]
    public async Task Chaining_MultipleControlMethods_AllPassedCorrectly()
    {
        var cursor = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await _collection
            .Query<TestModel>()
            .BM25("keyboard")
            .Limit(20)
            .Offset(10)
            .AutoLimit(3)
            .After(cursor)
            .Rerank(new Rerank { Property = "title", Query = "best" })
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Bm25Search);
        Assert.Equal("keyboard", request.Bm25Search.Query);
        Assert.Equal(20u, request.Limit);
        Assert.Equal(10u, request.Offset);
        Assert.Equal(3u, request.Autocut);
        Assert.Equal(cursor.ToString(), request.After);
        Assert.NotNull(request.Rerank);
        Assert.Equal("title", request.Rerank.Property);
        Assert.Equal("best", request.Rerank.Query);
    }

    #endregion

    #region Generate (RAG)

    [Fact]
    public async Task Generate_WithSinglePrompt_SetsGenerativeField()
    {
        await Query()
            .NearText("wireless mouse")
            .Limit(10)
            .Generate(singlePrompt: "Describe this product")
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearText);
        Assert.NotNull(request.Generative);
        Assert.NotNull(request.Generative.Single);
        Assert.Equal("Describe this product", request.Generative.Single.Prompt);
    }

    [Fact]
    public async Task Generate_WithGroupedTask_SetsGenerativeField()
    {
        await Query()
            .NearText("electronics")
            .Generate(groupedTask: new GroupedTask("Summarize all products"))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Generative);
        Assert.NotNull(request.Generative.Grouped);
        Assert.Equal("Summarize all products", request.Generative.Grouped.Task);
    }

    [Fact]
    public async Task Generate_WithBothPrompts_SetsBothFields()
    {
        await Query()
            .Hybrid("gaming keyboard", alpha: 0.7f)
            .Limit(5)
            .Generate(
                singlePrompt: "Describe this",
                groupedTask: new GroupedTask("Compare all items", "title", "price")
            )
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.HybridSearch);
        Assert.Equal("gaming keyboard", request.HybridSearch.Query);
        Assert.Equal(5u, request.Limit);
        Assert.NotNull(request.Generative);
        Assert.NotNull(request.Generative.Single);
        Assert.Equal("Describe this", request.Generative.Single.Prompt);
        Assert.NotNull(request.Generative.Grouped);
        Assert.Equal("Compare all items", request.Generative.Grouped.Task);
        Assert.Contains("title", request.Generative.Grouped.Properties.Values);
        Assert.Contains("price", request.Generative.Grouped.Properties.Values);
    }

    [Fact]
    public async Task Generate_FluentSinglePrompt_SetsGenerativeField()
    {
        await Query()
            .BM25("mouse")
            .Generate()
            .SinglePrompt("Explain this product")
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Bm25Search);
        Assert.NotNull(request.Generative);
        Assert.NotNull(request.Generative.Single);
        Assert.Equal("Explain this product", request.Generative.Single.Prompt);
    }

    [Fact]
    public async Task Generate_FluentGroupedTask_SetsGenerativeField()
    {
        await Query()
            .NearVector(new float[] { 0.1f, 0.2f, 0.3f })
            .Generate()
            .GroupedTask("Summarize results", "title")
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearVector);
        Assert.NotNull(request.Generative);
        Assert.NotNull(request.Generative.Grouped);
        Assert.Equal("Summarize results", request.Generative.Grouped.Task);
        Assert.Contains("title", request.Generative.Grouped.Properties.Values);
    }

    [Fact]
    public async Task Generate_Fetch_WithSinglePrompt_ProducesValidRequest()
    {
        await Query()
            .Generate(singlePrompt: "Describe this item")
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.Null(request.NearText);
        Assert.Null(request.NearVector);
        Assert.NotNull(request.Generative);
        Assert.NotNull(request.Generative.Single);
        Assert.Equal("Describe this item", request.Generative.Single.Prompt);
    }

    [Fact]
    public async Task Generate_NearObject_WithSinglePrompt_ProducesValidRequest()
    {
        var objectId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        await Query()
            .NearObject(objectId)
            .Generate(singlePrompt: "Explain this")
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearObject);
        Assert.Equal(objectId.ToString(), request.NearObject.Id);
        Assert.NotNull(request.Generative);
        Assert.NotNull(request.Generative.Single);
        Assert.Equal("Explain this", request.Generative.Single.Prompt);
    }

    [Fact]
    public async Task Generate_WithFilterAndSearch_PreservesAllParameters()
    {
        await _collection
            .Query<TestModel>()
            .Where(t => t.Price > 10)
            .NearText("wireless mouse")
            .Limit(10)
            .Rerank(new Rerank { Property = "title" })
            .Generate(singlePrompt: "Describe this product")
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Filters);
        Assert.NotNull(request.NearText);
        Assert.Equal(10u, request.Limit);
        Assert.NotNull(request.Rerank);
        Assert.NotNull(request.Generative);
        Assert.NotNull(request.Generative.Single);
        Assert.Equal("Describe this product", request.Generative.Single.Prompt);
    }

    #endregion

    #region Reference filters

    [Fact]
    public async Task Where_ReferenceUUID_BuildsReferenceFilter()
    {
        var authorId = Guid.NewGuid();

        await _collection
            .Query<ArticleWithRef>()
            .Where(a => a.Author!.UUID == authorId)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Filters);
        Assert.Equal(V1.Filters.Types.Operator.Equal, request.Filters.Operator);
        Assert.Equal(authorId.ToString(), request.Filters.ValueText);
        Assert.NotNull(request.Filters.Target?.SingleTarget);
        Assert.Equal("author", request.Filters.Target.SingleTarget.On);
        Assert.Equal("_id", request.Filters.Target.SingleTarget.Target?.Property);
    }

    [Fact]
    public async Task Where_ReferenceProperty_BuildsReferencePropertyFilter()
    {
        await _collection
            .Query<ArticleWithRef>()
            .Where(a => a.Author!.Name == "Alice")
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Filters);
        Assert.Equal(V1.Filters.Types.Operator.Equal, request.Filters.Operator);
        Assert.Equal("Alice", request.Filters.ValueText);
        Assert.NotNull(request.Filters.Target?.SingleTarget);
        Assert.Equal("author", request.Filters.Target.SingleTarget.On);
        Assert.Equal("name", request.Filters.Target.SingleTarget.Target?.Property);
    }

    #endregion

    #region Geo range filter

    [Fact]
    public async Task Where_GeoRange_WithIndividualArgs_ProducesWithinGeoRangeFilter()
    {
        await _collection
            .Query<TestModel>()
            .Where(t => t.Location!.IsWithinGeoRange(52.52f, 13.405f, 5000f))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Filters);
        Assert.Equal(V1.Filters.Types.Operator.WithinGeoRange, request.Filters.Operator);
        Assert.Equal("location", request.Filters.Target?.Property);
        Assert.NotNull(request.Filters.ValueGeo);
        Assert.Equal(52.52f, request.Filters.ValueGeo.Latitude, precision: 4);
        Assert.Equal(13.405f, request.Filters.ValueGeo.Longitude, precision: 4);
        Assert.Equal(5000f, request.Filters.ValueGeo.Distance, precision: 4);
    }

    [Fact]
    public async Task Where_GeoRange_WithConstraint_ProducesWithinGeoRangeFilter()
    {
        var constraint = new GeoCoordinateConstraint(48.8566f, 2.3522f, 10000f);

        await _collection
            .Query<TestModel>()
            .Where(t => t.Location!.IsWithinGeoRange(constraint))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Filters);
        Assert.Equal(V1.Filters.Types.Operator.WithinGeoRange, request.Filters.Operator);
        Assert.Equal("location", request.Filters.Target?.Property);
        Assert.NotNull(request.Filters.ValueGeo);
        Assert.Equal(48.8566f, request.Filters.ValueGeo.Latitude, precision: 4);
        Assert.Equal(2.3522f, request.Filters.ValueGeo.Longitude, precision: 4);
        Assert.Equal(10000f, request.Filters.ValueGeo.Distance, precision: 4);
    }

    [Fact]
    public async Task Where_GeoRange_CombinedWithOtherFilter_ProducesAllOfFilter()
    {
        await _collection
            .Query<TestModel>()
            .Where(t => t.Price > 0)
            .Where(t => t.Location!.IsWithinGeoRange(51.5074f, -0.1278f, 2000f))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Filters);
        Assert.Equal(V1.Filters.Types.Operator.And, request.Filters.Operator);
        Assert.Equal(2, request.Filters.Filters_.Count);
        Assert.Contains(
            request.Filters.Filters_,
            f => f.Operator == V1.Filters.Types.Operator.WithinGeoRange
        );
    }

    #endregion

    #region Multi-target vector methods

    [Fact]
    public async Task NearText_MultiTarget_Sum_SetsTargetsWithSumCombination()
    {
        await Query()
            .NearText("search")
            .VectorTargets(b => b.Sum(t => t.Embedding!, t => t.Embedding2!))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.NearText?.Targets);
        Assert.Contains("embedding", request.NearText.Targets.TargetVectors);
        Assert.Contains("embedding2", request.NearText.Targets.TargetVectors);
        Assert.Equal(V1.CombinationMethod.TypeSum, request.NearText.Targets.Combination);
    }

    [Fact]
    public async Task NearText_MultiTarget_Average_SetsCombination()
    {
        await Query()
            .NearText("search")
            .VectorTargets(b => b.Average(t => t.Embedding!, t => t.Embedding2!))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.NearText?.Targets);
        Assert.Equal(V1.CombinationMethod.TypeAverage, request.NearText.Targets.Combination);
        Assert.Equal(2, request.NearText.Targets.TargetVectors.Count);
    }

    [Fact]
    public async Task NearText_MultiTarget_ManualWeights_SetsWeightsAndCombination()
    {
        await Query()
            .NearText("search")
            .VectorTargets(b =>
                b.ManualWeights((t => t.Embedding!, 0.7), (t => t.Embedding2!, 0.3))
            )
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.NearText?.Targets);
        Assert.Contains("embedding", request.NearText.Targets.TargetVectors);
        Assert.Contains("embedding2", request.NearText.Targets.TargetVectors);
        Assert.Equal(V1.CombinationMethod.TypeManual, request.NearText.Targets.Combination);
        Assert.NotEmpty(request.NearText.Targets.WeightsForTargets);
    }

    [Fact]
    public async Task NearVector_SingleVec_MultiTarget_Sum_SetsTargets()
    {
        var vec = new float[] { 0.1f, 0.2f, 0.3f };
        await Query()
            .NearVector(vec)
            .VectorTargets(b => b.Sum(t => t.Embedding!, t => t.Embedding2!))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.NearVector?.Targets);
        Assert.Contains("embedding", request.NearVector.Targets.TargetVectors);
        Assert.Contains("embedding2", request.NearVector.Targets.TargetVectors);
        Assert.Equal(V1.CombinationMethod.TypeSum, request.NearVector.Targets.Combination);
    }

    [Fact]
    public async Task NearVector_PerTargetVectors_Sum_SetsNamedVectors()
    {
        var vec1 = new float[] { 0.1f, 0.2f };
        var vec2 = new float[] { 0.3f, 0.4f };
        await Query()
            .NearVector()
            .VectorTargets(b => b.Sum((t => t.Embedding!, vec1), (t => t.Embedding2!, vec2)))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.NearVector?.Targets);
        Assert.Contains("embedding", request.NearVector.Targets.TargetVectors);
        Assert.Contains("embedding2", request.NearVector.Targets.TargetVectors);
        Assert.Equal(V1.CombinationMethod.TypeSum, request.NearVector.Targets.Combination);
    }

    [Fact]
    public async Task NearVector_PerTargetVectors_ManualWeights_SetsWeights()
    {
        var vec1 = new float[] { 0.1f, 0.2f };
        var vec2 = new float[] { 0.3f, 0.4f };
        await Query()
            .NearVector()
            .VectorTargets(b =>
                b.ManualWeights((t => t.Embedding!, vec1, 0.7), (t => t.Embedding2!, vec2, 0.3))
            )
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.NearVector?.Targets);
        Assert.Equal(V1.CombinationMethod.TypeManual, request.NearVector.Targets.Combination);
        Assert.NotEmpty(request.NearVector.Targets.WeightsForTargets);
    }

    [Fact]
    public async Task NearObject_MultiTarget_Sum_SetsTargets()
    {
        var objectId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        await Query()
            .NearObject(objectId)
            .VectorTargets(b => b.Sum(t => t.Embedding!, t => t.Embedding2!))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.NearObject?.Targets);
        Assert.Contains("embedding", request.NearObject.Targets.TargetVectors);
        Assert.Contains("embedding2", request.NearObject.Targets.TargetVectors);
        Assert.Equal(V1.CombinationMethod.TypeSum, request.NearObject.Targets.Combination);
    }

    [Fact]
    public async Task Hybrid_NearTextStyle_MultiTarget_Sum_SetsHybridTargets()
    {
        // NearText-style: targets move to hybrid.Targets (matching Python client behaviour)
        await Query()
            .Hybrid("search")
            .VectorTargets(b => b.Sum(t => t.Embedding!, t => t.Embedding2!))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.HybridSearch?.Targets);
        Assert.Contains("embedding", request.HybridSearch.Targets.TargetVectors);
        Assert.Contains("embedding2", request.HybridSearch.Targets.TargetVectors);
        Assert.Equal(V1.CombinationMethod.TypeSum, request.HybridSearch.Targets.Combination);
    }

    [Fact]
    public async Task Hybrid_NearVectorStyle_PerTargetVectors_Sum_SetsHybridTargets()
    {
        var vec1 = new float[] { 0.1f, 0.2f };
        var vec2 = new float[] { 0.3f, 0.4f };
        await Query()
            .Hybrid("search")
            .VectorTargets(b => b.Sum((t => t.Embedding!, vec1), (t => t.Embedding2!, vec2)))
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.HybridSearch?.Targets);
        Assert.Contains("embedding", request.HybridSearch.Targets.TargetVectors);
        Assert.Contains("embedding2", request.HybridSearch.Targets.TargetVectors);
        Assert.Equal(V1.CombinationMethod.TypeSum, request.HybridSearch.Targets.Combination);
    }

    #endregion

    #region GroupBy

    [Fact]
    public async Task GroupBy_NearText_SetsGroupByRequest()
    {
        await Query()
            .NearText("laptop")
            .GroupBy(t => t.Title, numberOfGroups: 5, objectsPerGroup: 3)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.NearText);
        Assert.NotNull(request.GroupBy);
        Assert.Equal("title", request.GroupBy.Path[0]);
        Assert.Equal(5, request.GroupBy.NumberOfGroups);
        Assert.Equal(3, request.GroupBy.ObjectsPerGroup);
    }

    [Fact]
    public async Task GroupBy_Fetch_SetsGroupByRequest()
    {
        await Query()
            .GroupBy(t => t.WordCount, numberOfGroups: 3, objectsPerGroup: 10)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.GroupBy);
        Assert.Equal("wordCount", request.GroupBy.Path[0]);
        Assert.Equal(3, request.GroupBy.NumberOfGroups);
        Assert.Equal(10, request.GroupBy.ObjectsPerGroup);
    }

    [Fact]
    public async Task GroupBy_BM25_SetsGroupByAndSearchMode()
    {
        await Query()
            .BM25("keyboard")
            .Limit(20)
            .GroupBy(t => t.Title, numberOfGroups: 4, objectsPerGroup: 5)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.Bm25Search);
        Assert.Equal("keyboard", request.Bm25Search.Query);
        Assert.NotNull(request.GroupBy);
        Assert.Equal("title", request.GroupBy.Path[0]);
        Assert.Equal(4, request.GroupBy.NumberOfGroups);
        Assert.Equal(5, request.GroupBy.ObjectsPerGroup);
        Assert.Equal(20u, request.Limit);
    }

    [Fact]
    public async Task GroupBy_Hybrid_SetsGroupByRequest()
    {
        await Query()
            .Hybrid("gaming", alpha: 0.75f)
            .GroupBy(t => t.Title, numberOfGroups: 3, objectsPerGroup: 2)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request);
        Assert.NotNull(request.HybridSearch);
        Assert.Equal("gaming", request.HybridSearch.Query);
        Assert.NotNull(request.GroupBy);
        Assert.Equal("title", request.GroupBy.Path[0]);
        Assert.Equal(3, request.GroupBy.NumberOfGroups);
        Assert.Equal(2, request.GroupBy.ObjectsPerGroup);
    }

    [Fact]
    public async Task GroupBy_PropertyNameConvertedToCamelCase()
    {
        await Query()
            .GroupBy(t => t.WordCount, numberOfGroups: 5, objectsPerGroup: 3)
            .Execute(TestContext.Current.CancellationToken);

        var request = _getRequest();
        Assert.NotNull(request?.GroupBy);
        Assert.Equal("wordCount", request.GroupBy.Path[0]);
    }

    #endregion
}
