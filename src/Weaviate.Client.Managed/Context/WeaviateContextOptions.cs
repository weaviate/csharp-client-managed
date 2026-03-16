namespace Weaviate.Client.Managed.Context;

/// <summary>
/// Options for configuring a WeaviateContext.
/// </summary>
public class WeaviateContextOptions
{
    /// <summary>
    /// Gets or sets whether to automatically create collections that don't exist.
    /// Default is false.
    /// </summary>
    public bool AutoCreateCollections { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to automatically migrate collections on first access.
    /// Default is false.
    /// </summary>
    public bool AutoMigrate { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to allow breaking changes during auto-migration.
    /// Only applies if AutoMigrate is true.
    /// Default is false.
    /// </summary>
    public bool AllowBreakingMigrations { get; set; } = false;
}

/// <summary>
/// Builder for configuring WeaviateContextOptions using a fluent API.
/// </summary>
public class WeaviateContextOptionsBuilder
{
    private readonly WeaviateContextOptions _options;

    /// <summary>
    /// Creates a new options builder.
    /// </summary>
    /// <param name="options">The options instance to configure.</param>
    internal WeaviateContextOptionsBuilder(WeaviateContextOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Enables or disables automatic collection creation.
    /// </summary>
    /// <param name="enable">Whether to enable auto-creation.</param>
    /// <returns>The builder for chaining.</returns>
    public WeaviateContextOptionsBuilder UseAutoCreate(bool enable = true)
    {
        _options.AutoCreateCollections = enable;
        return this;
    }

    /// <summary>
    /// Enables or disables automatic migration on first access.
    /// </summary>
    /// <param name="enable">Whether to enable auto-migration.</param>
    /// <param name="allowBreaking">Whether to allow breaking changes.</param>
    /// <returns>The builder for chaining.</returns>
    public WeaviateContextOptionsBuilder UseAutoMigrate(
        bool enable = true,
        bool allowBreaking = false
    )
    {
        _options.AutoMigrate = enable;
        _options.AllowBreakingMigrations = allowBreaking;
        return this;
    }
}

/// <summary>
/// Typed options for a specific WeaviateContext subclass.
/// Used by dependency injection to support multiple context types with independent configuration.
/// </summary>
/// <typeparam name="TContext">The context type these options apply to.</typeparam>
public class WeaviateContextOptions<TContext> : WeaviateContextOptions
    where TContext : WeaviateContext { }
