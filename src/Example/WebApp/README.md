# AI Product Search - SvelteKit Frontend

A modern, AI-powered product search interface built with SvelteKit, showcasing the capabilities of the Weaviate C# Managed Client.

## Features

- **Semantic Search**: Find products by meaning using vector embeddings
- **Hybrid Search**: Combine semantic and keyword search for best results
- **Keyword Search**: Traditional BM25 full-text search
- **Smart Recommendations**: Vector-based similar product suggestions
- **Faceted Filtering**: Filter by category, price, rating, and brand
- **Server-Side Rendering**: Fast initial page loads with SSR
- **Type-Safe**: Full TypeScript support throughout

## Tech Stack

- **SvelteKit 7.x**: Modern web framework with file-based routing
- **TypeScript**: Type safety across frontend and backend integration
- **Vite**: Fast development server with HMR
- **Node.js Adapter**: Production-ready Node.js deployment

## Prerequisites

- Node.js 18+ and npm
- Backend WebApi running on http://localhost:5000
- Weaviate instance running with sample data

## Installation

```bash
npm install
```

## Development

Start the development server:

```bash
npm run dev
```

The app will be available at http://localhost:5173 with API proxy to the backend.

## Build

Build for production:

```bash
npm run build
```

Preview the production build:

```bash
npm run preview
```

## Project Structure

```
src/
├── lib/
│   ├── api.ts                      # API client for backend communication
│   ├── types.ts                    # TypeScript type definitions
│   └── components/
│       ├── ProductCard.svelte      # Product display card
│       └── SearchBar.svelte        # Search interface with mode selector
├── routes/
│   ├── +layout.svelte              # Main layout with navigation
│   ├── +page.svelte                # Home page
│   ├── +page.server.ts             # Home page data loading
│   ├── +error.svelte               # Error page
│   ├── search/
│   │   ├── +page.svelte            # Search results page
│   │   └── +page.server.ts         # Search execution
│   └── product/
│       └── [id]/
│           ├── +page.svelte        # Product detail page
│           └── +page.server.ts     # Product data loading
└── app.html                        # HTML template

static/                             # Static assets (images, fonts, etc.)
```

## API Client

The `ApiClient` class in `src/lib/api.ts` provides type-safe methods for all backend endpoints:

- `health()`: Health check
- `getProducts(params?)`: Get products with optional filters
- `getProduct(id)`: Get single product
- `getSimilarProducts(id, limit?)`: Vector-based recommendations
- `getFacets()`: Get category and brand counts
- `search(request)`: Main search with mode selection
- `getReviews(productId)`: Get product reviews
- `searchReviews(query, limit?)`: Search in reviews

## Search Modes

### Semantic Search
Uses AI-powered vector embeddings to find products by meaning. Example: "device for taking photos" will find cameras.

### Hybrid Search
Combines semantic search with keyword matching using an alpha parameter for optimal results.

### Keyword Search
Traditional BM25 full-text search for exact term matching.

## Configuration

The Vite configuration in `vite.config.ts` includes a proxy to forward `/api` requests to the backend:

```typescript
server: {
  port: 5173,
  proxy: {
    '/api': 'http://localhost:5000'
  }
}
```

Change the backend URL if your WebApi runs on a different port.

## Type Safety

All API responses are typed using TypeScript interfaces in `src/lib/types.ts`:

- `Product`: Product entity with all properties
- `SearchRequest`: Search parameters with mode and filters
- `SearchResult`: Search result with object and metadata
- `Facets`: Category and brand facet counts

## SSR Benefits

Server-side rendering provides:
- Faster initial page loads
- Better SEO
- Progressive enhancement
- Improved performance on slow connections

## Development Tips

- Use the browser devtools to inspect API calls
- Check the Network tab to see search requests and responses
- Use the SvelteKit inspector to debug component state
- Hot module replacement (HMR) updates changes instantly

## Deployment

The project uses `@sveltejs/adapter-node` for Node.js deployment:

```bash
npm run build
node build
```

Or deploy to platforms like Vercel, Netlify, or Cloudflare Pages by changing the adapter.

## Related Projects

- [WebApi Backend](../WebApi/README.md): ASP.NET Core backend
- [Weaviate C# Client](../../Weaviate.Client.Managed/README.md): ORM layer
- [Example Documentation](../README.md): Complete example overview
