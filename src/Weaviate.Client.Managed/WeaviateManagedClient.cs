namespace Weaviate.Client.Managed;

/// <summary>
/// A managed client for Weaviate that provides type-safe interactions with the Weaviate Vector Database.
/// </summary>
public class WeaviateManagedClient
{
    private readonly WeaviateClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeaviateManagedClient"/> class.
    /// </summary>
    /// <param name="client">The underlying Weaviate client.</param>
    public WeaviateManagedClient(WeaviateClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Gets the underlying Weaviate client.
    /// </summary>
    public WeaviateClient Client => _client;
}
