using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Mapping;
using Weaviate.Client.Managed.Models;
using Weaviate.Client.Models;
using Weaviate.Client.Models.Typed;

namespace Weaviate.Client.Managed.Query;

/// <summary>
/// Terminal executor for grouped search queries. Obtain an instance via
/// <see cref="CollectionMapperQueryClient{T}.GroupBy{TProp}"/>.
/// </summary>
/// <example>
/// <code>
/// var response = await context.Products.Query()
///     .NearText("laptop")
///     .Where(p => p.InStock)
///     .GroupBy(p => p.Category, numberOfGroups: 5, objectsPerGroup: 3)
///     .Execute();
///
/// foreach (var group in response.Groups.Values)
///     Console.WriteLine($"{group.Name}: {group.Count} objects");
/// </code>
/// </example>
public sealed class GroupByQueryExecutor<T>
    where T : class, new()
{
    private readonly CollectionMapperQueryClient<T> _inner;
    private readonly GroupByRequest _request;

    internal GroupByQueryExecutor(CollectionMapperQueryClient<T> inner, GroupByRequest request)
    {
        _inner = inner;
        _request = request;
    }

    /// <summary>
    /// Executes the grouped search query and returns the result bucketed by the
    /// specified property. This method is optional - the executor is directly awaitable.
    /// </summary>
    public async Task<GroupByQueryResponse<T>> Execute(
        CancellationToken cancellationToken = default
    )
    {
        var result = await _inner.ExecuteGroupByQueryAsync(_request, cancellationToken);
        return MapResult(result);
    }

    /// <summary>
    /// Makes this grouped search query directly awaitable without explicitly calling <see cref="Execute"/>.
    /// </summary>
    /// <returns>A task awaiter for the grouped search result.</returns>
    public TaskAwaiter<GroupByQueryResponse<T>> GetAwaiter()
    {
        return Execute(CancellationToken.None).GetAwaiter();
    }

    private static GroupByQueryResponse<T> MapResult(GroupByResult<T> result)
    {
        // Build the flat objects list, reusing items from the group buckets so we only
        // create each GroupByQueryResult<T> once (then share across the Groups dict).
        var groups = result.Groups.ToDictionary(
            kvp => kvp.Key,
            kvp => new GroupByGroup<T>
            {
                Name = kvp.Value.Name,
                MinDistance = kvp.Value.MinDistance,
                MaxDistance = kvp.Value.MaxDistance,
                Objects = kvp
                    .Value.Objects.Select(o => new GroupByQueryResult<T>
                    {
                        UUID = o.UUID,
                        Object = ManagedObjectMapper.FromWeaviateObject(o),
                        Metadata = o.Metadata,
                        BelongsToGroup = o.BelongsToGroup,
                    })
                    .ToList(),
            }
        );

        var objects = groups.Values.SelectMany(g => g.Objects).ToList();

        return new GroupByQueryResponse<T> { Objects = objects, Groups = groups };
    }
}
