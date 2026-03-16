using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Weaviate.Client.Managed.Context;

namespace Weaviate.Client.Managed.DependencyInjection;

/// <summary>
/// Extension methods for registering Weaviate managed context services with dependency injection.
/// </summary>
public static class WeaviateManagedServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="WeaviateContext"/> subclass with the dependency injection container.
    /// </summary>
    /// <typeparam name="TContext">The context type to register. Must derive from <see cref="WeaviateContext"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure context options.</param>
    /// <param name="lifetime">
    /// The service lifetime for the context. Default is <see cref="ServiceLifetime.Singleton"/>.
    /// Unlike EF Core, WeaviateContext wraps a singleton client with no connection pooling or change tracking,
    /// so singleton is the natural default. Use <see cref="ServiceLifetime.Scoped"/> if you need
    /// per-request tenant scoping via middleware.
    /// </param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddWeaviateContext<TContext>(
        this IServiceCollection services,
        Action<WeaviateContextOptions>? configureOptions = null,
        ServiceLifetime lifetime = ServiceLifetime.Singleton
    )
        where TContext : WeaviateContext
    {
        // Configure typed options for this context type
        if (configureOptions != null)
        {
            services.Configure<WeaviateContextOptions<TContext>>(configureOptions);
        }
        else
        {
            services.TryAddSingleton(Options.Create(new WeaviateContextOptions<TContext>()));
        }

        // Check if context type has a constructor accepting WeaviateContextOptions
        var hasOptionsConstructor = typeof(TContext)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Any(c =>
                c.GetParameters()
                    .Any(p => typeof(WeaviateContextOptions).IsAssignableFrom(p.ParameterType))
            );

        // Register the context using a factory delegate
        var descriptor = ServiceDescriptor.Describe(
            typeof(TContext),
            sp =>
            {
                if (hasOptionsConstructor)
                {
                    var options = sp.GetRequiredService<
                        IOptions<WeaviateContextOptions<TContext>>
                    >();
                    return ActivatorUtilities.CreateInstance<TContext>(
                        sp,
                        (WeaviateContextOptions)options.Value
                    );
                }

                return ActivatorUtilities.CreateInstance<TContext>(sp);
            },
            lifetime
        );
        services.TryAdd(descriptor);

        return services;
    }

    /// <summary>
    /// Registers a <see cref="WeaviateContext"/> subclass with the dependency injection container
    /// and optionally runs schema migration at application startup.
    /// </summary>
    /// <typeparam name="TContext">The context type to register. Must derive from <see cref="WeaviateContext"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional action to configure context options.</param>
    /// <param name="eagerMigration">
    /// Whether to run schema migration at application startup via an <see cref="IHostedService"/>.
    /// Default is true.
    /// </param>
    /// <param name="lifetime">The service lifetime for the context. Default is <see cref="ServiceLifetime.Singleton"/>.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddWeaviateContext<TContext>(
        this IServiceCollection services,
        Action<WeaviateContextOptions>? configureOptions,
        bool eagerMigration,
        ServiceLifetime lifetime = ServiceLifetime.Singleton
    )
        where TContext : WeaviateContext
    {
        services.AddWeaviateContext<TContext>(configureOptions, lifetime);

        if (eagerMigration)
        {
            services.AddHostedService<WeaviateContextInitializationService<TContext>>();
        }

        return services;
    }
}
