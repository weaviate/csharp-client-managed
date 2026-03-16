using Weaviate.Client.Managed.Attributes;
using Weaviate.Client.Models;

namespace Example;

/// <summary>
/// Represents a product in an e-commerce catalog.
/// </summary>
[WeaviateCollection(Name = "Product", Description = "Product catalog for e-commerce")]
public record Product
{
    [WeaviateUUID]
    public Guid UUID { get; set; }

    [Property(Description = "Product name")]
    public string Name { get; set; } = "";

    [Property(Description = "Product description")]
    [Index(Searchable = true)]
    [Tokenization(PropertyTokenization.Word)]
    public string Description { get; set; } = "";

    [Property(Description = "Product category")]
    [Index(Filterable = true)]
    public string Category { get; set; } = "";

    [Property(DataType.Number, Description = "Product price in USD")]
    [Index(Filterable = true)]
    public decimal Price { get; set; }

    [Property(Description = "Brand name")]
    [Index(Filterable = true)]
    public string Brand { get; set; } = "";

    [Property(DataType.Number, Description = "Average rating (1-5)")]
    [Index(Filterable = true)]
    public double Rating { get; set; }

    [Property(DataType.Int, Description = "Stock quantity")]
    public int Stock { get; set; }

    [Property(Description = "Product specifications")]
    [NestedType(typeof(ProductSpecifications))]
    public ProductSpecifications? Specs { get; set; }

    // Optional: vector for semantic search (can be omitted if no vectorizer)
    // [Vector<Vectorizer.Text2VecCohere>(Name = "description")]
    // public float[]? DescriptionVector { get; set; }

    // Metadata properties
    [MetadataProperty]
    public double? Score { get; set; }

    [MetadataProperty]
    public double? Distance { get; set; }
}

/// <summary>
/// Product specifications as a nested object.
/// </summary>
public record ProductSpecifications
{
    [Property(Description = "Product color")]
    public string? Color { get; set; }

    [Property(Description = "Product size")]
    public string? Size { get; set; }

    [Property(DataType.Number, Description = "Weight in pounds")]
    public double? Weight { get; set; }

    [Property(Description = "Material")]
    public string? Material { get; set; }
}

/// <summary>
/// Represents a customer review for a product, demonstrating cross-collection references.
/// </summary>
[WeaviateCollection(Name = "ProductReview", Description = "Customer reviews for products")]
public record ProductReview
{
    [WeaviateUUID]
    public Guid UUID { get; set; }

    [Property(Description = "Review title")]
    public string Title { get; set; } = "";

    [Property(Description = "Review content")]
    [Index(Searchable = true)]
    [Tokenization(PropertyTokenization.Word)]
    public string Content { get; set; } = "";

    [Property(DataType.Number, Description = "Rating (1-5)")]
    [Index(Filterable = true)]
    public double Rating { get; set; }

    [Property(Description = "Reviewer name")]
    public string ReviewerName { get; set; } = "";

    [Property(Description = "Review date")]
    public DateTime ReviewDate { get; set; }

    // Reference to the Product being reviewed
    // Demonstrates cross-collection relationships
    [Reference]
    public Product? Product { get; set; }

    // Metadata properties
    [MetadataProperty]
    public double? Score { get; set; }
}
