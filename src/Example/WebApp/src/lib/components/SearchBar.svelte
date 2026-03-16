<script lang="ts">
	import { goto } from '$app/navigation';

	export let initialQuery = '';
	export let initialMode: 'semantic' | 'hybrid' | 'keyword' = 'semantic';

	let query = initialQuery;
	let mode = initialMode;

	function handleSearch() {
		if (!query.trim()) return;
		goto(`/search?q=${encodeURIComponent(query)}&mode=${mode}`);
	}

	function handleKeydown(event: KeyboardEvent) {
		if (event.key === 'Enter') {
			handleSearch();
		}
	}
</script>

<div class="search-bar">
	<div class="input-group">
		<input
			type="text"
			bind:value={query}
			on:keydown={handleKeydown}
			placeholder="Search for products... (e.g., 'running shoes', 'wireless headphones')"
			class="search-input"
		/>
		<button on:click={handleSearch} class="search-button"> 🔍 Search </button>
	</div>

	<div class="mode-selector">
		<label>
			<input type="radio" bind:group={mode} value="semantic" />
			<span>Semantic</span>
			<small>(AI-powered meaning search)</small>
		</label>
		<label>
			<input type="radio" bind:group={mode} value="hybrid" />
			<span>Hybrid</span>
			<small>(AI + keyword combined)</small>
		</label>
		<label>
			<input type="radio" bind:group={mode} value="keyword" />
			<span>Keyword</span>
			<small>(BM25 traditional search)</small>
		</label>
	</div>
</div>

<style>
	.search-bar {
		background: white;
		padding: 2rem;
		border-radius: 8px;
		box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
		margin-bottom: 2rem;
	}

	.input-group {
		display: flex;
		gap: 1rem;
		margin-bottom: 1rem;
	}

	.search-input {
		flex: 1;
		padding: 0.75rem 1rem;
		font-size: 1rem;
		border: 2px solid #e0e0e0;
		border-radius: 4px;
		outline: none;
		transition: border-color 0.2s;
	}

	.search-input:focus {
		border-color: #007bff;
	}

	.search-button {
		padding: 0.75rem 2rem;
		font-size: 1rem;
		font-weight: 600;
		background: #007bff;
		color: white;
		border: none;
		border-radius: 4px;
		cursor: pointer;
		transition: background 0.2s;
	}

	.search-button:hover {
		background: #0056b3;
	}

	.mode-selector {
		display: flex;
		gap: 2rem;
		flex-wrap: wrap;
	}

	.mode-selector label {
		display: flex;
		align-items: center;
		gap: 0.5rem;
		cursor: pointer;
	}

	.mode-selector input[type='radio'] {
		cursor: pointer;
	}

	.mode-selector span {
		font-weight: 500;
		color: #333;
	}

	.mode-selector small {
		color: #666;
		font-size: 0.75rem;
	}
</style>
