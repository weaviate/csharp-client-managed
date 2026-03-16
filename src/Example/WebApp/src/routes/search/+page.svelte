<script lang="ts">
	import { goto } from '$app/navigation';
	import LoadingSpinner from '$lib/components/LoadingSpinner.svelte';
	import ProductCard from '$lib/components/ProductCard.svelte';
	import SearchBar from '$lib/components/SearchBar.svelte';
	import type { PageData } from './$types';

	export let data: PageData;

	let isNavigating = false;

	// Handle navigation loading state
	function navigateWithLoading(url: string) {
		isNavigating = true;
		goto(url).finally(() => {
			isNavigating = false;
		});
	}

	function handleCategoryClick(category: string) {
		const params = new URLSearchParams(window.location.search);
		// Toggle category - if already selected, deselect it
		if (data.category === category) {
			params.delete('category');
		} else {
			params.set('category', category);
		}
		navigateWithLoading(`/search?${params.toString()}`);
	}

	function handleBrandClick(brand: string) {
		const params = new URLSearchParams(window.location.search);
		// Toggle brand - if already selected, deselect it
		if (data.brand === brand) {
			params.delete('brand');
		} else {
			params.set('brand', brand);
		}
		navigateWithLoading(`/search?${params.toString()}`);
	}

	function handlePriceFilter(minPrice?: number, maxPrice?: number) {
		const params = new URLSearchParams(window.location.search);
		if (minPrice !== undefined) {
			params.set('minPrice', minPrice.toString());
		} else {
			params.delete('minPrice');
		}
		if (maxPrice !== undefined) {
			params.set('maxPrice', maxPrice.toString());
		} else {
			params.delete('maxPrice');
		}
		navigateWithLoading(`/search?${params.toString()}`);
	}

	function handleRatingFilter(minRating?: number) {
		const params = new URLSearchParams(window.location.search);
		if (minRating !== undefined) {
			params.set('minRating', minRating.toString());
		} else {
			params.delete('minRating');
		}
		navigateWithLoading(`/search?${params.toString()}`);
	}

	function clearFilters() {
		const params = new URLSearchParams(window.location.search);
		params.delete('category');
		params.delete('brand');
		params.delete('minPrice');
		params.delete('maxPrice');
		params.delete('minRating');
		params.delete('page');
		navigateWithLoading(`/search?${params.toString()}`);
	}

	function goToPage(page: number) {
		const params = new URLSearchParams(window.location.search);
		if (page === 1) {
			params.delete('page');
		} else {
			params.set('page', page.toString());
		}
		navigateWithLoading(`/search?${params.toString()}`);
		// Scroll to top of results
		window.scrollTo({ top: 0, behavior: 'smooth' });
	}

	function handleSortChange(event: Event) {
		const select = event.target as HTMLSelectElement;
		const params = new URLSearchParams(window.location.search);
		if (select.value === 'relevance') {
			params.delete('sortBy');
		} else {
			params.set('sortBy', select.value);
		}
		params.delete('page');
		navigateWithLoading(`/search?${params.toString()}`);
	}

	$: hasFilters = data.category || data.brand || data.minPrice || data.maxPrice || data.minRating;
</script>

<svelte:head>
	<title>Search - AI Product Search</title>
</svelte:head>

