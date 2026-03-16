import { ApiClient } from '$lib/api';
import type { SearchRequest } from '$lib/types';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ url }) => {
	const query = url.searchParams.get('q') || '';
	const mode = (url.searchParams.get('mode') || 'semantic') as 'semantic' | 'hybrid' | 'keyword';
	const category = url.searchParams.get('category') || undefined;
	const brand = url.searchParams.get('brand') || undefined;
	const minPrice = url.searchParams.get('minPrice');
	const maxPrice = url.searchParams.get('maxPrice');
	const minRating = url.searchParams.get('minRating');
	const sortBy = url.searchParams.get('sortBy') || undefined;
	const page = parseInt(url.searchParams.get('page') || '1', 10);
	const pageSize = 20;
	const offset = (page - 1) * pageSize;

	try {
		// Build search request
		const searchRequest: SearchRequest = {
			query,
			mode,
			limit: pageSize,
			offset
		};

		if (category) {
			searchRequest.category = category;
		}

		if (brand) {
			searchRequest.brand = brand;
		}

		if (minPrice) {
			searchRequest.minPrice = parseFloat(minPrice);
		}

		if (maxPrice) {
			searchRequest.maxPrice = parseFloat(maxPrice);
		}

		if (minRating) {
			searchRequest.minRating = parseFloat(minRating);
		}

		if (sortBy) {
			searchRequest.sortBy = sortBy as any;
		}

		// Execute search if query exists
		const results = query ? await ApiClient.search(searchRequest) : [];

		// Get facets for filtering
		const facets = await ApiClient.getFacets();

		return {
			query,
			mode,
			category,
			brand,
			minPrice: minPrice ? parseFloat(minPrice) : undefined,
			maxPrice: maxPrice ? parseFloat(maxPrice) : undefined,
			minRating: minRating ? parseFloat(minRating) : undefined,
			sortBy,
			results,
			facets,
			page,
			pageSize,
			hasMore: results.length === pageSize
		};
	} catch (error) {
		console.error('Search error:', error);
		return {
			query,
			mode,
			category,
			brand,
			minPrice: undefined,
			maxPrice: undefined,
			minRating: undefined,
			sortBy: undefined,
			results: [],
			facets: { categories: [], brands: [] },
			page: 1,
			pageSize: 20,
			hasMore: false
		};
	}
};
