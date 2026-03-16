using Weaviate.Client;
using Weaviate.Client.Managed.Context;
using Weaviate.Client.Managed.Extensions;
using Weaviate.Client.Models;

namespace Example;

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

/// <summary>
/// Reliable example demonstrating WeaviateContext with e-commerce product data.
/// Shows filtering, sorting, BM25 search, nested objects, and cross-collection references.
/// No external file dependencies - all data generated in code.
/// </summary>
public class ProductCatalogExample
{
    public static async Task Run()
    {
        Console.WriteLine("=== Product Catalog Example ===\n");
        Console.WriteLine("Demonstrates WeaviateContext with e-commerce product data");
        Console.WriteLine(
            "Shows filtering, sorting, BM25 search, nested objects, and references\n"
        );

        // Generate sample products in code (no file I/O!)
        var products = GenerateSampleProducts();
        Console.WriteLine($"Generated {products.Count} sample products");

        // Connect and create context
        var client = await Connect.Local();
        var context = new ProductCatalogContext(client);

        // Clean slate
        try
        {
            await context.Client.Collections.Delete("ProductReview");
            await context.Client.Collections.Delete("Product");
            Console.WriteLine("Deleted existing collections");
        }
        catch
        {
            Console.WriteLine("No existing collections to delete");
        }

        // Create schema and insert products
        await context.Migrate();
        await context.Insert(products.ToArray());
        Console.WriteLine($"Inserted {products.Count} products");

        // Generate and insert reviews with references to products
        var reviews = GenerateSampleReviews(products);
        await context.Insert(reviews.ToArray());
        Console.WriteLine($"Inserted {reviews.Count} reviews\n");

        // ===== DEMO 1: Filter by Category =====
        Console.WriteLine("=== DEMO 1: Products in Electronics Category ===");
        var electronics = await context
            .Query<Product>()
            .Where(p => p.Category == "Electronics")
            .Limit(5);

        foreach (var product in electronics.Objects())
        {
            Console.WriteLine($"  {product.Name} - ${product.Price} ({product.Brand})");
        }

        // ===== DEMO 2: Filter by Price Range =====
        Console.WriteLine("\n=== DEMO 2: Products Under $50 ===");
        var affordable = await context
            .Query<Product>()
            .Where(p => p.Price < 50)
            .Sort(p => p.Price, descending: false)
            .Limit(5);

        foreach (var product in affordable.Objects())
        {
            Console.WriteLine($"  {product.Name} - ${product.Price:F2}");
        }

        // ===== DEMO 3: Filter by Rating =====
        Console.WriteLine("\n=== DEMO 3: Highly Rated Products (4.5+) ===");
        var topRated = await context
            .Query<Product>()
            .Where(p => p.Rating >= 4.5)
            .Sort(p => p.Rating, descending: true)
            .Limit(5);

        foreach (var product in topRated.Objects())
        {
            Console.WriteLine($"  {product.Name} - {product.Rating:F1}★ (${product.Price})");
        }

        // ===== DEMO 4: BM25 Keyword Search =====
        Console.WriteLine("\n=== DEMO 4: BM25 Search: 'wireless' ===");
        var wirelessProducts = await context
            .Query<Product>()
            .BM25(query: "wireless")
            .WithMetadata(MetadataOptions.Score)
            .Limit(5)
            .Execute();

        foreach (var result in wirelessProducts)
        {
            Console.WriteLine($"  Score: {result.Metadata?.Score:F3} | {result.Object.Name}");
        }

        // ===== DEMO 5: Nested Property Filtering =====
        Console.WriteLine("\n=== DEMO 5: Blue Products ===");
        var blueProducts = await context
            .Query<Product>()
            .Where(p => p.Specs != null && p.Specs.Color == "Blue")
            .Limit(5);

        foreach (var product in blueProducts.Objects())
        {
            Console.WriteLine($"  {product.Name} - {product.Specs?.Color} (${product.Price})");
        }

        // ===== DEMO 6: Complex Multi-Filter =====
        Console.WriteLine("\n=== DEMO 6: High-Rated Electronics Under $200 ===");
        var complexQuery = await context
            .Query<Product>()
            .Where(p => p.Category == "Electronics")
            .Where(p => p.Price < 200)
            .Where(p => p.Rating >= 4.0)
            .Sort(p => p.Rating, descending: true)
            .Limit(5);

        foreach (var product in complexQuery.Objects())
        {
            Console.WriteLine($"  {product.Name} - {product.Rating:F1}★ - ${product.Price}");
        }

        // ===== DEMO 7: References WITH Hydration (Eager Loading) =====
        Console.WriteLine("\n=== DEMO 7: Product Reviews WITH References (Eager Loading) ===");
        var reviewsWithProducts = await context
            .Query<ProductReview>()
            .Where(r => r.Rating >= 4.0)
            .WithReferences() // Hydrates the Product reference
            .Limit(5);

        foreach (var review in reviewsWithProducts.Objects())
        {
            var product = review.Product; // Fully hydrated Product object!
            Console.WriteLine($"  '{review.Title}' ({review.Rating}★) by {review.ReviewerName}");
            Console.WriteLine($"    Product: {product?.Name} (${product?.Price})");
        }

        // ===== DEMO 8: References WITHOUT Hydration (Explicit Loading) =====
        Console.WriteLine(
            "\n=== DEMO 8: Product Reviews WITHOUT References (Explicit Loading) ==="
        );
        var reviewsWithoutProducts = await context.Query<ProductReview>().Limit(3); // No WithReferences() - Product will be null

        foreach (var review in reviewsWithoutProducts.Objects())
        {
            Console.WriteLine($"  '{review.Title}' ({review.Rating}★)");
            Console.WriteLine($"    Product UUID: {review.Product?.UUID ?? Guid.Empty}");
            Console.WriteLine(
                $"    Product object: {(review.Product == null || string.IsNullOrEmpty(review.Product.Name) ? "NOT loaded" : "Loaded")}"
            );
        }

        // ===== DEMO 9: Count Operations =====
        Console.WriteLine("\n=== DEMO 9: Count Operations ===");
        var productCount = await context.Count<Product>();
        var reviewCount = await context.Count<ProductReview>();
        Console.WriteLine($"Total products: {productCount}");
        Console.WriteLine($"Total reviews: {reviewCount}");

        // ===== DEMO 10: Iterator Usage =====
        Console.WriteLine("\n=== DEMO 10: Iterator: Calculate Average Price ===");
        var allProducts = context.Iterator<Product>(cacheSize: 10);
        var avgPrice = await allProducts.AverageAsync(p => p.Price);
        Console.WriteLine($"Average product price: ${avgPrice:F2}");

        // Cleanup
        await context.DisposeAsync();
        client.Dispose();
        Console.WriteLine("\n=== Example Complete ===");
    }