<div class="search-page">
	<div class="search-header">
		<SearchBar initialQuery={data.query} initialMode={data.mode} />
	</div>

	<div class="search-content">
		<aside class="sidebar">
			<div class="filter-section">
				<div class="filter-header">
					<h3>Filters</h3>
					{#if hasFilters}
						<button class="clear-btn" on:click={clearFilters}>Clear All</button>
					{/if}
				</div>

				<!-- Categories -->
				<div class="filter-group">
					<h4>Categories</h4>
					<div class="category-list">
						{#each data.facets.categories as { category, count }}
							<button
								class="category-item"
								class:active={data.category === category}
								on:click={() => handleCategoryClick(category)}
								aria-pressed={data.category === category}
								aria-label={`Filter by ${category} category`}
							>
								<span class="category-name">{category}</span>
								<span class="category-count">{count}</span>
							</button>
						{/each}
					</div>
				</div>

				<!-- Price Range -->
				<div class="filter-group">
					<h4>Price Range</h4>
					<div class="price-inputs">
						<input
							id="minPrice"
							name="minPrice"
							type="number"
							placeholder="Min"
							aria-label="Minimum price"
							value={data.minPrice || ''}
							on:change={(e) => handlePriceFilter(parseFloat(e.currentTarget.value) || undefined, data.maxPrice)}
						/>
						<span>to</span>
						<input
							id="maxPrice"
							name="maxPrice"
							type="number"
							placeholder="Max"
							aria-label="Maximum price"
							value={data.maxPrice || ''}
							on:change={(e) => handlePriceFilter(data.minPrice, parseFloat(e.currentTarget.value) || undefined)}
						/>
					</div>
				</div>

				<!-- Rating -->
				<div class="filter-group">
					<h4>Minimum Rating</h4>
					<div class="rating-options">
						{#each [4.5, 4.0, 3.5, 3.0] as rating}
							<button
								class="rating-btn"
								class:active={data.minRating === rating}
								on:click={() => handleRatingFilter(rating === data.minRating ? undefined : rating)}
								aria-pressed={data.minRating === rating}
								aria-label={`Filter by minimum rating ${rating} stars`}
							>
								⭐ {rating}+
							</button>
						{/each}
					</div>
				</div>

				<!-- Brands -->
				{#if data.facets.brands.length > 0}
					<div class="filter-group">
						<h4>Brands</h4>
						<div class="brand-list">
							{#each data.facets.brands as { brand, count }}
								<button
									class="brand-item"
									class:active={data.brand === brand}
									on:click={() => handleBrandClick(brand)}
									aria-pressed={data.brand === brand}
									aria-label={`Filter by ${brand} brand`}
								>
									<span class="brand-name">{brand}</span>
									<span class="brand-count">{count}</span>
								</button>
							{/each}
						</div>
					</div>
				{/if}
			</div>
		</aside>

		<main class="results">
			{#if data.query}
				<div class="results-header">
					<div>
						<h2>Search Results</h2>
						<p class="results-meta">
							Found {data.results.length} results for <strong>"{data.query}"</strong>
							{#if hasFilters}
								<span class="with-filters">(with filters)</span>
							{/if}
						</p>
					</div>
					<div class="sort-dropdown">
						<label for="sort">Sort:</label>
						<select id="sort" value={data.sortBy || 'relevance'} on:change={handleSortChange}>
							<option value="relevance">Relevance</option>
							<option value="price-asc">Price: Low to High</option>
							<option value="price-desc">Price: High to Low</option>
							<option value="rating-desc">Rating: High to Low</option>
						</select>
					</div>
				</div>

				{#if data.results.length === 0}
					<div class="no-results">
						<p>No products found matching your search.</p>
						{#if hasFilters}
							<button class="clear-filters-btn" on:click={clearFilters}>
								Try clearing filters
							</button>
						{/if}
					</div>
				{:else}
					<div class="results-grid">
						{#each data.results as result}
							<ProductCard
								product={result.object}
								showDistance={data.mode !== 'keyword'}
								distance={result.metadata?.distance}
							/>
						{/each}
					</div>
				{/if}

				<!-- Pagination -->
				{#if data.query && data.results.length > 0}
					<div class="pagination">
						{#if data.page > 1}
							<button class="page-btn" on:click={() => goToPage(data.page - 1)}>
								← Previous
							</button>
						{/if}

						<span class="page-info">
							Page {data.page}
						</span>

						{#if data.hasMore}
							<button class="page-btn" on:click={() => goToPage(data.page + 1)}>
								Next →
							</button>
						{/if}
					</div>
				{/if}

				{#if isNavigating}
					<LoadingSpinner size="large" />
				{:else if !data.results.length}
					<div class="empty-state">
						<h2>🔍 Start Your Search</h2>
						<p>Enter a search query above to find products using AI-powered semantic search.</p>
						<div class="search-tips">
							<h3>Search Tips:</h3>
							<ul>
								<li><strong>Semantic:</strong> Find products by meaning (e.g., "device for taking photos")</li>
								<li><strong>Hybrid:</strong> Combine meaning with keywords for best results</li>
								<li><strong>Keyword:</strong> Traditional exact match search</li>
							</ul>
						</div>
					</div>
				{/if}
			{:else}
				<div class="empty-state">
					<h2>🔍 Start Your Search</h2>
					<p>Enter a search query above to find products using AI-powered semantic search.</p>
					<div class="search-tips">
						<h3>Search Tips:</h3>
						<ul>
							<li><strong>Semantic:</strong> Find products by meaning (e.g., "device for taking photos")</li>
							<li><strong>Hybrid:</strong> Combine meaning with keywords for best results</li>
							<li><strong>Keyword:</strong> Traditional exact match search</li>
						</ul>
					</div>
				</div>
			{/if}
		</main>
	</div>
</div>

<style>
	.search-page {
		width: 100%;
	}

	.search-header {
		background: white;
		padding: 2rem;
		border-bottom: 1px solid #e5e7eb;
		margin-bottom: 2rem;
	}

	.search-content {
		display: grid;
		grid-template-columns: 280px 1fr;
		gap: 2rem;
		max-width: 1400px;
		margin: 0 auto;
		padding: 0 1rem;
	}

	.sidebar {
		background: white;
		border-radius: 8px;
		padding: 1.5rem;
		height: fit-content;
		position: sticky;
		top: 2rem;
	}

	.filter-header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		margin-bottom: 1.5rem;
	}

	.filter-header h3 {
		margin: 0;
		font-size: 1.25rem;
		font-weight: 600;
	}

	.clear-btn {
		background: none;
		border: none;
		color: #3b82f6;
		cursor: pointer;
		font-size: 0.875rem;
		padding: 0.25rem 0.5rem;
	}

	.clear-btn:hover {
		text-decoration: underline;
	}

	.filter-group {
		margin-bottom: 2rem;
	}

	.filter-group h4 {
		margin: 0 0 1rem 0;
		font-size: 1rem;
		font-weight: 600;
		color: #374151;
	}

	.category-list {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.category-item {
		display: flex;
		justify-content: space-between;
		align-items: center;
		padding: 0.5rem 0.75rem;
		background: #f9fafb;
		border: 1px solid #e5e7eb;
		border-radius: 6px;
		cursor: pointer;
		transition: all 0.2s;
		text-align: left;
	}

	.category-item:hover {
		background: #f3f4f6;
		border-color: #3b82f6;
	}

	.category-item.active {
		background: #eff6ff;
		border-color: #3b82f6;
		font-weight: 600;
	}

	.category-name {
		font-size: 0.875rem;
	}

	.category-count {
		font-size: 0.75rem;
		color: #6b7280;
		background: white;
		padding: 0.125rem 0.5rem;
		border-radius: 12px;
	}

	.price-inputs {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		width: 100%;
	}

	.price-inputs input {
		flex: 1;
		min-width: 0;
		padding: 0.5rem;
		border: 1px solid #e5e7eb;
		border-radius: 6px;
		font-size: 0.875rem;
		width: 100%;
		box-sizing: border-box;
	}

	.price-inputs span {
		color: #6b7280;
		font-size: 0.75rem;
		flex-shrink: 0;
	}

	.rating-options {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.rating-btn {
		padding: 0.5rem;
		background: #f9fafb;
		border: 1px solid #e5e7eb;
		border-radius: 6px;
		cursor: pointer;
		font-size: 0.875rem;
		transition: all 0.2s;
	}

	.rating-btn:hover {
		background: #f3f4f6;
		border-color: #3b82f6;
	}

	.rating-btn.active {
		background: #eff6ff;
		border-color: #3b82f6;
		font-weight: 600;
	}

	.brand-list {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
	}

	.brand-item {
		display: flex;
		justify-content: space-between;
		align-items: center;
		padding: 0.75rem;
		background: #f9fafb;
		border: 1px solid #e5e7eb;
		border-radius: 6px;
		cursor: pointer;
		font-size: 0.875rem;
		transition: all 0.2s;
		width: 100%;
		text-align: left;
	}

	.brand-item:hover {
		background: #f3f4f6;
		border-color: #3b82f6;
	}

	.brand-item.active {
		background: #eff6ff;
		border-color: #3b82f6;
		font-weight: 600;
	}

	.brand-name {
		color: #374151;
	}

	.brand-count {
		color: #6b7280;
		font-size: 0.75rem;
		background: white;
		padding: 0.125rem 0.5rem;
		border-radius: 12px;
	}

	.results {
		min-height: 400px;
	}

	.results-header {
		margin-bottom: 2rem;
		display: flex;
		justify-content: space-between;
		align-items: flex-end;
		gap: 1rem;
	}

	.results-header h2 {
		margin: 0 0 0.5rem 0;
		font-size: 1.75rem;
		font-weight: 700;
	}

	.results-meta {
		margin: 0;
		color: #6b7280;
		font-size: 0.875rem;
	}

	.with-filters {
		color: #3b82f6;
	}

	.sort-dropdown {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		white-space: nowrap;
	}

	.sort-dropdown label {
		font-weight: 500;
		color: #374151;
	}

	.sort-dropdown select {
		padding: 0.5rem 1rem;
		border: 1px solid #d1d5db;
		border-radius: 6px;
		font-size: 0.875rem;
		cursor: pointer;
		background: white;
	}

	.sort-dropdown select:focus {
		outline: none;
		border-color: #3b82f6;
		box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
	}

	.no-results {
		background: white;
		padding: 3rem;
		border-radius: 8px;
		text-align: center;
	}

	.no-results p {
		margin: 0 0 1rem 0;
		color: #6b7280;
		font-size: 1.125rem;
	}

	.clear-filters-btn {
		padding: 0.75rem 1.5rem;
		background: #3b82f6;
		color: white;
		border: none;
		border-radius: 6px;
		cursor: pointer;
		font-size: 1rem;
	}

	.clear-filters-btn:hover {
		background: #2563eb;
	}

	.results-grid {
		display: grid;
		grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
		gap: 1.5rem;
	}

	.empty-state {
		background: white;
		padding: 3rem;
		border-radius: 8px;
		text-align: center;
	}

	.empty-state h2 {
		margin: 0 0 1rem 0;
		font-size: 2rem;
		font-weight: 700;
		color: #111827;
	}

	.empty-state > p {
		margin: 0 0 2rem 0;
		color: #6b7280;
		font-size: 1.125rem;
	}

	.search-tips {
		background: #f9fafb;
		padding: 1.5rem;
		border-radius: 8px;
		text-align: left;
		max-width: 600px;
		margin: 0 auto;
	}

	.search-tips h3 {
		margin: 0 0 1rem 0;
		font-size: 1.125rem;
		font-weight: 600;
	}

	.search-tips ul {
		margin: 0;
		padding-left: 1.5rem;
	}

	.search-tips li {
		margin-bottom: 0.75rem;
		color: #374151;
		line-height: 1.6;
	}

	.pagination {
		display: flex;
		justify-content: center;
		align-items: center;
		gap: 1rem;
		margin-top: 2rem;
		padding: 1.5rem 0;
	}

	.page-btn {
		background: white;
		border: 1px solid #e5e7eb;
		color: #3b82f6;
		padding: 0.5rem 1rem;
		border-radius: 6px;
		cursor: pointer;
		font-weight: 500;
		transition: all 0.2s;
	}

	.page-btn:hover {
		background: #3b82f6;
		color: white;
		border-color: #3b82f6;
	}

	.page-info {
		color: #6b7280;
		font-weight: 500;
	}

	@media (max-width: 768px) {
		.search-content {
			grid-template-columns: 1fr;
			gap: 1rem;
		}

		.sidebar {
			position: static;
			padding: 1rem;
		}

		.results {
			padding: 1rem;
		}

		.results-header {
			flex-direction: column;
			align-items: flex-start;
			gap: 0.75rem;
		}

		.results-header h2 {
			font-size: 1.5rem;
		}

		.sort-dropdown {
			width: 100%;
		}

		.sort-dropdown select {
			flex: 1;
		}

		.results-grid {
			grid-template-columns: 1fr;
		}

		.pagination {
			flex-wrap: wrap;
		}

		.page-btn {
			font-size: 0.875rem;
			padding: 0.375rem 0.75rem;
		}
	}
</style>
