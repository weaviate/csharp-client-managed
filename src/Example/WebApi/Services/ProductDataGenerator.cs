using WebApi.Models;

namespace WebApi.Services;

public static class ProductDataGenerator
{
    public static List<Product> GenerateSampleProducts()
    {
        var products = new List<Product>();
        int imageIndex = 1;

        // Electronics (10 products)
        products.AddRange([
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Wireless Mouse",
                Description = "Ergonomic wireless mouse with adjustable DPI and long battery life",
                Category = "Electronics",
                Price = 29.99m,
                Brand = "TechPro",
                Rating = 4.5,
                Stock = 150,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 0.2,
                    Material = "Plastic",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Mechanical Keyboard",
                Description =
                    "RGB mechanical gaming keyboard with Cherry MX switches and programmable keys",
                Category = "Electronics",
                Price = 129.99m,
                Brand = "GamerGear",
                Rating = 4.8,
                Stock = 75,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 2.1,
                    Material = "Aluminum",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "USB-C Hub",
                Description =
                    "7-in-1 USB-C hub with HDMI, USB 3.0, SD card reader, and power delivery",
                Category = "Electronics",
                Price = 49.99m,
                Brand = "ConnectPlus",
                Rating = 4.3,
                Stock = 200,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Gray",
                    Weight = 0.3,
                    Material = "Aluminum",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Bluetooth Speaker",
                Description =
                    "Portable waterproof Bluetooth speaker with 360-degree sound and 12-hour battery",
                Category = "Electronics",
                Price = 79.99m,
                Brand = "SoundWave",
                Rating = 4.6,
                Stock = 120,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Weight = 1.5,
                    Material = "Silicone",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Webcam",
                Description =
                    "1080p HD webcam with built-in microphone and auto-focus for video calls",
                Category = "Electronics",
                Price = 69.99m,
                Brand = "ViewTech",
                Rating = 4.4,
                Stock = 90,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Color = "Black", Weight = 0.5 },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Laptop Stand",
                Description =
                    "Adjustable aluminum laptop stand with ergonomic design for better posture",
                Category = "Electronics",
                Price = 39.99m,
                Brand = "DeskMate",
                Rating = 4.7,
                Stock = 180,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Silver",
                    Weight = 1.2,
                    Material = "Aluminum",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Phone Charger",
                Description =
                    "Fast-charging USB-C wall charger with 30W power delivery and foldable plug",
                Category = "Electronics",
                Price = 24.99m,
                Brand = "PowerUp",
                Rating = 4.5,
                Stock = 250,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Weight = 0.2,
                    Material = "Plastic",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "HDMI Cable",
                Description = "6ft 4K HDMI cable with gold-plated connectors and Ethernet support",
                Category = "Electronics",
                Price = 14.99m,
                Brand = "CableMax",
                Rating = 4.2,
                Stock = 300,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Color = "Black", Material = "Braided nylon" },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Gaming Headset",
                Description = "7.1 surround sound gaming headset with noise-cancelling microphone",
                Category = "Electronics",
                Price = 89.99m,
                Brand = "GamerGear",
                Rating = 4.6,
                Stock = 110,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 0.8,
                    Material = "Plastic",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Portable SSD",
                Description =
                    "1TB portable SSD with USB 3.1 Gen 2 for fast file transfers up to 1050MB/s",
                Category = "Electronics",
                Price = 149.99m,
                Brand = "DataVault",
                Rating = 4.9,
                Stock = 85,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
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
                UUID = Guid.NewGuid(),
                Name = "Cotton T-Shirt",
                Description = "Soft 100% cotton t-shirt with classic fit and crew neck",
                Category = "Clothing",
                Price = 19.99m,
                Brand = "ComfortWear",
                Rating = 4.4,
                Stock = 200,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Navy",
                    Size = "M",
                    Material = "Cotton",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Denim Jeans",
                Description = "Classic fit denim jeans with stretch for comfort and durability",
                Category = "Clothing",
                Price = 59.99m,
                Brand = "DenimCo",
                Rating = 4.6,
                Stock = 150,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Size = "32x32",
                    Material = "Denim",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Hoodie",
                Description =
                    "Cozy fleece-lined hoodie with kangaroo pocket and adjustable drawstring",
                Category = "Clothing",
                Price = 44.99m,
                Brand = "UrbanStyle",
                Rating = 4.7,
                Stock = 120,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Gray",
                    Size = "L",
                    Material = "Cotton blend",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Running Shoes",
                Description =
                    "Lightweight running shoes with responsive cushioning and breathable mesh",
                Category = "Clothing",
                Price = 89.99m,
                Brand = "ActiveFit",
                Rating = 4.8,
                Stock = 100,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Size = "10",
                    Material = "Mesh",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Winter Jacket",
                Description =
                    "Insulated winter jacket with water-resistant outer shell and multiple pockets",
                Category = "Clothing",
                Price = 129.99m,
                Brand = "OutdoorGear",
                Rating = 4.5,
                Stock = 80,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Size = "M",
                    Material = "Polyester",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Baseball Cap",
                Description = "Adjustable baseball cap with embroidered logo and curved brim",
                Category = "Clothing",
                Price = 24.99m,
                Brand = "CapStyle",
                Rating = 4.3,
                Stock = 180,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Color = "Navy", Material = "Cotton" },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Socks (3-pack)",
                Description =
                    "Moisture-wicking athletic socks with arch support and cushioned sole",
                Category = "Clothing",
                Price = 14.99m,
                Brand = "ActiveFit",
                Rating = 4.6,
                Stock = 250,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Size = "L",
                    Material = "Cotton blend",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Yoga Pants",
                Description =
                    "High-waisted yoga pants with four-way stretch and moisture-wicking fabric",
                Category = "Clothing",
                Price = 49.99m,
                Brand = "FlexWear",
                Rating = 4.7,
                Stock = 140,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Size = "M",
                    Material = "Spandex blend",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Dress Shirt",
                Description = "Wrinkle-free dress shirt with button-down collar and classic fit",
                Category = "Clothing",
                Price = 39.99m,
                Brand = "FormalStyle",
                Rating = 4.4,
                Stock = 110,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Size = "M",
                    Material = "Cotton",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Sneakers",
                Description = "Casual canvas sneakers with cushioned insole and rubber outsole",
                Category = "Clothing",
                Price = 54.99m,
                Brand = "UrbanStyle",
                Rating = 4.5,
                Stock = 130,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
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
                UUID = Guid.NewGuid(),
                Name = "Coffee Maker",
                Description = "Programmable coffee maker with 12-cup capacity and auto-shutoff",
                Category = "Home & Garden",
                Price = 69.99m,
                Brand = "BrewMaster",
                Rating = 4.5,
                Stock = 95,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 5.0,
                    Material = "Plastic",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Blender",
                Description = "High-powered blender with 1000W motor and multiple speed settings",
                Category = "Home & Garden",
                Price = 99.99m,
                Brand = "BlendTech",
                Rating = 4.7,
                Stock = 80,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Silver",
                    Weight = 8.0,
                    Material = "Stainless steel",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Air Purifier",
                Description =
                    "HEPA air purifier with activated carbon filter for rooms up to 500 sq ft",
                Category = "Home & Garden",
                Price = 179.99m,
                Brand = "PureAir",
                Rating = 4.6,
                Stock = 60,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Weight = 12.0,
                    Material = "Plastic",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Desk Lamp",
                Description =
                    "LED desk lamp with adjustable brightness, color temperature, and USB charging port",
                Category = "Home & Garden",
                Price = 44.99m,
                Brand = "LightWorks",
                Rating = 4.5,
                Stock = 120,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 2.0,
                    Material = "Metal",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Throw Pillow",
                Description =
                    "Decorative throw pillow with soft velvet cover and hypoallergenic filling",
                Category = "Home & Garden",
                Price = 22.99m,
                Brand = "HomeComfort",
                Rating = 4.3,
                Stock = 200,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Gray",
                    Size = "18x18",
                    Material = "Velvet",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Plant Pot",
                Description = "Ceramic plant pot with drainage hole and matching saucer",
                Category = "Home & Garden",
                Price = 18.99m,
                Brand = "GreenThumb",
                Rating = 4.4,
                Stock = 150,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "White",
                    Size = "8-inch",
                    Material = "Ceramic",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Kitchen Knife Set",
                Description =
                    "6-piece stainless steel knife set with wooden block and sharpening rod",
                Category = "Home & Garden",
                Price = 89.99m,
                Brand = "ChefPro",
                Rating = 4.8,
                Stock = 70,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Material = "Stainless steel", Weight = 4.0 },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Vacuum Cleaner",
                Description =
                    "Bagless upright vacuum cleaner with HEPA filter and pet hair attachment",
                Category = "Home & Garden",
                Price = 199.99m,
                Brand = "CleanHome",
                Rating = 4.5,
                Stock = 50,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Weight = 15.0,
                    Material = "Plastic",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Bath Towel Set",
                Description =
                    "4-piece bath towel set made from 100% Turkish cotton, ultra-absorbent",
                Category = "Home & Garden",
                Price = 49.99m,
                Brand = "LuxuryLinens",
                Rating = 4.6,
                Stock = 130,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Color = "Navy", Material = "Turkish cotton" },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Picture Frame",
                Description = "8x10 wooden picture frame with glass front and hanging hardware",
                Category = "Home & Garden",
                Price = 16.99m,
                Brand = "FrameIt",
                Rating = 4.2,
                Stock = 180,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
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
                UUID = Guid.NewGuid(),
                Name = "The Pragmatic Programmer",
                Description =
                    "Classic guide to software development best practices and professional growth",
                Category = "Books",
                Price = 44.99m,
                Brand = "Addison-Wesley",
                Rating = 4.8,
                Stock = 75,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Clean Code",
                Description = "Essential handbook for writing maintainable and readable code",
                Category = "Books",
                Price = 42.99m,
                Brand = "Prentice Hall",
                Rating = 4.7,
                Stock = 90,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Design Patterns",
                Description =
                    "Foundational book on software design patterns and object-oriented programming",
                Category = "Books",
                Price = 54.99m,
                Brand = "Addison-Wesley",
                Rating = 4.6,
                Stock = 60,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Refactoring",
                Description =
                    "Comprehensive guide to improving code structure without changing behavior",
                Category = "Books",
                Price = 46.99m,
                Brand = "Addison-Wesley",
                Rating = 4.7,
                Stock = 70,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Domain-Driven Design",
                Description = "Strategic approach to tackling complexity in software development",
                Category = "Books",
                Price = 56.99m,
                Brand = "Addison-Wesley",
                Rating = 4.5,
                Stock = 55,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "C# in Depth",
                Description =
                    "Deep dive into C# language features and best practices for .NET developers",
                Category = "Books",
                Price = 49.99m,
                Brand = "Manning",
                Rating = 4.8,
                Stock = 65,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Effective C#",
                Description = "50 specific ways to improve your C# programming skills",
                Category = "Books",
                Price = 44.99m,
                Brand = "Addison-Wesley",
                Rating = 4.6,
                Stock = 75,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Code Complete",
                Description =
                    "Comprehensive manual on software construction and practical guidelines",
                Category = "Books",
                Price = 49.99m,
                Brand = "Microsoft Press",
                Rating = 4.7,
                Stock = 80,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Head First Design Patterns",
                Description =
                    "Brain-friendly guide to software design patterns with visual explanations",
                Category = "Books",
                Price = 39.99m,
                Brand = "O'Reilly",
                Rating = 4.5,
                Stock = 95,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Working Effectively with Legacy Code",
                Description = "Strategies for safely modifying and improving existing codebases",
                Category = "Books",
                Price = 47.99m,
                Brand = "Prentice Hall",
                Rating = 4.6,
                Stock = 70,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
            },
        ]);

        // Sports (10 products)
        products.AddRange([
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Yoga Mat",
                Description = "Non-slip yoga mat with extra cushioning for comfort during practice",
                Category = "Sports",
                Price = 34.99m,
                Brand = "YogaFlow",
                Rating = 4.6,
                Stock = 110,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Purple",
                    Size = "68x24",
                    Material = "TPE",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Resistance Bands",
                Description =
                    "Set of 5 resistance bands with varying strengths for strength training",
                Category = "Sports",
                Price = 24.99m,
                Brand = "FitStrong",
                Rating = 4.5,
                Stock = 150,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Material = "Latex" },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Dumbbell Set",
                Description =
                    "Adjustable dumbbell set with 5-50 lbs weight range and compact design",
                Category = "Sports",
                Price = 299.99m,
                Brand = "PowerLift",
                Rating = 4.8,
                Stock = 40,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Weight = 55.0,
                    Material = "Steel",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Jump Rope",
                Description = "Speed jump rope with ball bearings and adjustable length for cardio",
                Category = "Sports",
                Price = 12.99m,
                Brand = "CardioMax",
                Rating = 4.3,
                Stock = 200,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Color = "Black", Material = "PVC" },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Water Bottle",
                Description =
                    "Insulated stainless steel water bottle that keeps drinks cold for 24 hours",
                Category = "Sports",
                Price = 27.99m,
                Brand = "HydroFlow",
                Rating = 4.7,
                Stock = 170,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Size = "32oz",
                    Material = "Stainless steel",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Foam Roller",
                Description =
                    "High-density foam roller for muscle recovery and deep tissue massage",
                Category = "Sports",
                Price = 29.99m,
                Brand = "RecoverPro",
                Rating = 4.5,
                Stock = 130,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Black",
                    Size = "18-inch",
                    Material = "EVA foam",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Exercise Ball",
                Description = "Anti-burst exercise ball with pump for core training and stability",
                Category = "Sports",
                Price = 19.99m,
                Brand = "CoreFit",
                Rating = 4.4,
                Stock = 145,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications
                {
                    Color = "Blue",
                    Size = "65cm",
                    Material = "PVC",
                },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Gym Bag",
                Description =
                    "Durable gym bag with separate shoe compartment and water bottle holder",
                Category = "Sports",
                Price = 44.99m,
                Brand = "ActiveGear",
                Rating = 4.5,
                Stock = 100,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Color = "Black", Material = "Polyester" },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Running Belt",
                Description = "Adjustable running belt with expandable pocket for phone and keys",
                Category = "Sports",
                Price = 16.99m,
                Brand = "RunFree",
                Rating = 4.3,
                Stock = 180,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Color = "Black", Material = "Nylon" },
            },
            new Product
            {
                UUID = Guid.NewGuid(),
                Name = "Fitness Tracker",
                Description =
                    "Waterproof fitness tracker with heart rate monitor and sleep tracking",
                Category = "Sports",
                Price = 79.99m,
                Brand = "FitTech",
                Rating = 4.6,
                Stock = 90,
                ImageUrl = $"https://picsum.photos/300/300?random={imageIndex++}",
                Specs = new ProductSpecifications { Color = "Black", Material = "Silicone" },
            },
        ]);

        return products;
    }

    public static List<ProductReview> GenerateSampleReviews(List<Product> products)
    {
        var reviews = new List<ProductReview>();
        var random = new Random(42); // Fixed seed for reproducibility

        var productsToReview = products;

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
