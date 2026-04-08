using Weaviate.Client.Managed.Extensions;
using Xunit;

namespace Weaviate.Client.Managed.Tests.Extensions;

public class WeaviateClientExtensionsTests
{
    [Fact]
    public void WithManagedIntegrationHeader_AddsHeader()
    {
        var config = new ClientConfiguration();
        var result = config.WithManagedIntegrationHeader();

        Assert.NotNull(result.Headers);
        Assert.True(result.Headers.ContainsKey(WeaviateDefaults.IntegrationHeader));
        Assert.Matches(
            @"^weaviate-client-csharp-managed/\d+",
            result.Headers[WeaviateDefaults.IntegrationHeader]
        );
    }

    [Fact]
    public void WithManagedIntegrationHeader_DoesNotOverwriteExistingValue()
    {
        var config = new ClientConfiguration(
            Headers: new Dictionary<string, string>
            {
                [WeaviateDefaults.IntegrationHeader] = "existing/1.0",
            }
        );
        var result = config.WithManagedIntegrationHeader();

        var value = result.Headers![WeaviateDefaults.IntegrationHeader];
        // Existing value is preserved; managed segment is appended
        Assert.Contains("existing/1.0", value);
        Assert.Contains("weaviate-client-csharp-managed/", value);
    }

    [Fact]
    public void WithManagedIntegrationHeader_DoesNotMutateOriginal()
    {
        var config = new ClientConfiguration();
        var result = config.WithManagedIntegrationHeader();

        // Original is unchanged (record with syntax returns new instance)
        Assert.Null(config.Headers);
        Assert.NotNull(result.Headers);
    }
}
