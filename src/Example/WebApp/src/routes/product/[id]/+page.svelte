<script lang="ts">
	import LoadingSpinner from '$lib/components/LoadingSpinner.svelte';
	import ProductCard from '$lib/components/ProductCard.svelte';
	import StarRating from '$lib/components/StarRating.svelte';
	import { cartStore } from '$lib/stores/cartStore';
	import { showSuccess } from '$lib/stores/toastStore';
	import { wishlistStore } from '$lib/stores/wishlistStore';
	import { onMount } from 'svelte';
	import type { PageData } from './$types';

	export let data: PageData;

	let isLoading = true;
	let showReviewForm = false;

	onMount(() => {
		isLoading = false;
	});

	$: product = data.product;
	$: similar = data.similar;
	$: reviews = data.reviews || [];
	$: inStock = product.stock > 0;
	$: isInWishlist = wishlistStore.isInWishlist($wishlistStore, product.uuid);
	$: averageRating = reviews.length > 0
		? reviews.reduce((sum, r) => sum + r.rating, 0) / reviews.length
		: product.rating;

	function addToCart() {
		cartStore.addItem(product, 1);
		showSuccess(`Added ${product.name} to cart!`);
	}

	function toggleWishlist() {
		wishlistStore.toggleItem(product);
		if (isInWishlist) {
			showSuccess('Removed from wishlist');
		} else {
			showSuccess(`Added ${product.name} to wishlist!`);
		}
	}

	function formatDate(dateString: string): string {
		const date = new Date(dateString);
		return date.toLocaleDateString('en-US', {
			year: 'numeric',
			month: 'long',
			day: 'numeric'
		});
	}
</script>

<svelte:head>
	<title>{product.name} - AI Product Search</title>
</svelte:head>

