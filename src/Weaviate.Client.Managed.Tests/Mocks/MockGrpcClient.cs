using Weaviate.Client;
using V1 = Weaviate.Client.Grpc.Protobuf.V1;

namespace Weaviate.Client.Managed.Tests.Mocks;

/// <summary>
/// Helper class for creating WeaviateClient instances with gRPC request capture capabilities.
/// </summary>
internal static class MockGrpcClient
{
    /// <summary>
    /// Creates a WeaviateClient that captures gRPC requests for the specified path pattern.
    /// </summary>
    /// <typeparam name="TRequest">The protobuf request type to capture (e.g., SearchRequest, AggregateRequest)</typeparam>
    /// <param name="pathPattern">The gRPC path pattern to match (e.g., "/weaviate.v1.Weaviate/Search")</param>
    /// <param name="responseFactory">Optional factory to create custom response messages. If null, returns empty SearchReply for Search operations.</param>
    /// <returns>A tuple containing the client and a function to retrieve the captured request</returns>
    public static (
        WeaviateClient Client,
        Func<TRequest?> GetCapturedRequest
    ) CreateWithRequestCapture<TRequest>(
        string pathPattern,
        Func<Google.Protobuf.IMessage>? responseFactory = null
    )
        where TRequest : class, Google.Protobuf.IMessage<TRequest>, new()
    {
        TRequest? capturedRequest = null;

        var channel = NoOpGrpcChannel.Create(
            customAsyncHandler: async (request, ct) =>
            {
                var path = request.RequestUri?.PathAndQuery ?? string.Empty;
                if (path.Contains(pathPattern))
                {
                    var content = await request.Content!.ReadAsByteArrayAsync(ct);
                    capturedRequest = GrpcTestHelpers.DecodeGrpcRequest<TRequest>(content);

                    // Use factory if provided, otherwise default to empty SearchReply for Search operations
                    var replyMessage =
                        responseFactory?.Invoke()
                        ?? new V1.SearchReply { Collection = "TestCollection" };
                    return GrpcTestHelpers.CreateGrpcResponse(replyMessage);
                }
                return null;
            }
        );

        var grpcClient = new Grpc.WeaviateGrpcClient(channel);
        var client = new WeaviateClient(grpcClient: grpcClient);

        return (client, () => capturedRequest);
    }

    /// <summary>
    /// Creates a WeaviateClient that captures Search requests.
    /// Convenience method for the most common use case.
    /// </summary>
    public static (
        WeaviateClient Client,
        Func<V1.SearchRequest?> GetRequest
    ) CreateWithSearchCapture()
    {
        return CreateWithRequestCapture<V1.SearchRequest>("/weaviate.v1.Weaviate/Search");
    }

    /// <summary>
    /// Creates a WeaviateClient that captures Aggregate requests.
    /// </summary>
    public static (
        WeaviateClient Client,
        Func<V1.AggregateRequest?> GetCapturedRequest
    ) CreateWithAggregateCapture()
    {
        return CreateWithRequestCapture<V1.AggregateRequest>(
            "/weaviate.v1.Weaviate/Aggregate",
            () => new V1.AggregateReply { Collection = "TestCollection" }
        );
    }
}
