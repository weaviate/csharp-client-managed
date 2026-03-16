using System.Net;
using Google.Protobuf;

namespace Weaviate.Client.Managed.Tests.Mocks;

/// <summary>
/// Helper methods for gRPC testing
/// </summary>
internal static class GrpcTestHelpers
{
    /// <summary>
    /// Serializes a protobuf message to gRPC wire format.
    /// Wire format: [compression flag (1 byte)][message length (4 bytes, big-endian)][protobuf message]
    /// </summary>
    /// <param name="message">The protobuf message to serialize</param>
    /// <returns>Byte array in gRPC wire format</returns>
    public static byte[] SerializeGrpcMessage(IMessage message)
    {
        var messageBytes = message.ToByteArray();
        var grpcMessage = new byte[5 + messageBytes.Length];

        // Compression flag (0 = no compression)
        grpcMessage[0] = 0;

        // Message length in big-endian (4 bytes)
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
            grpcMessage.AsSpan(1, 4),
            (uint)messageBytes.Length
        );

        // Copy protobuf message
        messageBytes.CopyTo(grpcMessage, 5);

        return grpcMessage;
    }

    /// <summary>
    /// Creates an HTTP/2 response message containing a gRPC-encoded protobuf message.
    /// Suitable for mocking gRPC service responses in tests.
    /// </summary>
    /// <param name="message">The protobuf message to include in the response</param>
    /// <param name="statusCode">HTTP status code (default: OK)</param>
    /// <param name="grpcStatus">gRPC status code (default: 0 = OK)</param>
    /// <returns>HttpResponseMessage configured for gRPC</returns>
    public static HttpResponseMessage CreateGrpcResponse(
        IMessage message,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        int grpcStatus = 0
    )
    {
        var grpcMessage = SerializeGrpcMessage(message);

        var response = new HttpResponseMessage(statusCode)
        {
            Version = new Version(2, 0), // HTTP/2 is required for gRPC
            Content = new ByteArrayContent(grpcMessage),
        };

        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/grpc"
        );
        response.TrailingHeaders.Add("grpc-status", grpcStatus.ToString());

        return response;
    }

    /// <summary>
    /// Decodes a gRPC request from wire format
    /// </summary>
    /// <typeparam name="T">The protobuf message type</typeparam>
    /// <param name="content">The gRPC wire format bytes</param>
    /// <returns>The decoded protobuf message</returns>
    public static T DecodeGrpcRequest<T>(byte[] content)
        where T : Google.Protobuf.IMessage<T>, new()
    {
        // gRPC wire format: 1 byte compressed flag + 4 bytes length + message bytes
        var messageBytes = content.Skip(5).ToArray();
        var parser = new Google.Protobuf.MessageParser<T>(() => new T());
        return parser.ParseFrom(messageBytes);
    }
}
