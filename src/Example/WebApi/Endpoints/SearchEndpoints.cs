using Weaviate.Client.Managed.Models;
using WebApi.Models;

namespace WebApi.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/search").WithTags("Search");

        // POST /api/search - Unified search endpoint
        group
            .MapPost(
                "/",
                async (ProductCatalogContext context, SearchRequest request) =>
                {
                    // Build product query
                    var productQuery = context.Query<Product>();
                    productQuery = request.Mode switch
                    {
                        "semantic" => productQuery.NearText(request.Query),
                        "hybrid" => productQuery.Hybrid(
                            request.Query,
                            alpha: request.Alpha ?? 0.5f
                        ),
                        _ => productQuery.BM25(request.Query),
                    };

                    // Apply filters
                    if (request.Category != null)
                        productQuery = productQuery.Where(p => p.Category == request.Category);

                    if (request.Brand != null)
                        productQuery = productQuery.Where(p => p.Brand == request.Brand);

                    if (request.MinPrice.HasValue)
                        productQuery = productQuery.Where(p => p.Price >= request.MinPrice.Value);

                    if (request.MaxPrice.HasValue)
                        productQuery = productQuery.Where(p => p.Price <= request.MaxPrice.Value);

                    if (request.MinRating.HasValue)
                        productQuery = productQuery.Where(p => p.Rating >= request.MinRating.Value);

                    // Build review query with the same search mode
                    var reviewQuery = context.Query<ProductReview>();
                    reviewQuery = request.Mode switch
                    {
                        "semantic" => reviewQuery.NearText(request.Query),
                        "hybrid" => reviewQuery.Hybrid(request.Query, alpha: request.Alpha ?? 0.5f),
                        _ => reviewQuery.BM25(request.Query),
                    };

                    // Run product and review queries in parallel
                    var productResultsTask = productQuery
                        .Limit((uint)(request.Limit ?? 20))
                        .Offset((uint)(request.Offset ?? 0))
                        .WithMetadata(MetadataOptions.Score | MetadataOptions.Distance)
                        .Execute();

                    var reviewResultsTask = reviewQuery
                        .WithReferences()
                        .Limit((uint)(request.Limit ?? 20))
                        .WithMetadata(MetadataOptions.Score | MetadataOptions.Distance)
                        .Execute();

                    await Task.WhenAll(productResultsTask, reviewResultsTask);

                    var productResults = await productResultsTask;
                    var reviewResults = await reviewResultsTask;

                    // Merge: add products surfaced via review matches not already in product results
                    var matchedUuids = productResults
                        .Where(r => r.UUID.HasValue)
                        .Select(r => r.UUID!.Value)
                        .ToHashSet();

                    var reviewDerivedProducts = reviewResults
                        .Where(r =>
                            r.Object.Product != null
                            && !matchedUuids.Contains(r.Object.Product.UUID)
                            && (
                                request.Category == null
                                || r.Object.Product.Category == request.Category
                            )
                            && (request.Brand == null || r.Object.Product.Brand == request.Brand)
                            && (
                                !request.MinPrice.HasValue
                                || r.Object.Product.Price >= request.MinPrice.Value
                            )
                            && (
                                !request.MaxPrice.HasValue
                                || r.Object.Product.Price <= request.MaxPrice.Value
                            )
                            && (
                                !request.MinRating.HasValue
                                || r.Object.Product.Rating >= request.MinRating.Value
                            )
                        )
                        .DistinctBy(r => r.Object.Product!.UUID)
                        .Select(r => new QueryResult<Product>
                        {
                            UUID = r.Object.Product!.UUID,
                            Object = r.Object.Product!,
                            Metadata = r.Metadata,
                        });

                    return Results.Ok(productResults.Concat(reviewDerivedProducts));
                }
            )
            .WithName("Search");
    }
}

public record SearchRequest(
    string Query,
    string Mode = "hybrid", // "semantic" | "hybrid" | "keyword"
    float? Alpha = 0.5f, // For hybrid: 0=keyword, 1=semantic
    string? Category = null,
    string? Brand = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    double? MinRating = null,
    int? Limit = 20,
    int? Offset = 0
);
