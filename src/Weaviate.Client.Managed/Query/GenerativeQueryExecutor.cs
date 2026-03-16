using System.Runtime.CompilerServices;
using Weaviate.Client.Managed.Models;
using Weaviate.Client.Models;

namespace Weaviate.Client.Managed.Query;

/// <summary>
/// Executes a generative (RAG) query built from <see cref="CollectionMapperQueryClient{T}"/>.
/// Holds generative-specific parameters (prompts, provider) and delegates execution
/// to the query builder's internal generative execution method.
/// </summary>
/// <typeparam name="T">The entity type being queried.</typeparam>
/// <example>
/// <code>
/// // Execute() is optional - directly awaitable
/// var results = await products.Query()
///     .NearText("wireless mouse")
///     .Limit(10)
///     .Generate(singlePrompt: "Describe this product");
///
/// foreach (var r in results)
///     Console.WriteLine($"{r.Object.Name}: {r.Generative?[0]}");
/// </code>
/// </example>
public class GenerativeQueryExecutor<T>
    where T : class, new()
{
    private readonly CollectionMapperQueryClient<T> _queryBuilder;
    private SinglePrompt? _singlePrompt;
    private GroupedTask? _groupedTask;
    private GenerativeProvider? _provider;

    internal GenerativeQueryExecutor(
        CollectionMapperQueryClient<T> queryBuilder,
        SinglePrompt? singlePrompt = null,
        GroupedTask? groupedTask = null,
        GenerativeProvider? provider = null
    )
    {
        _queryBuilder = queryBuilder;
        _singlePrompt = singlePrompt;
        _groupedTask = groupedTask;
        _provider = provider;
    }

    /// <summary>
    /// Sets the single prompt for per-object generation.
    /// The prompt is applied to each search result individually.
    /// Supports <c>{propertyName}</c> template substitution.
    /// </summary>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>This executor for chaining.</returns>
    public GenerativeQueryExecutor<T> SinglePrompt(string prompt)
    {
        _singlePrompt = prompt;
        return this;
    }

    /// <summary>
    /// Sets the single prompt for per-object generation.
    /// </summary>
    /// <param name="prompt">The prompt configuration.</param>
    /// <returns>This executor for chaining.</returns>
    public GenerativeQueryExecutor<T> SinglePrompt(Weaviate.Client.Models.SinglePrompt prompt)
    {
        _singlePrompt = prompt;
        return this;
    }

    /// <summary>
    /// Sets the grouped task for result-set level generation.
    /// The task is applied to the entire result set as context.
    /// </summary>
    /// <param name="task">The task text.</param>
    /// <param name="properties">Properties to include as context for the task.</param>
    /// <returns>This executor for chaining.</returns>
    public GenerativeQueryExecutor<T> GroupedTask(string task, params string[] properties)
    {
        _groupedTask = new Weaviate.Client.Models.GroupedTask(task, properties);
        return this;
    }

    /// <summary>
    /// Sets the grouped task for result-set level generation.
    /// </summary>
    /// <param name="task">The grouped task configuration.</param>
    /// <returns>This executor for chaining.</returns>
    public GenerativeQueryExecutor<T> GroupedTask(Weaviate.Client.Models.GroupedTask task)
    {
        _groupedTask = task;
        return this;
    }

    /// <summary>
    /// Sets the generative AI provider to use.
    /// If not specified, the collection's configured generative module is used.
    /// </summary>
    /// <param name="provider">The provider configuration.</param>
    /// <returns>This executor for chaining.</returns>
    public GenerativeQueryExecutor<T> WithProvider(GenerativeProvider provider)
    {
        _provider = provider;
        return this;
    }

    /// <summary>
    /// Executes the generative query and returns mapped results with generative data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generative query response containing mapped entities and AI-generated content.</returns>
    public Task<GenerativeQueryResponse<T>> Execute(CancellationToken cancellationToken = default)
    {
        return _queryBuilder.ExecuteGenerativeQueryAsync(
            _singlePrompt,
            _groupedTask,
            _provider,
            cancellationToken
        );
    }

    /// <summary>
    /// Makes this query directly awaitable without explicitly calling <see cref="Execute"/>.
    /// </summary>
    /// <returns>A task awaiter for the generative query response.</returns>
    public TaskAwaiter<GenerativeQueryResponse<T>> GetAwaiter()
    {
        return Execute(CancellationToken.None).GetAwaiter();
    }
}
