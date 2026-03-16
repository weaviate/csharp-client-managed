using System.Net;
using Weaviate.Client;

namespace Weaviate.Client.Managed.Tests.Mocks;

internal static class MockWeaviateClient
{
    public static (WeaviateClient Client, MockHttpMessageHandler Handler) CreateWithMockHandler()
    {
        var mockHandler = new MockHttpMessageHandler(
            (request, ct) =>
            {
                var path = request.RequestUri?.PathAndQuery ?? string.Empty;
                string content;
                if (path.Contains("/schema"))
                {
                    // Return a schema with TestCollection
                    content =
                        "{\"classes\": [{\"class\": \"TestCollection\", \"properties\": []}]}";
                }
                else
                {
                    content = "{}";
                }
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content),
                };
                return Task.FromResult(response);
            }
        );

        // Create a no-op gRPC channel to avoid network calls
        var grpcChannel = NoOpGrpcChannel.Create();
        var grpcClient = new Grpc.WeaviateGrpcClient(grpcChannel);

        // Pass the mock gRPC client to avoid health check failures
        var client = new WeaviateClient(httpMessageHandler: mockHandler, grpcClient: grpcClient);

        return (client, mockHandler);
    }
}

internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<
        HttpRequestMessage,
        CancellationToken,
        Task<HttpResponseMessage>
    > _handler;

    public MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler
    )
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        return _handler(request, cancellationToken);
    }
}
