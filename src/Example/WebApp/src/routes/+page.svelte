<script lang="ts">
	import ProductCard from '$lib/components/ProductCard.svelte';
	import SearchBar from '$lib/components/SearchBar.svelte';
	import type { PageData } from './$types';

	export let data: PageData;
</script>

<div class="home">
	<div class="hero">
		<h1>🔍 AI-Powered Product Search</h1>
		<p class="subtitle">
			Experience semantic search powered by Weaviate vector database and C# managed client
		</p>
	</div>

	<SearchBar />

	<section class="features">
		<div class="feature">
			<h3>🧠 Semantic Search</h3>
			<p>Find products by meaning, not just keywords. Search for "running shoes" and get relevant athletic footwear.</p>
		</div>
		<div class="feature">
			<h3>⚡ Hybrid Search</h3>
			<p>Combine AI-powered semantic search with traditional keyword matching for best results.</p>
		</div>
		<div class="feature">
			<h3>🎯 Smart Recommendations</h3>
			<p>Get similar product suggestions using vector similarity for accurate recommendations.</p>
		</div>
	</section>

	<section class="products-section">
		<h2>Featured Products</h2>
		{#if data.products.length > 0}
			<div class="products-grid">
				{#each data.products as product}
					<ProductCard {product} />
				{/each}
			</div>
		{:else}
			<p class="no-products">No products available. Make sure the backend is running.</p>
		{/if}
	</section>
</div>

<style>
	.hero {
		text-align: center;
		margin-bottom: 3rem;
	}

	.hero h1 {
		font-size: 2.5rem;
		margin: 0 0 1rem 0;
		color: #333;
	}

	.subtitle {
		font-size: 1.125rem;
		color: #666;
		margin: 0;
	}

	.features {
		display: grid;
		grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
		gap: 2rem;
		margin-bottom: 3rem;
	}

	.feature {
		background: white;
		padding: 1.5rem;
		border-radius: 8px;
		box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
	}

	.feature h3 {
		margin: 0 0 0.5rem 0;
		color: #333;
		font-size: 1.25rem;
	}

	.feature p {
		margin: 0;
		color: #666;
		line-height: 1.5;
	}

	.products-section h2 {
		font-size: 1.75rem;
		margin: 0 0 1.5rem 0;
		color: #333;
	}

	.products-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
		gap: 1.5rem;
	}

	.no-products {
		text-align: center;
		color: #999;
		padding: 3rem;
		background: white;
		border-radius: 8px;
	}
</style>