    private static List<Product> GenerateSampleProducts()
    {
        var products = new List<Product>();

        // Electronics (10 products)
        products.AddRange([
            new Product
            {
                Name = "Wireless Mouse",
                Description = "Ergonomic wireless mouse with adjustable DPI and long battery life",
                Category = "Electronics",
                Price = 29.99m,
                Brand = "TechPro",
                Rating = 4.5,
                Stock = 150,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 0.2,
                    Material = "Plastic",
                },
            },
            new Product
            {
                Name = "Mechanical Keyboard",
                Description =
                    "RGB mechanical gaming keyboard with Cherry MX switches and programmable keys",
                Category = "Electronics",
                Price = 129.99m,
                Brand = "GamerGear",
                Rating = 4.8,
                Stock = 75,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 2.1,
                    Material = "Aluminum",
                },
            },
            new Product
            {
                Name = "USB-C Hub",
                Description =
                    "7-in-1 USB-C hub with HDMI, USB 3.0, SD card reader, and power delivery",
                Category = "Electronics",
                Price = 49.99m,
                Brand = "ConnectPlus",
                Rating = 4.3,
                Stock = 200,
                Specs = new ProductSpecifications
                {
                    Color = "Gray",
                    Weight = 0.3,
                    Material = "Aluminum",
                },
            },
            new Product
            {
                Name = "Bluetooth Speaker",
                Description =
                    "Portable waterproof Bluetooth speaker with 360-degree sound and 12-hour battery",
                Category = "Electronics",
                Price = 79.99m,
                Brand = "SoundWave",
                Rating = 4.6,
                Stock = 120,
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Weight = 1.5,
                    Material = "Silicone",
                },
            },
            new Product
            {
                Name = "Webcam",
                Description =
                    "1080p HD webcam with built-in microphone and auto-focus for video calls",
                Category = "Electronics",
                Price = 69.99m,
                Brand = "ViewTech",
                Rating = 4.4,
                Stock = 90,
                Specs = new ProductSpecifications { Color = "Black", Weight = 0.5 },
            },
            new Product
            {
                Name = "Laptop Stand",
                Description =
                    "Adjustable aluminum laptop stand with ergonomic design for better posture",
                Category = "Electronics",
                Price = 39.99m,
                Brand = "DeskMate",
                Rating = 4.7,
                Stock = 180,
                Specs = new ProductSpecifications
                {
                    Color = "Silver",
                    Weight = 1.2,
                    Material = "Aluminum",
                },
            },
            new Product
            {
                Name = "Phone Charger",
                Description =
                    "Fast-charging USB-C wall charger with 30W power delivery and foldable plug",
                Category = "Electronics",
                Price = 24.99m,
                Brand = "PowerUp",
                Rating = 4.5,
                Stock = 250,
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Weight = 0.2,
                    Material = "Plastic",
                },
            },
            new Product
            {
                Name = "HDMI Cable",
                Description = "6ft 4K HDMI cable with gold-plated connectors and Ethernet support",
                Category = "Electronics",
                Price = 14.99m,
                Brand = "CableMax",
                Rating = 4.2,
                Stock = 300,
                Specs = new ProductSpecifications { Color = "Black", Material = "Braided nylon" },
            },
            new Product
            {
                Name = "Gaming Headset",
                Description = "7.1 surround sound gaming headset with noise-cancelling microphone",
                Category = "Electronics",
                Price = 89.99m,
                Brand = "GamerGear",
                Rating = 4.6,
                Stock = 110,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 0.8,
                    Material = "Plastic",
                },
            },
            new Product
            {
                Name = "Portable SSD",
                Description =
                    "1TB portable SSD with USB 3.1 Gen 2 for fast file transfers up to 1050MB/s",
                Category = "Electronics",
                Price = 149.99m,
                Brand = "DataVault",
                Rating = 4.9,
                Stock = 85,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 0.1,
                    Material = "Metal",
                },
            },
        ]);

        // Clothing (10 products)
        products.AddRange([
            new Product
            {
                Name = "Cotton T-Shirt",
                Description = "Soft 100% cotton t-shirt with classic fit and crew neck",
                Category = "Clothing",
                Price = 19.99m,
                Brand = "ComfortWear",
                Rating = 4.4,
                Stock = 200,
                Specs = new ProductSpecifications
                {
                    Color = "Navy",
                    Size = "M",
                    Material = "Cotton",
                },
            },
            new Product
            {
                Name = "Denim Jeans",
                Description = "Classic fit denim jeans with stretch for comfort and durability",
                Category = "Clothing",
                Price = 59.99m,
                Brand = "DenimCo",
                Rating = 4.6,
                Stock = 150,
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Size = "32x32",
                    Material = "Denim",
                },
            },
            new Product
            {
                Name = "Hoodie",
                Description =
                    "Cozy fleece-lined hoodie with kangaroo pocket and adjustable drawstring",
                Category = "Clothing",
                Price = 44.99m,
                Brand = "UrbanStyle",
                Rating = 4.7,
                Stock = 120,
                Specs = new ProductSpecifications
                {
                    Color = "Gray",
                    Size = "L",
                    Material = "Cotton blend",
                },
            },
            new Product
            {
                Name = "Running Shoes",
                Description =
                    "Lightweight running shoes with responsive cushioning and breathable mesh",
                Category = "Clothing",
                Price = 89.99m,
                Brand = "ActiveFit",
                Rating = 4.8,
                Stock = 100,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Size = "10",
                    Material = "Mesh",
                },
            },
            new Product
            {
                Name = "Winter Jacket",
                Description =
                    "Insulated winter jacket with water-resistant outer shell and multiple pockets",
                Category = "Clothing",
                Price = 129.99m,
                Brand = "OutdoorGear",
                Rating = 4.5,
                Stock = 80,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Size = "M",
                    Material = "Polyester",
                },
            },
            new Product
            {
                Name = "Baseball Cap",
                Description = "Adjustable baseball cap with embroidered logo and curved brim",
                Category = "Clothing",
                Price = 24.99m,
                Brand = "CapStyle",
                Rating = 4.3,
                Stock = 180,
                Specs = new ProductSpecifications { Color = "Navy", Material = "Cotton" },
            },
            new Product
            {
                Name = "Socks (3-pack)",
                Description =
                    "Moisture-wicking athletic socks with arch support and cushioned sole",
                Category = "Clothing",
                Price = 14.99m,
                Brand = "ActiveFit",
                Rating = 4.6,
                Stock = 250,
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Size = "L",
                    Material = "Cotton blend",
                },
            },
            new Product
            {
                Name = "Yoga Pants",
                Description =
                    "High-waisted yoga pants with four-way stretch and moisture-wicking fabric",
                Category = "Clothing",
                Price = 49.99m,
                Brand = "FlexWear",
                Rating = 4.7,
                Stock = 140,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Size = "M",
                    Material = "Spandex blend",
                },
            },
            new Product
            {
                Name = "Dress Shirt",
                Description = "Wrinkle-free dress shirt with button-down collar and classic fit",
                Category = "Clothing",
                Price = 39.99m,
                Brand = "FormalStyle",
                Rating = 4.4,
                Stock = 110,
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Size = "M",
                    Material = "Cotton",
                },
            },
            new Product
            {
                Name = "Sneakers",
                Description = "Casual canvas sneakers with cushioned insole and rubber outsole",
                Category = "Clothing",
                Price = 54.99m,
                Brand = "UrbanStyle",
                Rating = 4.5,
                Stock = 130,
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Size = "9",
                    Material = "Canvas",
                },
            },
        ]);

        // Home & Garden (10 products)
        products.AddRange([
            new Product
            {
                Name = "Coffee Maker",
                Description = "Programmable coffee maker with 12-cup capacity and auto-shutoff",
                Category = "Home & Garden",
                Price = 69.99m,
                Brand = "BrewMaster",
                Rating = 4.5,
                Stock = 95,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 5.0,
                    Material = "Plastic",
                },
            },
            new Product
            {
                Name = "Blender",
                Description = "High-powered blender with 1000W motor and multiple speed settings",
                Category = "Home & Garden",
                Price = 99.99m,
                Brand = "BlendTech",
                Rating = 4.7,
                Stock = 80,
                Specs = new ProductSpecifications
                {
                    Color = "Silver",
                    Weight = 8.0,
                    Material = "Stainless steel",
                },
            },
            new Product
            {
                Name = "Air Purifier",
                Description =
                    "HEPA air purifier with activated carbon filter for rooms up to 500 sq ft",
                Category = "Home & Garden",
                Price = 179.99m,
                Brand = "PureAir",
                Rating = 4.6,
                Stock = 60,
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Weight = 12.0,
                    Material = "Plastic",
                },
            },
            new Product
            {
                Name = "Desk Lamp",
                Description =
                    "LED desk lamp with adjustable brightness, color temperature, and USB charging port",
                Category = "Home & Garden",
                Price = 44.99m,
                Brand = "LightWorks",
                Rating = 4.5,
                Stock = 120,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 2.0,
                    Material = "Metal",
                },
            },
            new Product
            {
                Name = "Throw Pillow",
                Description =
                    "Decorative throw pillow with soft velvet cover and hypoallergenic filling",
                Category = "Home & Garden",
                Price = 22.99m,
                Brand = "HomeCom fort",
                Rating = 4.3,
                Stock = 200,
                Specs = new ProductSpecifications
                {
                    Color = "Gray",
                    Size = "18x18",
                    Material = "Velvet",
                },
            },
            new Product
            {
                Name = "Plant Pot",
                Description = "Ceramic plant pot with drainage hole and matching saucer",
                Category = "Home & Garden",
                Price = 18.99m,
                Brand = "GreenThumb",
                Rating = 4.4,
                Stock = 150,
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Size = "8-inch",
                    Material = "Ceramic",
                },
            },
            new Product
            {
                Name = "Kitchen Knife Set",
                Description =
                    "6-piece stainless steel knife set with wooden block and sharpening rod",
                Category = "Home & Garden",
                Price = 89.99m,
                Brand = "ChefPro",
                Rating = 4.8,
                Stock = 70,
                Specs = new ProductSpecifications { Material = "Stainless steel", Weight = 4.0 },
            },
            new Product
            {
                Name = "Vacuum Cleaner",
                Description =
                    "Bagless upright vacuum cleaner with HEPA filter and pet hair attachment",
                Category = "Home & Garden",
                Price = 199.99m,
                Brand = "CleanHome",
                Rating = 4.5,
                Stock = 50,
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Weight = 15.0,
                    Material = "Plastic",
                },
            },
            new Product
            {
                Name = "Bath Towel Set",
                Description =
                    "4-piece bath towel set made from 100% Turkish cotton, ultra-absorbent",
                Category = "Home & Garden",
                Price = 49.99m,
                Brand = "LuxuryLinens",
                Rating = 4.6,
                Stock = 130,
                Specs = new ProductSpecifications { Color = "Navy", Material = "Turkish cotton" },
            },
            new Product
            {
                Name = "Picture Frame",
                Description = "8x10 wooden picture frame with glass front and hanging hardware",
                Category = "Home & Garden",
                Price = 16.99m,
                Brand = "FrameIt",
                Rating = 4.2,
                Stock = 180,
                Specs = new ProductSpecifications
                {
                    Color = "Walnut",
                    Size = "8x10",
                    Material = "Wood",
                },
            },
        ]);

        // Books (10 products)
        products.AddRange([
            new Product
            {
                Name = "The Pragmatic Programmer",
                Description =
                    "Classic guide to software development best practices and professional growth",
                Category = "Books",
                Price = 44.99m,
                Brand = "Addison-Wesley",
                Rating = 4.8,
                Stock = 75,
            },
            new Product
            {
                Name = "Clean Code",
                Description = "Essential handbook for writing maintainable and readable code",
                Category = "Books",
                Price = 42.99m,
                Brand = "Prentice Hall",
                Rating = 4.7,
                Stock = 90,
            },
            new Product
            {
                Name = "Design Patterns",
                Description =
                    "Foundational book on software design patterns and object-oriented programming",
                Category = "Books",
                Price = 54.99m,
                Brand = "Addison-Wesley",
                Rating = 4.6,
                Stock = 60,
            },
            new Product
            {
                Name = "Refactoring",
                Description =
                    "Comprehensive guide to improving code structure without changing behavior",
                Category = "Books",
                Price = 46.99m,
                Brand = "Addison-Wesley",
                Rating = 4.7,
                Stock = 70,
            },
            new Product
            {
                Name = "Domain-Driven Design",
                Description = "Strategic approach to tackling complexity in software development",
                Category = "Books",
                Price = 56.99m,
                Brand = "Addison-Wesley",
                Rating = 4.5,
                Stock = 55,
            },
            new Product
            {
                Name = "C# in Depth",
                Description =
                    "Deep dive into C# language features and best practices for .NET developers",
                Category = "Books",
                Price = 49.99m,
                Brand = "Manning",
                Rating = 4.8,
                Stock = 65,
            },
            new Product
            {
                Name = "Effective C#",
                Description = "50 specific ways to improve your C# programming skills",
                Category = "Books",
                Price = 44.99m,
                Brand = "Addison-Wesley",
                Rating = 4.6,
                Stock = 75,
            },
            new Product
            {
                Name = "Code Complete",
                Description =
                    "Comprehensive manual on software construction and practical guidelines",
                Category = "Books",
                Price = 49.99m,
                Brand = "Microsoft Press",
                Rating = 4.7,
                Stock = 80,
            },
            new Product
            {
                Name = "Head First Design Patterns",
                Description =
                    "Brain-friendly guide to software design patterns with visual explanations",
                Category = "Books",
                Price = 39.99m,
                Brand = "O'Reilly",
                Rating = 4.5,
                Stock = 95,
            },
            new Product
            {
                Name = "Working Effectively with Legacy Code",
                Description = "Strategies for safely modifying and improving existing codebases",
                Category = "Books",
                Price = 47.99m,
                Brand = "Prentice Hall",
                Rating = 4.6,
                Stock = 70,
            },
        ]);

        // Sports (10 products)
        products.AddRange([
            new Product
            {
                Name = "Yoga Mat",
                Description = "Non-slip yoga mat with extra cushioning for comfort during practice",
                Category = "Sports",
                Price = 34.99m,
                Brand = "YogaFlow",
                Rating = 4.6,
                Stock = 110,
                Specs = new ProductSpecifications
                {
                    Color = "Purple",
                    Size = "68x24",
                    Material = "TPE",
                },
            },
            new Product
            {
                Name = "Resistance Bands",
                Description =
                    "Set of 5 resistance bands with varying strengths for strength training",
                Category = "Sports",
                Price = 24.99m,
                Brand = "FitStrong",
                Rating = 4.5,
                Stock = 150,
                Specs = new ProductSpecifications { Material = "Latex" },
            },
            new Product
            {
                Name = "Dumbbell Set",
                Description =
                    "Adjustable dumbbell set with 5-50 lbs weight range and compact design",
                Category = "Sports",
                Price = 299.99m,
                Brand = "PowerLift",
                Rating = 4.8,
                Stock = 40,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 55.0,
                    Material = "Steel",
                },
            },
            new Product
            {
                Name = "Jump Rope",
                Description = "Speed jump rope with ball bearings and adjustable length for cardio",
                Category = "Sports",
                Price = 12.99m,
                Brand = "CardioMax",
                Rating = 4.3,
                Stock = 200,
                Specs = new ProductSpecifications { Color = "Black", Material = "PVC" },
            },
            new Product
            {
                Name = "Water Bottle",
                Description =
                    "Insulated stainless steel water bottle that keeps drinks cold for 24 hours",
                Category = "Sports",
                Price = 27.99m,
                Brand = "HydroFlow",
                Rating = 4.7,
                Stock = 170,
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Size = "32oz",
                    Material = "Stainless steel",
                },
            },
            new Product
            {
                Name = "Foam Roller",
                Description =
                    "High-density foam roller for muscle recovery and deep tissue massage",
                Category = "Sports",
                Price = 29.99m,
                Brand = "RecoverPro",
                Rating = 4.5,
                Stock = 130,
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Size = "18-inch",
                    Material = "EVA foam",
                },
            },
            new Product
            {
                Name = "Exercise Ball",
                Description = "Anti-burst exercise ball with pump for core training and stability",
                Category = "Sports",
                Price = 19.99m,
                Brand = "CoreFit",
                Rating = 4.4,
                Stock = 145,
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Size = "65cm",
                    Material = "PVC",
                },
            },
            new Product
            {
                Name = "Gym Bag",
                Description =
                    "Durable gym bag with separate shoe compartment and water bottle holder",
                Category = "Sports",
                Price = 44.99m,
                Brand = "ActiveGear",
                Rating = 4.5,
                Stock = 100,
                Specs = new ProductSpecifications { Color = "Black", Material = "Polyester" },
            },
            new Product
            {
                Name = "Running Belt",
                Description = "Adjustable running belt with expandable pocket for phone and keys",
                Category = "Sports",
                Price = 16.99m,
                Brand = "RunFree",
                Rating = 4.3,
                Stock = 180,
                Specs = new ProductSpecifications { Color = "Black", Material = "Nylon" },
            },
            new Product
            {
                Name = "Fitness Tracker",
                Description =
                    "Waterproof fitness tracker with heart rate monitor and sleep tracking",
                Category = "Sports",
                Price = 79.99m,
                Brand = "FitTech",
                Rating = 4.6,
                Stock = 90,
                Specs = new ProductSpecifications { Color = "Black", Material = "Silicone" },
            },
        ]);

        return products;
    }

    private static List<ProductReview> GenerateSampleReviews(List<Product> products)
    {
        var reviews = new List<ProductReview>();
        var random = new Random(42); // Fixed seed for reproducibility

        // Select products to review (select 10 random products for reviews)
        var productsToReview = products.OrderBy(_ => random.Next()).Take(10).ToList();

        // Reviewer names
        var reviewerNames = new[]
        {
            "John D.",
            "Sarah M.",
            "Mike R.",
            "Emily K.",
            "David L.",
            "Jessica W.",
            "Tom H.",
            "Amanda B.",
            "Chris P.",
            "Lisa T.",
        };

        // Positive review templates (4-5 stars)
        var positiveReviews = new[]
        {
            (
                "Great purchase!",
                "This product exceeded my expectations. The quality is excellent and it works perfectly for my needs."
            ),
            (
                "Excellent quality",
                "Very satisfied with this purchase. The build quality is solid and it performs as advertised."
            ),
            (
                "Highly recommended",
                "Would definitely recommend this to anyone. It's reliable and well worth the price."
            ),
            (
                "Perfect for my needs",
                "Exactly what I was looking for. Great value for money and very functional."
            ),
            (
                "Love it!",
                "Absolutely love this product. It's become an essential part of my daily routine."
            ),
        };

        // Neutral/negative review templates (2-3 stars)
        var neutralReviews = new[]
        {
            (
                "It's okay",
                "The product is decent but has some minor issues. It works but could be better for the price."
            ),
            (
                "Average product",
                "Nothing special about this product. It does the job but there are better options available."
            ),
            (
                "Not bad, not great",
                "It's an okay product. Some features are good, others could use improvement."
            ),
        };

        int reviewIndex = 0;
        foreach (var product in productsToReview)
        {
            // Generate 1-3 reviews per product
            int numReviews = random.Next(1, 4);

            for (int i = 0; i < numReviews; i++)
            {
                bool isPositive = random.NextDouble() > 0.3; // 70% positive reviews
                var (title, content) = isPositive
                    ? positiveReviews[random.Next(positiveReviews.Length)]
                    : neutralReviews[random.Next(neutralReviews.Length)];

                double rating = isPositive ? 4.0 + random.NextDouble() : 2.5 + random.NextDouble();

                reviews.Add(
                    new ProductReview
                    {
                        Title = title,
                        Content = content,
                        Rating = Math.Round(rating, 1),
                        ReviewerName = reviewerNames[reviewIndex % reviewerNames.Length],
                        ReviewDate = DateTime.Now.AddDays(-random.Next(1, 180)),
                        Product = product, // Reference to the product
                    }
                );

                reviewIndex++;
            }
        }

        return reviews;
    }
}
