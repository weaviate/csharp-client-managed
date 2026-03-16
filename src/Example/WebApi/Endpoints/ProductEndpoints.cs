using WebApi.Models;

namespace WebApi.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        // GET /api/products - List products with optional filters
        group
            .MapGet(
                "/",
                async (
                    ProductCatalogContext context,
                    string? category = null,
                    decimal? minPrice = null,
                    decimal? maxPrice = null,
                    double? minRating = null,
                    int limit = 20,
                    int offset = 0
                ) =>
                {
                    var query = context.Query<Product>();

                    if (category != null)
                        query = query.Where(p => p.Category == category);

                    if (minPrice.HasValue)
                        query = query.Where(p => p.Price >= minPrice.Value);

                    if (maxPrice.HasValue)
                        query = query.Where(p => p.Price <= maxPrice.Value);

                    if (minRating.HasValue)
                        query = query.Where(p => p.Rating >= minRating.Value);

                    var results = await query.Limit((uint)limit).Offset((uint)offset).Execute();

                    return Results.Ok(results.Select(r => r.Object));
                }
            )
            .WithName("GetProducts");

        // GET /api/products/{id} - Get single product
        group
            .MapGet(
                "/{id:guid}",
                async (ProductCatalogContext context, Guid id) =>
                {
                    var product = await context.Query<Product>().Find(id);

                    return product != null ? Results.Ok(product) : Results.NotFound();
                }
            )
            .WithName("GetProduct");

        // GET /api/products/{id}/similar - Get similar products
        group
            .MapGet(
                "/{id:guid}/similar",
                async (ProductCatalogContext context, Guid id, int limit = 5) =>
                {
                    var results = await context
                        .Query<Product>()
                        .NearObject(id)
                        .Limit((uint)limit)
                        .WithMetadata(MetadataOptions.Distance)
                        .Execute();

                    return Results.Ok(results);
                }
            )
            .WithName("GetSimilarProducts");

        // GET /api/products/facets - Get facets (categories with counts)
        group
            .MapGet(
                "/facets",
                async (ProductCatalogContext context) =>
                {
                    // Get all products for aggregation
                    var products = await context.Query<Product>().Execute();

                    // Calculate category facets
                    var categoryFacets = products
                        .GroupBy(r => r.Object.Category)
                        .Select(g => new { category = g.Key, count = g.Count() })
                        .OrderByDescending(f => f.count)
                        .ToList();

                    // Calculate brand facets
                    var brandFacets = products
                        .GroupBy(r => r.Object.Brand)
                        .Select(g => new { brand = g.Key, count = g.Count() })
                        .OrderByDescending(f => f.count)
                        .ToList();

                    return Results.Ok(new { categories = categoryFacets, brands = brandFacets });
                }
            )
            .WithName("GetFacets");
    }
}
