using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weaviate.Client.Managed.Context;

namespace Weaviate.Client.Managed.DependencyInjection;

/// <summary>
/// Hosted service that runs schema migration for a WeaviateContext at application startup.
/// </summary>
/// <typeparam name="TContext">The context type to initialize.</typeparam>
internal class WeaviateContextInitializationService<TContext> : IHostedService
    where TContext : WeaviateContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WeaviateContextInitializationService<TContext>> _logger;

    public WeaviateContextInitializationService(
        IServiceProvider serviceProvider,
        ILogger<WeaviateContextInitializationService<TContext>> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Initializing WeaviateContext {ContextType}...",
            typeof(TContext).Name
        );

        try
        {
            // Create a scope to handle both singleton and scoped lifetimes
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TContext>();

            _logger.LogInformation(
                "Running schema migration for {ContextType}...",
                typeof(TContext).Name
            );

            await context
                .Migrate(
                    allowBreakingChanges: context.Options.AllowBreakingMigrations,
                    cancellationToken: cancellationToken
                )
                .ConfigureAwait(false);

            _logger.LogInformation(
                "WeaviateContext {ContextType} initialized successfully.",
                typeof(TContext).Name
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to initialize WeaviateContext {ContextType}",
                typeof(TContext).Name
            );
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
