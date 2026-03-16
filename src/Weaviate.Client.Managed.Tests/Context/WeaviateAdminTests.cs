using Weaviate.Client.Managed.Tests.Mocks;
using Xunit;

namespace Weaviate.Client.Managed.Tests.Context;

public class WeaviateAdminTests
{
    private static WeaviateClient CreateMockClient()
    {
        var (client, _) = MockWeaviateClient.CreateWithMockHandler();
        return client;
    }

    [Fact]
    public void Admin_ExposesBackupClient()
    {
        var client = CreateMockClient();
        var admin = new WeaviateAdmin(client);

        Assert.NotNull(admin.Backup);
        Assert.Same(client.Backup, admin.Backup);
    }

    [Fact]
    public void Admin_ExposesUsersClient()
    {
        var client = CreateMockClient();
        var admin = new WeaviateAdmin(client);

        Assert.NotNull(admin.Users);
        Assert.Same(client.Users, admin.Users);
    }

    [Fact]
    public void Admin_ExposesRolesClient()
    {
        var client = CreateMockClient();
        var admin = new WeaviateAdmin(client);

        Assert.NotNull(admin.Roles);
        Assert.Same(client.Roles, admin.Roles);
    }

    [Fact]
    public void Admin_ExposesGroupsClient()
    {
        var client = CreateMockClient();
        var admin = new WeaviateAdmin(client);

        Assert.NotNull(admin.Groups);
        Assert.Same(client.Groups, admin.Groups);
    }

    [Fact]
    public void Admin_ExposesClusterClient()
    {
        var client = CreateMockClient();
        var admin = new WeaviateAdmin(client);

        Assert.NotNull(admin.Cluster);
        Assert.Same(client.Cluster, admin.Cluster);
    }

    [Fact]
    public void Admin_ExposesAliasesClient()
    {
        var client = CreateMockClient();
        var admin = new WeaviateAdmin(client);

        Assert.NotNull(admin.Aliases);
        Assert.Same(client.Alias, admin.Aliases);
    }
}
