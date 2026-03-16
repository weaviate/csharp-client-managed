using WebApi.Models;

namespace WebApi.Models;

/// <summary>
/// WeaviateContext for product catalog with products and reviews.
/// </summary>
public class ProductCatalogContext : WeaviateContext
{
    public ProductCatalogContext(WeaviateClient client)
        : base(client) { }

    public CollectionSet<Product> Products { get; set; } = null!;
    public CollectionSet<ProductReview> Reviews { get; set; } = null!;
}
