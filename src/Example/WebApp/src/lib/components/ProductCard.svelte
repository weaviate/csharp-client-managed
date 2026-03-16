<script lang="ts">
	import type { Product } from '$lib/types';
	import StarRating from './StarRating.svelte';

	export let product: Product;
	export let showDistance = false;
	export let distance: number | undefined = undefined;
</script>

<a href="/product/{product.uuid}" class="product-card">
	<div class="image-container">
		<img src={product.imageUrl} alt={product.name} />
		{#if showDistance && distance != null}
			<div class="badge">Distance: {distance.toFixed(3)}</div>
		{/if}
	</div>
	<div class="content">
		<h3>{product.name}</h3>
		<p class="category">{product.category}</p>
		<p class="description">{product.description}</p>
		<div class="footer">
			<span class="price">${product.price?.toFixed(2) ?? '0.00'}</span>
			<StarRating rating={product.rating ?? 0} size="small" />
		</div>
		{#if product.specs?.color}
			<div class="specs">
				<span class="spec-badge">{product.specs.color}</span>
				{#if product.specs.size}
					<span class="spec-badge">{product.specs.size}</span>
				{/if}
			</div>
		{/if}
	</div>
</a>

<style>
	.product-card {
		display: block;
		background: white;
		border-radius: 8px;
		overflow: hidden;
		box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
		transition: transform 0.2s, box-shadow 0.2s;
		text-decoration: none;
		color: inherit;
	}

	.product-card:hover {
		transform: translateY(-4px);
		box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
	}

	.image-container {
		position: relative;
		width: 100%;
		height: 200px;
		overflow: hidden;
		background: #f0f0f0;
	}

	img {
		width: 100%;
		height: 100%;
		object-fit: cover;
	}

	.badge {
		position: absolute;
		top: 8px;
		right: 8px;
		background: rgba(0, 123, 255, 0.9);
		color: white;
		padding: 4px 8px;
		border-radius: 4px;
		font-size: 0.75rem;
		font-weight: 500;
	}

	.content {
		padding: 1rem;
	}

	h3 {
		margin: 0 0 0.5rem 0;
		font-size: 1.125rem;
		font-weight: 600;
		color: #333;
	}

	.category {
		margin: 0 0 0.5rem 0;
		font-size: 0.875rem;
		color: #007bff;
		font-weight: 500;
	}

	.description {
		margin: 0 0 1rem 0;
		font-size: 0.875rem;
		color: #666;
		line-height: 1.4;
		display: -webkit-box;
		-webkit-line-clamp: 2;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}

	.footer {
		display: flex;
		justify-content: space-between;
		align-items: center;
		margin-bottom: 0.5rem;
	}

	.price {
		font-size: 1.25rem;
		font-weight: 700;
		color: #28a745;
	}

	.specs {
		display: flex;
		gap: 0.5rem;
		flex-wrap: wrap;
	}

	.spec-badge {
		display: inline-block;
		padding: 2px 8px;
		background: #e9ecef;
		border-radius: 4px;
		font-size: 0.75rem;
		color: #495057;
	}
</style>
