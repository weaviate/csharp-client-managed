<script lang="ts">
	import ToastContainer from '$lib/components/ToastContainer.svelte';
	import { cartStore } from '$lib/stores/cartStore';
	import { wishlistStore } from '$lib/stores/wishlistStore';
	import '../app.css';

	export let title = 'AI Product Search';

	$: cartCount = cartStore.getCount($cartStore);
	$: wishlistCount = $wishlistStore.length;
</script>

<svelte:head>
	<title>{title}</title>
</svelte:head>

<ToastContainer />

<div class="app">
	<header>
		<nav>
			<a href="/" class="logo">
				<h1>🔍 AI Product Search</h1>
			</a>
			<div class="nav-links">
				<a href="/">Home</a>
				<a href="/search">Search</a>
				<a href="/wishlist" class="nav-icon">
					♡ Wishlist
					{#if wishlistCount > 0}
						<span class="badge">{wishlistCount}</span>
					{/if}
				</a>
				<a href="/cart" class="nav-icon">
					🛒 Cart
					{#if cartCount > 0}
						<span class="badge">{cartCount}</span>
					{/if}
				</a>
			</div>
		</nav>
	</header>

	<main>
		<slot />
	</main>

	<footer>
		<p>Powered by Weaviate C# Client • Vector Search Demo</p>
	</footer>
</div>

<style>
	:global(body) {
		margin: 0;
		font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen-Sans, Ubuntu,
			Cantarell, 'Helvetica Neue', sans-serif;
		background: #f5f5f5;
	}

	.app {
		display: flex;
		flex-direction: column;
		min-height: 100vh;
	}

	header {
		background: white;
		border-bottom: 1px solid #e0e0e0;
		box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
	}

	nav {
		max-width: 1200px;
		margin: 0 auto;
		padding: 1rem 2rem;
		display: flex;
		justify-content: space-between;
		align-items: center;
	}

	.logo {
		text-decoration: none;
		color: inherit;
	}

	.logo h1 {
		margin: 0;
		font-size: 1.5rem;
		color: #333;
	}

	.nav-links {
		display: flex;
		gap: 2rem;
		align-items: center;
	}

	.nav-links a {
		text-decoration: none;
		color: #666;
		font-weight: 500;
		transition: color 0.2s;
	}

	.nav-links a:hover {
		color: #007bff;
	}

	.nav-icon {
		position: relative;
		display: inline-flex;
		align-items: center;
		gap: 0.25rem;
	}

	.badge {
		position: absolute;
		top: -8px;
		right: -12px;
		background: #ef4444;
		color: white;
		font-size: 0.75rem;
		font-weight: 600;
		padding: 0.125rem 0.375rem;
		border-radius: 10px;
		min-width: 20px;
		text-align: center;
	}

	main {
		flex: 1;
		max-width: 1200px;
		width: 100%;
		margin: 0 auto;
		padding: 2rem;
	}

	footer {
		background: white;
		border-top: 1px solid #e0e0e0;
		padding: 2rem;
		text-align: center;
		color: #666;
	}

	footer p {
		margin: 0;
	}
</style>
