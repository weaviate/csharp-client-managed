using Xunit;

namespace Weaviate.Client.Managed.Tests.Query;

public class GenerativeQueryResultTests
{
    [Fact]
    public void GenerativeQueryResponse_ImplementsIEnumerable()
    {
        var results = new GenerativeQueryResponse<TestModel>
        {
            Results = new List<GenerativeQueryResult<TestModel>>
            {
                new()
                {
                    UUID = Guid.NewGuid(),
                    Object = new TestModel { Title = "Item 1" },
                    Generative = new GenerativeResult([new GenerativeReply("Generated text 1")]),
                },
                new()
                {
                    UUID = Guid.NewGuid(),
                    Object = new TestModel { Title = "Item 2" },
                    Generative = new GenerativeResult([new GenerativeReply("Generated text 2")]),
                },
            },
            Generative = new GenerativeResult([new GenerativeReply("Grouped result")]),
        };

        // Can enumerate
        var count = 0;
        foreach (var result in results)
        {
            count++;
            Assert.NotNull(result.Object);
            Assert.NotNull(result.Generative);
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public void GenerativeQueryResponse_GroupedResult_IsAccessible()
    {
        var response = new GenerativeQueryResponse<TestModel>
        {
            Results = new List<GenerativeQueryResult<TestModel>>(),
            Generative = new GenerativeResult([new GenerativeReply("Summary of all results")]),
        };

        Assert.NotNull(response.Generative);
        Assert.Equal("Summary of all results", response.Generative[0]);
    }

    [Fact]
    public void GenerativeQueryResult_ExtendsQueryResult()
    {
        var result = new GenerativeQueryResult<TestModel>
        {
            UUID = Guid.NewGuid(),
            Object = new TestModel { Title = "Test" },
            Metadata = new Metadata { Score = 0.95f },
            Generative = new GenerativeResult([new GenerativeReply("AI generated")]),
        };

        // Base properties accessible
        Assert.NotNull(result.UUID);
        Assert.Equal("Test", result.Object.Title);
        Assert.Equal(0.95f, result.Metadata!.Score);

        // Generative property accessible
        Assert.NotNull(result.Generative);
        Assert.Equal("AI generated", result.Generative[0]);
    }

    [Fact]
    public void GenerativeQueryResponse_EmptyResults_IsValid()
    {
        var response = new GenerativeQueryResponse<TestModel>
        {
            Results = Array.Empty<GenerativeQueryResult<TestModel>>(),
            Generative = null,
        };

        Assert.Empty(response.Results);
        Assert.Null(response.Generative);
        Assert.Empty(response);
    }
}
