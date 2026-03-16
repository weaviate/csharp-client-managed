import { browser } from '$app/environment';
import type { Product } from '$lib/types';
import { writable } from 'svelte/store';

function createWishlistStore() {
    // Load from localStorage if in browser
    const initialItems: Product[] = browser
        ? JSON.parse(localStorage.getItem('wishlist') || '[]')
        : [];

    const { subscribe, set, update } = writable<Product[]>(initialItems);

    return {
        subscribe,
        addItem: (product: Product) => {
            update(items => {
                if (items.some(item => item.uuid === product.uuid)) {
                    return items; // Already in wishlist
                }
                return [...items, product];
            });
        },
        removeItem: (productId: string) => {
            update(items => items.filter(item => item.uuid !== productId));
        },
        toggleItem: (product: Product) => {
            update(items => {
                const exists = items.some(item => item.uuid === product.uuid);
                if (exists) {
                    return items.filter(item => item.uuid !== product.uuid);
                }
                return [...items, product];
            });
        },
        clear: () => set([]),
        isInWishlist: (items: Product[], productId: string) => {
            return items.some(item => item.uuid === productId);
        }
    };
}

export const wishlistStore = createWishlistStore();

// Persist to localStorage on changes
if (browser) {
    wishlistStore.subscribe(items => {
        localStorage.setItem('wishlist', JSON.stringify(items));
    });
}
