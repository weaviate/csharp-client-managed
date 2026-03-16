<script lang="ts">
	import { page } from '$app/stores';
</script>

<svelte:head>
	<title>Error - AI Product Search</title>
</svelte:head>

<div class="error-page">
	<div class="error-content">
		<h1>{$page.status}</h1>
		<h2>{$page.error?.message || 'Something went wrong'}</h2>
		<p>We're sorry, but the page you're looking for could not be found.</p>
		<div class="actions">
			<a href="/" class="home-btn">Go Home</a>
			<a href="/search" class="search-btn">Search Products</a>
		</div>
	</div>
</div>

<style>
	.error-page {
		display: flex;
		align-items: center;
		justify-content: center;
		min-height: 60vh;
		padding: 2rem;
	}

	.error-content {
		text-align: center;
		max-width: 600px;
	}

	.error-content h1 {
		font-size: 6rem;
		font-weight: 700;
		color: #3b82f6;
		margin: 0;
	}

	.error-content h2 {
		font-size: 1.5rem;
		font-weight: 600;
		color: #111827;
		margin: 1rem 0;
	}

	.error-content p {
		color: #6b7280;
		font-size: 1.125rem;
		margin: 1rem 0 2rem 0;
	}

	.actions {
		display: flex;
		gap: 1rem;
		justify-content: center;
	}

	.home-btn,
	.search-btn {
		padding: 0.75rem 1.5rem;
		border-radius: 8px;
		font-weight: 600;
		text-decoration: none;
		transition: all 0.2s;
	}

	.home-btn {
		background: #3b82f6;
		color: white;
	}

	.home-btn:hover {
		background: #2563eb;
	}

	.search-btn {
		background: white;
		color: #3b82f6;
		border: 2px solid #3b82f6;
	}

	.search-btn:hover {
		background: #eff6ff;
	}
</style>
