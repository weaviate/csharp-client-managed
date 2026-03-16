import { ApiClient } from '$lib/api';
import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ params }) => {
	const { id } = params;

	try {
		// Load product details
		const product = await ApiClient.getProduct(id);

		if (!product) {
			throw error(404, 'Product not found');
		}

		// Load similar products
		const similar = await ApiClient.getSimilarProducts(id, 4);

		// Load reviews
		const reviews = await ApiClient.getReviews(id);

		return {
			product,
			similar,
			reviews
		};
	} catch (err) {
		console.error('Error loading product:', err);
		throw error(404, 'Product not found');
	}
};