{#if isLoading}
	<LoadingSpinner size="large" />
{:else}
<div class="product-page">
	<!-- Breadcrumb -->
	<div class="breadcrumb">
		<a href="/">Home</a>
		<span class="separator">›</span>
		<a href="/search">Search</a>
		<span class="separator">›</span>
		<span class="category">{product.category}</span>
		<span class="separator">›</span>
		<span class="current">{product.name}</span>
	</div>

	<!-- Product Details -->
	<div class="product-details">
		<div class="product-image">
			<img src={product.imageUrl} alt={product.name} />
			{#if !inStock}
				<div class="out-of-stock-badge">Out of Stock</div>
			{/if}
		</div>

		<div class="product-info">
			<div class="category-badge">{product.category}</div>
			<h1>{product.name}</h1>

			<div class="rating-row">
				<StarRating rating={averageRating} />
				{#if reviews.length > 0}
					<span class="review-count">({reviews.length} {reviews.length === 1 ? 'review' : 'reviews'})</span>
				{/if}
				<span class="brand">by {product.brand}</span>
			</div>

			<p class="description">{product.description}</p>

			<div class="price-section">
				<div class="price">${product.price?.toFixed(2) ?? '0.00'}</div>
				<div class="stock-info">
					{#if inStock}
						<span class="in-stock">✓ In Stock ({product.stock} available)</span>
					{:else}
						<span class="out-of-stock">✗ Out of Stock</span>
					{/if}
				</div>
			</div>

			{#if product.specifications}
				<div class="specifications">
					<h3>Specifications</h3>
					<div class="spec-grid">
						{#if product.specifications.color}
							<div class="spec-item">
								<span class="spec-label">Color:</span>
								<span class="spec-value">{product.specifications.color}</span>
							</div>
						{/if}
						{#if product.specifications.size}
							<div class="spec-item">
								<span class="spec-label">Size:</span>
								<span class="spec-value">{product.specifications.size}</span>
							</div>
						{/if}
						{#if product.specifications.material}
							<div class="spec-item">
								<span class="spec-label">Material:</span>
								<span class="spec-value">{product.specifications.material}</span>
							</div>
						{/if}
						{#if product.specifications.weight}
							<div class="spec-item">
								<span class="spec-label">Weight:</span>
								<span class="spec-value">{product.specifications.weight}</span>
							</div>
						{/if}
						{#if product.specifications.dimensions}
							<div class="spec-item">
								<span class="spec-label">Dimensions:</span>
								<span class="spec-value">{product.specifications.dimensions}</span>
							</div>
						{/if}
					</div>
				</div>
			{/if}

			<div class="actions">
				<button
					class="add-to-cart"
					disabled={!inStock}
					on:click={addToCart}
				>
					{inStock ? 'Add to Cart' : 'Out of Stock'}
				</button>
				<button
					class="wishlist"
					class:active={isInWishlist}
					on:click={toggleWishlist}
				>
					{isInWishlist ? '♥' : '♡'} {isInWishlist ? 'In Wishlist' : 'Add to Wishlist'}
				</button>
			</div>
		</div>
	</div>

	<!-- Similar Products -->
	{#if similar.length > 0}
		<div class="similar-section">
			<h2>Similar Products</h2>
			<p class="section-subtitle">Found using AI vector similarity</p>
			<div class="similar-grid">
				{#each similar as result}
					<ProductCard
						product={result.object}
						showDistance={true}
						distance={result.metadata?.distance}
					/>
				{/each}
			</div>
		</div>
	{/if}

	<!-- Customer Reviews -->
	<div class="reviews-section">
		<div class="reviews-header">
			<div>
				<h2>Customer Reviews</h2>
				{#if reviews.length > 0}
					<div class="reviews-summary">
						<StarRating rating={averageRating} size="large" />
						<span class="average-text">{averageRating.toFixed(1)} out of 5</span>
					</div>
				{:else}
					<p class="no-reviews">No reviews yet. Be the first to review this product!</p>
				{/if}
			</div>
			<button class="write-review-btn" on:click={() => (showReviewForm = !showReviewForm)}>
				{showReviewForm ? 'Cancel' : 'Write a Review'}
			</button>
		</div>

		{#if showReviewForm}
			<div class="review-form-container">
				<h3>Write Your Review</h3>
				<p class="form-notice">Coming soon: Review submission form</p>
			</div>
		{/if}

		<div class="reviews-list">
			{#each reviews as review}
				<div class="review-card">
					<div class="review-header">
						<div>
							<div class="reviewer-info">
								<span class="reviewer-name">{review.reviewerName}</span>
								<span class="review-date">{formatDate(review.reviewDate)}</span>
							</div>
							<h4 class="review-title">{review.title}</h4>
							<StarRating rating={review.rating} size="small" />
						</div>
					</div>
					<p class="review-content">{review.content}</p>
				</div>
			{/each}
		</div>
	</div>
</div>
{/if}

<style>
	.product-page {
		max-width: 1200px;
		margin: 0 auto;
		padding: 2rem 1rem;
	}

	.breadcrumb {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		margin-bottom: 2rem;
		font-size: 0.875rem;
		color: #6b7280;
	}

	.breadcrumb a {
		color: #3b82f6;
		text-decoration: none;
	}

	.breadcrumb a:hover {
		text-decoration: underline;
	}

	.separator {
		color: #d1d5db;
	}

	.category {
		color: #6b7280;
	}

	.current {
		color: #111827;
		font-weight: 500;
	}

	.product-details {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 3rem;
		background: white;
		padding: 2rem;
		border-radius: 12px;
		margin-bottom: 3rem;
	}

	.product-image {
		position: relative;
		aspect-ratio: 1;
		background: #f9fafb;
		border-radius: 8px;
		overflow: hidden;
	}

	.product-image img {
		width: 100%;
		height: 100%;
		object-fit: cover;
	}

	.out-of-stock-badge {
		position: absolute;
		top: 1rem;
		right: 1rem;
		background: #ef4444;
		color: white;
		padding: 0.5rem 1rem;
		border-radius: 6px;
		font-weight: 600;
		font-size: 0.875rem;
	}

	.product-info {
		display: flex;
		flex-direction: column;
		gap: 1rem;
	}

	.category-badge {
		display: inline-block;
		width: fit-content;
		padding: 0.25rem 0.75rem;
		background: #eff6ff;
		color: #3b82f6;
		border-radius: 6px;
		font-size: 0.875rem;
		font-weight: 500;
	}

	.product-info h1 {
		margin: 0;
		font-size: 2rem;
		font-weight: 700;
		color: #111827;
		line-height: 1.2;
	}

	.rating-row {
		display: flex;
		align-items: center;
		gap: 1rem;
	}

	.review-count {
		color: #6b7280;
		font-size: 0.875rem;
	}

	.brand {
		color: #6b7280;
		font-size: 1rem;
	}

	.description {
		margin: 0;
		color: #374151;
		font-size: 1rem;
		line-height: 1.6;
	}

	.price-section {
		padding: 1.5rem 0;
		border-top: 1px solid #e5e7eb;
		border-bottom: 1px solid #e5e7eb;
	}

	.price {
		font-size: 2.5rem;
		font-weight: 700;
		color: #059669;
		margin-bottom: 0.5rem;
	}

	.stock-info {
		font-size: 0.875rem;
	}

	.in-stock {
		color: #059669;
		font-weight: 500;
	}

	.out-of-stock {
		color: #ef4444;
		font-weight: 500;
	}

	.specifications {
		background: #f9fafb;
		padding: 1.5rem;
		border-radius: 8px;
	}

	.specifications h3 {
		margin: 0 0 1rem 0;
		font-size: 1.125rem;
		font-weight: 600;
		color: #111827;
	}

	.spec-grid {
		display: grid;
		gap: 0.75rem;
	}

	.spec-item {
		display: flex;
		justify-content: space-between;
		align-items: center;
	}

	.spec-label {
		font-weight: 500;
		color: #6b7280;
		font-size: 0.875rem;
	}

	.spec-value {
		color: #111827;
		font-size: 0.875rem;
	}

	.actions {
		display: flex;
		gap: 1rem;
		margin-top: 1rem;
	}

	.add-to-cart {
		flex: 1;
		padding: 1rem 2rem;
		background: #3b82f6;
		color: white;
		border: none;
		border-radius: 8px;
		font-size: 1.125rem;
		font-weight: 600;
		cursor: pointer;
		transition: background 0.2s;
	}

	.add-to-cart:hover:not(:disabled) {
		background: #2563eb;
	}

	.add-to-cart:disabled {
		background: #d1d5db;
		cursor: not-allowed;
	}

	.wishlist {
		padding: 1rem 2rem;
		background: white;
		color: #374151;
		border: 2px solid #e5e7eb;
		border-radius: 8px;
		font-size: 1rem;
		font-weight: 600;
		cursor: pointer;
		transition: all 0.2s;
	}

	.wishlist:hover {
		border-color: #3b82f6;
		color: #3b82f6;
	}

	.wishlist.active {
		background: #fef2f2;
		border-color: #ef4444;
		color: #ef4444;
	}

	.wishlist.active:hover {
		background: #fee2e2;
	}

	.similar-section {
		margin-top: 3rem;
	}

	.similar-section h2 {
		margin: 0 0 0.5rem 0;
		font-size: 1.75rem;
		font-weight: 700;
		color: #111827;
	}

	.section-subtitle {
		margin: 0 0 1.5rem 0;
		color: #6b7280;
		font-size: 0.875rem;
	}

	.similar-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
		gap: 1.5rem;
	}

	/* Reviews Section */
	.reviews-section {
		margin-top: 3rem;
		background: white;
		padding: 2rem;
		border-radius: 12px;
	}

	.reviews-header {
		display: flex;
		justify-content: space-between;
		align-items: flex-start;
		margin-bottom: 2rem;
	}

	.reviews-header h2 {
		margin: 0 0 1rem 0;
		font-size: 1.75rem;
		font-weight: 700;
		color: #111827;
	}

	.reviews-summary {
		display: flex;
		align-items: center;
		gap: 1rem;
		margin-top: 0.5rem;
	}

	.average-text {
		font-size: 1.125rem;
		color: #374151;
		font-weight: 500;
	}

	.no-reviews {
		color: #6b7280;
		font-size: 0.875rem;
		margin: 0.5rem 0 0 0;
	}

	.write-review-btn {
		padding: 0.75rem 1.5rem;
		background: #3b82f6;
		color: white;
		border: none;
		border-radius: 8px;
		font-size: 0.875rem;
		font-weight: 600;
		cursor: pointer;
		transition: background 0.2s;
	}

	.write-review-btn:hover {
		background: #2563eb;
	}

	.review-form-container {
		background: #f9fafb;
		padding: 1.5rem;
		border-radius: 8px;
		margin-bottom: 2rem;
	}

	.review-form-container h3 {
		margin: 0 0 0.5rem 0;
		font-size: 1.125rem;
		font-weight: 600;
		color: #111827;
	}

	.form-notice {
		color: #6b7280;
		font-size: 0.875rem;
		margin: 0;
	}

	.reviews-list {
		display: flex;
		flex-direction: column;
		gap: 1.5rem;
	}

	.review-card {
		border-bottom: 1px solid #e5e7eb;
		padding-bottom: 1.5rem;
	}

	.review-card:last-child {
		border-bottom: none;
		padding-bottom: 0;
	}

	.review-header {
		display: flex;
		justify-content: space-between;
		align-items: flex-start;
		margin-bottom: 0.75rem;
	}

	.reviewer-info {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		margin-bottom: 0.5rem;
	}

	.reviewer-name {
		font-weight: 600;
		color: #111827;
		font-size: 0.875rem;
	}

	.review-date {
		color: #9ca3af;
		font-size: 0.75rem;
	}

	.review-title {
		margin: 0 0 0.5rem 0;
		font-size: 1.125rem;
		font-weight: 600;
		color: #111827;
	}

	.review-content {
		margin: 0;
		color: #374151;
		font-size: 0.875rem;
		line-height: 1.6;
	}

	@media (max-width: 768px) {
		.product-details {
			grid-template-columns: 1fr;
			gap: 1.5rem;
		}

		.product-info h1 {
			font-size: 1.5rem;
		}

		.price {
			font-size: 2rem;
		}

		.actions {
			flex-direction: column;
		}

		.similar-grid {
			grid-template-columns: 1fr;
		}

		.reviews-header {
			flex-direction: column;
			gap: 1rem;
		}

		.write-review-btn {
			width: 100%;
		}
	}
</style>
