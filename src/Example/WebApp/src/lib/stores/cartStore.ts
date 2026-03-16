import { browser } from '$app/environment';
import type { Product } from '$lib/types';
import { writable } from 'svelte/store';

export interface CartItem {
    product: Product;
    quantity: number;
}

function createCartStore() {
    // Load from localStorage if in browser
    const initialCart: CartItem[] = browser
        ? JSON.parse(localStorage.getItem('cart') || '[]')
        : [];

    const { subscribe, set, update } = writable<CartItem[]>(initialCart);

    return {
        subscribe,
        addItem: (product: Product, quantity: number = 1) => {
            update(items => {
                const existing = items.find(item => item.product.uuid === product.uuid);
                if (existing) {
                    existing.quantity += quantity;
                    return items;
                }
                return [...items, { product, quantity }];
            });
        },
        removeItem: (productId: string) => {
            update(items => items.filter(item => item.product.uuid !== productId));
        },
        updateQuantity: (productId: string, quantity: number) => {
            if (quantity <= 0) {
                return cartStore.removeItem(productId);
            }
            update(items => {
                const item = items.find(i => i.product.uuid === productId);
                if (item) {
                    item.quantity = quantity;
                }
                return items;
            });
        },
        clear: () => set([]),
        getTotal: (items: CartItem[]) => {
            return items.reduce((sum, item) => {
                return sum + (item.product.price || 0) * item.quantity;
            }, 0);
        },
        getCount: (items: CartItem[]) => {
            return items.reduce((sum, item) => sum + item.quantity, 0);
        }
    };
}

export const cartStore = createCartStore();

// Persist to localStorage on changes
if (browser) {
    cartStore.subscribe(items => {
        localStorage.setItem('cart', JSON.stringify(items));
    });
}
