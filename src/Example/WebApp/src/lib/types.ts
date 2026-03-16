// Type definitions for the WebApi backend

export interface Product {
	uuid: string;
	name: string;
	description: string;
	category: string;
	price: number | null;
	brand: string;
	rating: number | null;
	stock: number;
	imageUrl: string;
	specs?: ProductSpecifications | null;
}

export interface ProductSpecifications {
	color?: string;
	size?: string;
	weight?: number;
	material?: string;
}

export interface ProductReview {
	uuid: string;
	title: string;
	content: string;
	rating: number;
	reviewerName: string;
	reviewDate: string;
}

export interface SearchRequest {
	query: string;
	mode: 'semantic' | 'hybrid' | 'keyword';
	category?: string;
	minPrice?: number;
	maxPrice?: number;
	minRating?: number;
	brand?: string;
	limit?: number;
	offset?: number;
	alpha?: number; // For hybrid search
	sortBy?: 'relevance' | 'price-asc' | 'price-desc' | 'rating-desc';
}

export interface SearchResult {
	uuid: string;
	object: Product;
	metadata: {
		distance?: number;
		score?: number;
	};
}

export interface CategoryFacet {
	category: string;
	count: number;
}

export interface BrandFacet {
	brand: string;
	count: number;
}

export interface Facets {
	categories: CategoryFacet[];
	brands: BrandFacet[];
}
