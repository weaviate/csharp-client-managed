<script lang="ts">
	import { cartStore } from '$lib/stores/cartStore';
	import { showSuccess } from '$lib/stores/toastStore';
	import { wishlistStore } from '$lib/stores/wishlistStore';
	import type { Product } from '$lib/types';

	$: items = $wishlistStore;

	function removeItem(productId: string, productName: string) {
		wishlistStore.removeItem(productId);
		showSuccess(`Removed ${productName} from wishlist`);
	}

	function addToCart(product: Product) {
		cartStore.addItem(product, 1);
		showSuccess(`Added ${product.name} to cart!`);
	}

	function clearWishlist() {
		if (confirm('Are you sure you want to clear your wishlist?')) {
			wishlistStore.clear();
			showSuccess('Wishlist cleared');
		}
	}
</script>

<svelte:head>
	<title>Wishlist - AI Product Search</title>
</svelte:head>

<div class="wishlist-page">
	<div class="wishlist-header">
		<h1>♡ My Wishlist</h1>
		{#if items.length > 0}
			<p class="item-count">{items.length} {items.length === 1 ? 'item' : 'items'}</p>
		{/if}
	</div>

	{#if items.length === 0}
		<div class="empty-wishlist">
			<h2>Your wishlist is empty</h2>
			<p>Save products you love for later!</p>
			<a href="/search" class="browse-btn">Browse Products</a>
		</div>
	{:else}
		<div class="wishlist-actions">
			<button class="clear-btn" on:click={clearWishlist}>Clear Wishlist</button>
		</div>

		<div class="wishlist-grid">
			{#each items as product (product.uuid)}
				<div class="wishlist-item">
					<button
						class="remove-btn"
						on:click={() => removeItem(product.uuid, product.name)}
						aria-label="Remove {product.name}"
					>
						×
					</button>
					<a href="/product/{product.uuid}" class="product-link">
						<img src={product.imageUrl} alt={product.name} class="product-image" />
					</a>
					<div class="product-info">
						<h3>
							<a href="/product/{product.uuid}">{product.name}</a>
						</h3>
						<p class="product-brand">{product.brand}</p>
						<div class="product-meta">
							<span class="product-rating">⭐ {product.rating?.toFixed(1) ?? '0.0'}</span>
							{#if product.stock > 0}
								<span class="in-stock">In Stock</span>
							{:else}
								<span class="out-of-stock">Out of Stock</span>
							{/if}
						</div>
						<p class="product-price">${product.price?.toFixed(2) ?? '0.00'}</p>
						<button
							class="add-to-cart-btn"
							disabled={product.stock === 0}
							on:click={() => addToCart(product)}
						>
							{product.stock > 0 ? 'Add to Cart' : 'Out of Stock'}
						</button>
					</div>
				</div>
			{/each}
		</div>
	{/if}
</div>

<style>
	.wishlist-page {
		max-width: 1200px;
		margin: 0 auto;
		padding: 2rem 1rem;
	}

	.wishlist-header {
		margin-bottom: 2rem;
	}

	.wishlist-header h1 {
		margin: 0 0 0.5rem 0;
		font-size: 2rem;
		font-weight: 700;
	}

	.item-count {
		margin: 0;
		color: #6b7280;
	}

	.empty-wishlist {
		text-align: center;
		padding: 4rem 2rem;
	}

	.empty-wishlist h2 {
		margin: 0 0 1rem 0;
		font-size: 2rem;
		color: #111827;
	}

	.empty-wishlist p {
		margin: 0 0 2rem 0;
		color: #6b7280;
		font-size: 1.125rem;
	}

	.browse-btn {
		display: inline-block;
		padding: 0.75rem 2rem;
		background: #3b82f6;
		color: white;
		text-decoration: none;
		border-radius: 8px;
		font-weight: 600;
		transition: background 0.2s;
	}

	.browse-btn:hover {
		background: #2563eb;
	}

	.wishlist-actions {
		margin-bottom: 2rem;
		display: flex;
		justify-content: flex-end;
	}

	.clear-btn {
		padding: 0.5rem 1rem;
		background: white;
		color: #6b7280;
		border: 1px solid #d1d5db;
		border-radius: 6px;
		font-weight: 500;
		cursor: pointer;
		transition: all 0.2s;
	}

	.clear-btn:hover {
		border-color: #ef4444;
		color: #ef4444;
	}

	.wishlist-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
		gap: 2rem;
	}

	.wishlist-item {
		background: white;
		border-radius: 8px;
		border: 1px solid #e5e7eb;
		overflow: hidden;
		position: relative;
		transition: transform 0.2s, box-shadow 0.2s;
	}

	.wishlist-item:hover {
		transform: translateY(-4px);
		box-shadow: 0 10px 25px rgba(0, 0, 0, 0.1);
	}

	.remove-btn {
		position: absolute;
		top: 0.5rem;
		right: 0.5rem;
		background: white;
		border: none;
		color: #9ca3af;
		font-size: 1.75rem;
		cursor: pointer;
		padding: 0;
		width: 32px;
		height: 32px;
		display: flex;
		align-items: center;
		justify-content: center;
		border-radius: 50%;
		box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
		transition: all 0.2s;
		z-index: 10;
	}

	.remove-btn:hover {
		background: #fee2e2;
		color: #ef4444;
		transform: scale(1.1);
	}

	.product-link {
		display: block;
	}

	.product-image {
		width: 100%;
		height: 200px;
		object-fit: cover;
	}

	.product-info {
		padding: 1.5rem;
	}

	.product-info h3 {
		margin: 0 0 0.5rem 0;
		font-size: 1.125rem;
		font-weight: 600;
		line-height: 1.4;
	}

	.product-info h3 a {
		color: inherit;
		text-decoration: none;
	}

	.product-info h3 a:hover {
		color: #3b82f6;
	}

	.product-brand {
		margin: 0 0 0.75rem 0;
		color: #6b7280;
		font-size: 0.875rem;
	}

	.product-meta {
		display: flex;
		gap: 1rem;
		margin-bottom: 0.75rem;
		font-size: 0.875rem;
	}

	.product-rating {
		color: #374151;
	}

	.in-stock {
		color: #059669;
		font-weight: 500;
	}

	.out-of-stock {
		color: #ef4444;
		font-weight: 500;
	}

	.product-price {
		margin: 0 0 1rem 0;
		font-size: 1.5rem;
		font-weight: 700;
		color: #111827;
	}

	.add-to-cart-btn {
		width: 100%;
		padding: 0.75rem;
		background: #3b82f6;
		color: white;
		border: none;
		border-radius: 6px;
		font-weight: 600;
		cursor: pointer;
		transition: background 0.2s;
	}

	.add-to-cart-btn:hover:not(:disabled) {
		background: #2563eb;
	}

	.add-to-cart-btn:disabled {
		background: #d1d5db;
		cursor: not-allowed;
	}

	@media (max-width: 768px) {
		.wishlist-grid {
			grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
			gap: 1rem;
		}

		.product-image {
			height: 150px;
		}

		.product-info {
			padding: 1rem;
		}
	}
</style>
