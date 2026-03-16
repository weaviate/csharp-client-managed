using WebApi.Models;

namespace WebApi.Services;

public class DataSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DataSeeder> logger
    )
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ProductCatalogContext>();

        if (_configuration.GetValue<bool>("Seed:Reset"))
        {
            _logger.LogInformation("Seed:Reset is set — wiping existing data...");

            // Delete reviews first (they reference products)
            await context.Client.Collections.Delete("ProductReview");
            await context.Client.Collections.Delete("Product");

            // Recreate schema
            await context.Migrate();

            _logger.LogInformation("Collections recreated, seeding fresh data...");
        }
        else
        {
            var existingProductCount = await context.Count<Product>();
            var existingReviewCount = await context.Count<ProductReview>();

            if (existingProductCount > 0 && existingReviewCount > 0)
            {
                _logger.LogInformation(
                    "Data already seeded ({ProductCount} products, {ReviewCount} reviews)",
                    existingProductCount,
                    existingReviewCount
                );
                return;
            }

            if (existingProductCount > 0)
            {
                // Products already exist but reviews are missing — seed reviews only
                _logger.LogInformation("Products exist, seeding missing reviews...");
                var results = await context.Products.Query().Limit(1000).Execute();
                var products = results.Select(r => r.Object).ToList();
                var reviews = ProductDataGenerator.GenerateSampleReviews(products);
                await context.Insert(reviews.ToArray());
                _logger.LogInformation(
                    "Seeded {ReviewCount} reviews for {ProductCount} products",
                    reviews.Count,
                    products.Count
                );
                return;
            }
        }

        _logger.LogInformation("Seeding product catalog...");
        var freshProducts = ProductDataGenerator.GenerateSampleProducts();
        await context.Insert(freshProducts.ToArray());

        // Query back products to get their UUIDs
        var productResults = await context.Products.Query().Limit(1000).Execute();
        var productsWithUUIDs = productResults.Select(r => r.Object).ToList();

        var freshReviews = ProductDataGenerator.GenerateSampleReviews(productsWithUUIDs);
        await context.Insert(freshReviews.ToArray());

        _logger.LogInformation(
            "Seeded {ProductCount} products and {ReviewCount} reviews",
            freshProducts.Count,
            freshReviews.Count
        );
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
