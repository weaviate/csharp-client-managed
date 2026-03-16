<script lang="ts">
	export let rating: number = 0;
	export let maxRating: number = 5;
	export let size: 'small' | 'medium' | 'large' = 'medium';
	export let interactive: boolean = false;
	export let onChange: ((rating: number) => void) | null = null;

	let hoverRating: number = 0;

	$: displayRating = interactive && hoverRating > 0 ? hoverRating : rating;
	$: fullStars = Math.floor(displayRating);
	$: hasHalfStar = displayRating % 1 >= 0.5;
	$: emptyStars = maxRating - fullStars - (hasHalfStar ? 1 : 0);

	function handleClick(starIndex: number) {
		if (interactive && onChange) {
			onChange(starIndex + 1);
		}
	}

	function handleMouseEnter(starIndex: number) {
		if (interactive) {
			hoverRating = starIndex + 1;
		}
	}

	function handleMouseLeave() {
		if (interactive) {
			hoverRating = 0;
		}
	}
</script>

<div
	class="star-rating"
	class:small={size === 'small'}
	class:medium={size === 'medium'}
	class:large={size === 'large'}
	class:interactive
	on:mouseleave={handleMouseLeave}
	role={interactive ? 'slider' : 'img'}
	aria-label={interactive ? `Rating: ${rating} out of ${maxRating}` : undefined}
	aria-valuemin={interactive ? 1 : undefined}
	aria-valuemax={interactive ? maxRating : undefined}
	aria-valuenow={interactive ? rating : undefined}
>
	{#each Array(fullStars) as _, i}
		<button
			class="star full"
			disabled={!interactive}
			on:click={() => handleClick(i)}
			on:mouseenter={() => handleMouseEnter(i)}
			aria-label={interactive ? `Rate ${i + 1} star${i > 0 ? 's' : ''}` : undefined}
		>
			★
		</button>
	{/each}

	{#if hasHalfStar}
		<button
			class="star half"
			disabled={!interactive}
			on:click={() => handleClick(fullStars)}
			on:mouseenter={() => handleMouseEnter(fullStars)}
			aria-label={interactive ? `Rate ${fullStars + 1} stars` : undefined}
		>
			<span class="half-star">★</span>
		</button>
	{/if}

	{#each Array(emptyStars) as _, i}
		<button
			class="star empty"
			disabled={!interactive}
			on:click={() => handleClick(fullStars + (hasHalfStar ? 1 : 0) + i)}
			on:mouseenter={() => handleMouseEnter(fullStars + (hasHalfStar ? 1 : 0) + i)}
			aria-label={interactive ? `Rate ${fullStars + (hasHalfStar ? 1 : 0) + i + 1} stars` : undefined}
		>
			☆
		</button>
	{/each}

	{#if !interactive}
		<span class="rating-text">({rating.toFixed(1)})</span>
	{/if}
</div>

<style>
	.star-rating {
		display: inline-flex;
		align-items: center;
		gap: 0.125rem;
	}

	.star {
		background: none;
		border: none;
		padding: 0;
		cursor: default;
		line-height: 1;
		transition: all 0.2s;
	}

	.interactive .star {
		cursor: pointer;
	}

	.interactive .star:hover {
		transform: scale(1.1);
	}

	.star.full {
		color: #fbbf24;
	}

	.star.half {
		position: relative;
		color: #d1d5db;
	}

	.half-star {
		position: absolute;
		left: 0;
		top: 0;
		color: #fbbf24;
		overflow: hidden;
		width: 50%;
	}

	.star.empty {
		color: #d1d5db;
	}

	.interactive .star:hover,
	.interactive .star:focus {
		color: #fbbf24;
	}

	.small .star {
		font-size: 0.875rem;
	}

	.medium .star {
		font-size: 1.125rem;
	}

	.large .star {
		font-size: 1.5rem;
	}

	.rating-text {
		margin-left: 0.25rem;
		font-size: 0.875rem;
		color: #6b7280;
	}

	.small .rating-text {
		font-size: 0.75rem;
	}

	.large .rating-text {
		font-size: 1rem;
	}
</style>
