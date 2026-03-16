using WebApi.Models;

namespace WebApi.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reviews").WithTags("Reviews");

        // GET /api/reviews - List reviews with filters
        group
            .MapGet(
                "/",
                async (
                    ProductCatalogContext context,
                    Guid? productId = null,
                    double? minRating = null,
                    string? searchText = null,
                    int limit = 20
                ) =>
                {
                    var query = context.Query<ProductReview>();

                    if (minRating.HasValue)
                        query = query.Where(r => r.Rating >= minRating.Value);

                    if (searchText != null)
                        query = query.BM25(searchText);

                    if (productId.HasValue)
                        query = query.Where(r => r.Product!.UUID == productId.Value);

                    var results = await query
                        .WithReferences(r => r.Product!)
                        .Limit((uint)limit)
                        .Execute();
                    var reviews = results.Select(r => r.Object);

                    return Results.Ok(reviews);
                }
            )
            .WithName("GetReviews");

        // GET /api/reviews/product/{productId} - Get reviews for a specific product
        group
            .MapGet(
                "/product/{productId:guid}",
                async (ProductCatalogContext context, Guid productId) =>
                {
                    var results = await context
                        .Query<ProductReview>()
                        .Where(r => r.Product!.UUID == productId)
                        .Limit(100)
                        .Execute();

                    var reviews = results
                        .Select(r => r.Object)
                        .OrderByDescending(r => r.ReviewDate)
                        .ToList();

                    return Results.Ok(reviews);
                }
            )
            .WithName("GetProductReviews");

        // POST /api/reviews - Create a new review
        group
            .MapPost(
                "/",
                async (ProductCatalogContext context, ProductReviewRequest request) =>
                {
                    // Validate request
                    if (string.IsNullOrWhiteSpace(request.Title))
                        return Results.BadRequest(new { error = "Title is required" });

                    if (string.IsNullOrWhiteSpace(request.Content))
                        return Results.BadRequest(new { error = "Content is required" });

                    if (request.Rating < 1 || request.Rating > 5)
                        return Results.BadRequest(new { error = "Rating must be between 1 and 5" });

                    if (string.IsNullOrWhiteSpace(request.ReviewerName))
                        return Results.BadRequest(new { error = "Reviewer name is required" });

                    // Verify product exists
                    var product = await context.Products.Find(request.ProductId);
                    if (product == null)
                        return Results.NotFound(new { error = "Product not found" });

                    // Create review
                    var review = new ProductReview
                    {
                        UUID = Guid.NewGuid(),
                        Title = request.Title,
                        Content = request.Content,
                        Rating = request.Rating,
                        ReviewerName = request.ReviewerName,
                        ReviewDate = DateTime.UtcNow,
                        Product = product,
                    };

                    await context.Insert(review);

                    // Update product average rating (simple calculation from sampled reviews)
                    var productReviews = (
                        await context
                            .Query<ProductReview>()
                            .Where(r => r.Product!.UUID == request.ProductId)
                            .Limit(1000)
                            .Execute()
                    )
                        .Select(r => r.Object)
                        .ToList();

                    if (productReviews.Any())
                    {
                        var avgRating = productReviews.Average(r => r.Rating);
                        product.Rating = Math.Round(avgRating, 1);

                        // Update product (by deleting and reinserting with same UUID)
                        await context.Products.Delete(product.UUID);
                        await context.Insert(product);
                    }

                    return Results.Created($"/api/reviews/{review.UUID}", review);
                }
            )
            .WithName("CreateReview");
    }
}

public record ProductReviewRequest(
    Guid ProductId,
    string Title,
    string Content,
    double Rating,
    string ReviewerName
);
