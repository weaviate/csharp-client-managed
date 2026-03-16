<script lang="ts">
	import { cartStore } from '$lib/stores/cartStore';
	import { showSuccess } from '$lib/stores/toastStore';

	$: items = $cartStore;
	$: total = cartStore.getTotal(items);
	$: itemCount = cartStore.getCount(items);

	function updateQuantity(productId: string, quantity: number) {
		cartStore.updateQuantity(productId, quantity);
	}

	function removeItem(productId: string, productName: string) {
		cartStore.removeItem(productId);
		showSuccess(`Removed ${productName} from cart`);
	}

	function clearCart() {
		if (confirm('Are you sure you want to clear your cart?')) {
			cartStore.clear();
			showSuccess('Cart cleared');
		}
	}
</script>

<svelte:head>
	<title>Shopping Cart - AI Product Search</title>
</svelte:head>

<div class="cart-page">
	<div class="cart-header">
		<h1>Shopping Cart</h1>
		{#if items.length > 0}
			<p class="item-count">{itemCount} {itemCount === 1 ? 'item' : 'items'}</p>
		{/if}
	</div>

	{#if items.length === 0}
		<div class="empty-cart">
			<h2>🛒 Your cart is empty</h2>
			<p>Add some products to get started!</p>
			<a href="/search" class="browse-btn">Browse Products</a>
		</div>
	{:else}
		<div class="cart-content">
			<div class="cart-items">
				{#each items as item (item.product.uuid)}
					<div class="cart-item">
						<img src={item.product.imageUrl} alt={item.product.name} class="item-image" />
						<div class="item-details">
							<h3>
								<a href="/product/{item.product.uuid}">{item.product.name}</a>
							</h3>
							<p class="item-brand">{item.product.brand}</p>
							<p class="item-price">${item.product.price?.toFixed(2) ?? '0.00'}</p>
						</div>
						<div class="item-quantity">
							<label for="qty-{item.product.uuid}">Qty:</label>
							<input
								id="qty-{item.product.uuid}"
								type="number"
								min="1"
								max={item.product.stock}
								value={item.quantity}
								on:change={(e) => updateQuantity(item.product.uuid, parseInt(e.currentTarget.value))}
							/>
						</div>
						<div class="item-subtotal">
							${((item.product.price || 0) * item.quantity).toFixed(2)}
						</div>
						<button
							class="remove-btn"
							on:click={() => removeItem(item.product.uuid, item.product.name)}
							aria-label="Remove {item.product.name}"
						>
							×
						</button>
					</div>
				{/each}
			</div>

			<div class="cart-summary">
				<h2>Order Summary</h2>
				<div class="summary-row">
					<span>Subtotal ({itemCount} items):</span>
					<span>${total.toFixed(2)}</span>
				</div>
				<div class="summary-row">
					<span>Shipping:</span>
					<span>Free</span>
				</div>
				<div class="summary-row total">
					<span>Total:</span>
					<span>${total.toFixed(2)}</span>
				</div>
				<button class="checkout-btn">Proceed to Checkout</button>
				<button class="clear-btn" on:click={clearCart}>Clear Cart</button>
			</div>
		</div>
	{/if}
</div>

<style>
	.cart-page {
		max-width: 1200px;
		margin: 0 auto;
		padding: 2rem 1rem;
	}

	.cart-header {
		margin-bottom: 2rem;
	}

	.cart-header h1 {
		margin: 0 0 0.5rem 0;
		font-size: 2rem;
		font-weight: 700;
	}

	.item-count {
		margin: 0;
		color: #6b7280;
	}

	.empty-cart {
		text-align: center;
		padding: 4rem 2rem;
	}

	.empty-cart h2 {
		margin: 0 0 1rem 0;
		font-size: 2rem;
		color: #111827;
	}

	.empty-cart p {
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

	.cart-content {
		display: grid;
		grid-template-columns: 1fr 350px;
		gap: 2rem;
		align-items: start;
	}

	.cart-items {
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	.cart-item {
		display: grid;
		grid-template-columns: 100px 1fr auto auto auto;
		gap: 1.5rem;
		background: white;
		padding: 1.5rem;
		border-radius: 8px;
		border: 1px solid #e5e7eb;
		align-items: center;
	}

	.item-image {
		width: 100px;
		height: 100px;
		object-fit: cover;
		border-radius: 6px;
	}

	.item-details {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.item-details h3 {
		margin: 0;
		font-size: 1.125rem;
		font-weight: 600;
	}

	.item-details h3 a {
		color: inherit;
		text-decoration: none;
	}

	.item-details h3 a:hover {
		color: #3b82f6;
	}

	.item-brand {
		margin: 0;
		color: #6b7280;
		font-size: 0.875rem;
	}

	.item-price {
		margin: 0;
		font-weight: 600;
		color: #111827;
	}

	.item-quantity {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
		align-items: center;
	}

	.item-quantity label {
		font-size: 0.875rem;
		color: #6b7280;
	}

	.item-quantity input {
		width: 60px;
		padding: 0.5rem;
		border: 1px solid #d1d5db;
		border-radius: 6px;
		text-align: center;
	}

	.item-subtotal {
		font-weight: 600;
		font-size: 1.125rem;
		color: #111827;
		min-width: 100px;
		text-align: right;
	}

	.remove-btn {
		background: none;
		border: none;
		color: #9ca3af;
		font-size: 2rem;
		cursor: pointer;
		padding: 0;
		width: 32px;
		height: 32px;
		display: flex;
		align-items: center;
		justify-content: center;
		border-radius: 4px;
		transition: all 0.2s;
	}

	.remove-btn:hover {
		background: #fee2e2;
		color: #ef4444;
	}

	.cart-summary {
		background: white;
		padding: 2rem;
		border-radius: 8px;
		border: 1px solid #e5e7eb;
		position: sticky;
		top: 2rem;
	}

	.cart-summary h2 {
		margin: 0 0 1.5rem 0;
		font-size: 1.5rem;
		font-weight: 700;
	}

	.summary-row {
		display: flex;
		justify-content: space-between;
		padding: 0.75rem 0;
		border-bottom: 1px solid #e5e7eb;
	}

	.summary-row.total {
		font-size: 1.25rem;
		font-weight: 700;
		border-top: 2px solid #111827;
		border-bottom: none;
		padding-top: 1rem;
		margin-top: 0.5rem;
	}

	.checkout-btn {
		width: 100%;
		padding: 1rem;
		background: #3b82f6;
		color: white;
		border: none;
		border-radius: 8px;
		font-size: 1.125rem;
		font-weight: 600;
		cursor: pointer;
		transition: background 0.2s;
		margin-top: 1.5rem;
	}

	.checkout-btn:hover {
		background: #2563eb;
	}

	.clear-btn {
		width: 100%;
		padding: 0.75rem;
		background: white;
		color: #6b7280;
		border: 1px solid #d1d5db;
		border-radius: 8px;
		font-weight: 500;
		cursor: pointer;
		transition: all 0.2s;
		margin-top: 0.75rem;
	}

	.clear-btn:hover {
		border-color: #ef4444;
		color: #ef4444;
	}

	@media (max-width: 968px) {
		.cart-content {
			grid-template-columns: 1fr;
		}

		.cart-summary {
			position: static;
		}

		.cart-item {
			grid-template-columns: 80px 1fr;
			gap: 1rem;
		}

		.item-image {
			width: 80px;
			height: 80px;
		}

		.item-quantity,
		.item-subtotal {
			grid-column: 2;
		}

		.remove-btn {
			grid-column: 2;
			justify-self: end;
		}
	}
</style>
