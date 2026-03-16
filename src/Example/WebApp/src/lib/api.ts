import { browser } from '$app/environment';
import type { Facets, Product, ProductReview, SearchRequest, SearchResult } from './types';

// Use different base URLs for server (SSR) vs client
// Server needs full URL (configurable via env var), client uses relative URL with proxy
const getApiBase = () => {
	if (browser) {
		// In browser, use relative URL which gets proxied by Vite to localhost:5001
		return '/api';
	} else {
		// In SSR, use env var or default to localhost:5001
		// @ts-ignore - process.env is available in Node.js SSR context
		const backendUrl = process.env.BACKEND_URL || 'http://localhost:5001';
		return `${backendUrl}/api`;
	}
};

const getHealthUrl = () => {
	if (browser) {
		return '/health';
	} else {
		// @ts-ignore - process.env is available in Node.js SSR context
		const backendUrl = process.env.BACKEND_URL || 'http://localhost:5001';
		return `${backendUrl}/health`;
	}
};

export class ApiClient {
	// Health check
	static async health(): Promise<{ status: string }> {
		try {
			const response = await fetch(getHealthUrl());
			if (!response.ok) throw new Error(`Health check failed: ${response.status}`);
			return response.json();
		} catch (error) {
			console.error('Health check error:', error);
			throw new Error('Unable to reach the backend API. Please ensure it is running.');
		}
	}

	// Get all products with optional filters
	static async getProducts(params?: {
		category?: string;
		minPrice?: number;
		maxPrice?: number;
		minRating?: number;
		limit?: number;
		offset?: number;
	}): Promise<Product[]> {
		try {
			const searchParams = new URLSearchParams();
			if (params?.category) searchParams.set('category', params.category);
			if (params?.minPrice !== undefined) searchParams.set('minPrice', params.minPrice.toString());
			if (params?.maxPrice !== undefined) searchParams.set('maxPrice', params.maxPrice.toString());
			if (params?.minRating !== undefined)
				searchParams.set('minRating', params.minRating.toString());
			if (params?.limit !== undefined) searchParams.set('limit', params.limit.toString());
			if (params?.offset !== undefined) searchParams.set('offset', params.offset.toString());

			const url = `${getApiBase()}/products${searchParams.toString() ? `?${searchParams.toString()}` : ''}`;
			const response = await fetch(url);
			if (!response.ok) throw new Error(`Failed to fetch products: ${response.status}`);
			return response.json();
		} catch (error) {
			console.error('Get products error:', error);
			throw new Error('Failed to load products. Please try again later.');
		}
	}

	// Get a single product by ID
	static async getProduct(id: string): Promise<Product> {
		try {
			const response = await fetch(`${getApiBase()}/products/${id}`);
			if (!response.ok) {
				if (response.status === 404) {
					throw new Error('Product not found');
				}
				throw new Error(`Failed to fetch product: ${response.status}`);
			}
			return response.json();
		} catch (error) {
			console.error('Get product error:', error);
			if (error instanceof Error && error.message === 'Product not found') {
				throw error;
			}
			throw new Error('Failed to load product details. Please try again later.');
		}
	}

	// Get similar products
	static async getSimilarProducts(id: string, limit: number = 5): Promise<SearchResult[]> {
		try {
			const response = await fetch(`${getApiBase()}/products/${id}/similar?limit=${limit}`);
			if (!response.ok) throw new Error(`Failed to fetch similar products: ${response.status}`);
			return response.json();
		} catch (error) {
			console.error('Get similar products error:', error);
			// Don't throw - return empty array so page still loads
			return [];
		}
	}

	// Get facets (categories and brands with counts)
	static async getFacets(): Promise<Facets> {
		try {
			const response = await fetch(`${getApiBase()}/products/facets`);
			if (!response.ok) throw new Error(`Failed to fetch facets: ${response.status}`);
			return response.json();
		} catch (error) {
			console.error('Get facets error:', error);
			// Return empty facets so page still loads
			return { categories: [], brands: [] };
		}
	}

	// Search products
	static async search(request: SearchRequest): Promise<SearchResult[]> {
		try {
			const response = await fetch(`${getApiBase()}/search`, {
				method: 'POST',
				headers: { 'Content-Type': 'application/json' },
				body: JSON.stringify(request)
			});
			if (!response.ok) throw new Error(`Search failed: ${response.status}`);
			return response.json();
		} catch (error) {
			console.error('Search error:', error);
			throw new Error('Search failed. Please check your query and try again.');
		}
	}

	// Get reviews for a product
	static async getReviews(productId: string): Promise<ProductReview[]> {
		try {
			const response = await fetch(`${getApiBase()}/reviews/product/${productId}`);
			if (!response.ok) throw new Error(`Failed to fetch reviews: ${response.status}`);
			return response.json();
		} catch (error) {
			console.error('Get reviews error:', error);
			// Return empty array so page still loads
			return [];
		}
	}

	// Search reviews
	static async searchReviews(query: string, limit: number = 10): Promise<ProductReview[]> {
		try {
			const response = await fetch(
				`${getApiBase()}/reviews/search?query=${encodeURIComponent(query)}&limit=${limit}`
			);
			if (!response.ok) throw new Error(`Review search failed: ${response.status}`);
			return response.json();
		} catch (error) {
			console.error('Search reviews error:', error);
			return [];
		}
	}
}
