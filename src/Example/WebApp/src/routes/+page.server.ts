import { ApiClient } from '$lib/api';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async () => {
	try {
		const products = await ApiClient.getProducts({ limit: 12 });
		return {
			products
		};
	} catch (error) {
		console.error('Failed to load products:', error);
		return {
			products: []
		};
	}
};
