# AI Product Search Demo - WebApi

Full-stack demo showcasing the Weaviate C# Managed Client with semantic search, product recommendations, and faceted filtering.

## Overview

- **Backend**: ASP.NET Core Minimal API (.NET 9.0)
- **Frontend**: SvelteKit + Tailwind CSS
- **Database**: Weaviate (vector database)

## Backend Status

✅ **Complete** - All backend code is implemented and ready:
- ✅ Product and ProductReview models with ImageUrl support
- ✅ ProductCatalogContext (WeaviateContext)
- ✅ 4 endpoint groups (Health, Products, Search, Reviews)
- ✅ DataSeeder service (50 products, ~20 reviews)
- ✅ ProductDataGenerator with sample data

## Prerequisites

1. **Docker** - For running Weaviate
2. **.NET 9.0 SDK** - For running the backend
3. **Node.js 18+** - For running the frontend (optional)

## Quick Start

### 1. Start Weaviate

We provide a simplified Docker Compose tailored for this demo:

```bash
cd src/Example/WebApi
docker compose up -d
```

This starts a single Weaviate instance without the complex cluster/RBAC setup, avoiding gRPC health check issues.

Wait for Weaviate to be ready (usually 5-10 seconds):
```bash
# Check if ready
curl http://localhost:8080/v1/.well-known/ready
```

### 2. Run the Backend

```bash
cd src/Example/WebApi
dotnet run
```

The API will:
1. Connect to Weaviate at localhost:8080
2. Create Product and ProductReview collections
3. Seed 50 products with images and ~20 reviews
4. Start listening at http://localhost:5000

### 3. Test the API

Open your browser to:
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health

Or use curl:
```bash
# Get all products
curl http://localhost:5000/api/products

# Search with hybrid mode
curl -X POST http://localhost:5000/api/search \
  -H "Content-Type: application/json" \
  -d '{"query":"wireless mouse","mode":"hybrid","alpha":0.5}'

# Get product facets
curl http://localhost:5000/api/products/facets
```

### 4. Stop Everything

```bash
# Stop backend (Ctrl+C in terminal)

# Stop Weaviate
cd src/Example/WebApi
docker compose down
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/health` | GET | Health check |
| `/api/products` | GET | List products with filters |
| `/api/products/{id}` | GET | Get single product |
| `/api/products/{id}/similar` | GET | Get similar products (NearObject) |
| `/api/products/facets` | GET | Get facets (categories, brands) |
| `/api/search` | POST | Unified search (semantic/hybrid/keyword) |
| `/api/reviews` | GET | List reviews with filters |

## Features Implemented

### 1. Semantic Search
Natural language product search using NearText:
- Example: "wireless headphones with noise cancellation"

### 2. Hybrid Search
Combines semantic (vector) + keyword (BM25):
- Adjustable alpha slider (0=keyword, 1=semantic)

### 3. Product Recommendations
Finds similar products using NearObject vector similarity.

### 4. Review Filtering
Filter reviews by rating, search content with BM25.

### 5. Faceted Search
Dynamic category and brand counts for filtering.

## Code Highlights

```csharp
// Query with semantic search
var results = await context
    .Query<Product>()
    .NearText("wireless mouse")
    .Where(p => p.Category == "Electronics")
    .Limit((uint)10)
    .Execute();

// Hybrid search
var results = await context
    .Query<Product>()
    .Hybrid("gaming keyboard", alpha: 0.7f)
    .WithMetadata(MetadataOptions.Score)
    .Execute();

// Similar products
var similar = await context
    .Query<Product>()
    .NearObject(productId)
    .Limit((uint)5)
    .WithMetadata(MetadataOptions.Distance)
    .Execute();
```

## Project Structure

```
src/Example/WebApi/
├── WebApi.csproj
├── Program.cs              # App entry point, DI setup
├── GlobalUsings.cs         # Global using directives
├── Models/
│   ├── Product.cs          # Product, ProductSpecifications, ProductReview
│   └── ProductCatalogContext.cs  # WeaviateContext
├── Endpoints/
│   ├── HealthEndpoints.cs
│   ├── ProductEndpoints.cs
│   ├── SearchEndpoints.cs
│   └── ReviewEndpoints.cs
├── Services/
│   ├── DataSeeder.cs       # IHostedService for data seeding
│   └── ProductDataGenerator.cs  # Sample data generation
└── wwwroot/
    └── images/             # Product images (will be from picsum.photos)
```

## Next Steps

1. Resolve gRPC health check issue (see workarounds above)
2. Test backend API with curl or Swagger UI
3. Implement SvelteKit frontend
4. Connect frontend to backend API
5. Deploy demo

## Frontend (To Be Implemented)

The frontend will be a SvelteKit application located at `src/Example/WebApp/` with:
- Product listing with category sidebar
- Interactive search with mode selector
- Product detail pages with similar products
- Review display and filtering

See the implementation plan for detailed frontend specifications.
