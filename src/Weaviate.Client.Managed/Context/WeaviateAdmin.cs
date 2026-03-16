using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Context;

/// <summary>
/// Provides organized access to Weaviate administrative operations from a
/// <see cref="WeaviateContext"/>. Wraps the core client's admin sub-clients
/// (backup, RBAC, cluster, aliases) and health-check methods.
/// </summary>
/// <example>
/// <code>
/// // Health check
/// var ready = await context.Admin.IsReady();
///
/// // Backup operations
/// var backup = await context.Admin.Backup.CreateSync(request);
///
/// // RBAC
/// var roles = await context.Admin.Roles.ListAll();
///
/// // Cluster info
/// var nodes = await context.Admin.Cluster.Nodes.List();
///
/// // Aliases
/// await context.Admin.Aliases.Create("products-v2", "Products");
/// </code>
/// </example>
public class WeaviateAdmin
{
    private readonly WeaviateClient _client;

    internal WeaviateAdmin(WeaviateClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Backup and restore operations.
    /// </summary>
    public BackupClient Backup => _client.Backup;

    /// <summary>
    /// User management (database users and OIDC users).
    /// </summary>
    public UsersClient Users => _client.Users;

    /// <summary>
    /// Role management and permission assignment.
    /// </summary>
    public RolesClient Roles => _client.Roles;

    /// <summary>
    /// OIDC group management.
    /// </summary>
    public GroupsClient Groups => _client.Groups;

    /// <summary>
    /// Cluster and replication operations.
    /// </summary>
    public ClusterClient Cluster => _client.Cluster;

    /// <summary>
    /// Collection alias management.
    /// </summary>
    public AliasClient Aliases => _client.Alias;

    /// <summary>
    /// Returns true if the Weaviate process is live.
    /// </summary>
    public Task<bool> IsLive(CancellationToken cancellationToken = default)
    {
        return _client.IsLive(cancellationToken);
    }

    /// <summary>
    /// Returns true if the Weaviate instance is ready to accept requests.
    /// </summary>
    public Task<bool> IsReady(CancellationToken cancellationToken = default)
    {
        return _client.IsReady(cancellationToken);
    }

    /// <summary>
    /// Waits until the Weaviate instance is ready, polling at the specified interval.
    /// </summary>
    /// <param name="timeout">Maximum time to wait.</param>
    /// <param name="pollInterval">How often to check readiness.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if ready within the timeout; false otherwise.</returns>
    public Task<bool> WaitUntilReady(
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default
    )
    {
        return _client.WaitUntilReady(timeout, pollInterval, cancellationToken);
    }

    /// <summary>
    /// Fetches the server metadata from the Weaviate instance.
    /// </summary>
    public Task<MetaInfo> GetMeta(CancellationToken cancellationToken = default)
    {
        return _client.GetMeta(cancellationToken);
    }
}
