<script lang="ts">
	import { removeToast, toasts } from '$lib/stores/toastStore';

	const typeStyles = {
		success: { icon: '✓', bg: '#10b981', border: '#059669' },
		error: { icon: '✕', bg: '#ef4444', border: '#dc2626' },
		warning: { icon: '⚠', bg: '#f59e0b', border: '#d97706' },
		info: { icon: 'ℹ', bg: '#3b82f6', border: '#2563eb' }
	};
</script>

<div class="toast-container">
	{#each $toasts as toast (toast.id)}
		<div
			class="toast"
			style="background: {typeStyles[toast.type].bg}; border-color: {typeStyles[toast.type].border}"
		>
			<span class="toast-icon">{typeStyles[toast.type].icon}</span>
			<span class="toast-message">{toast.message}</span>
			<button class="toast-close" on:click={() => removeToast(toast.id)}>
				×
			</button>
		</div>
	{/each}
</div>

<style>
	.toast-container {
		position: fixed;
		top: 1rem;
		right: 1rem;
		z-index: 9999;
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
		pointer-events: none;
	}

	.toast {
		pointer-events: auto;
		background: white;
		color: white;
		padding: 1rem 1.5rem;
		border-radius: 8px;
		border: 2px solid;
		box-shadow: 0 10px 25px rgba(0, 0, 0, 0.2);
		display: flex;
		align-items: center;
		gap: 0.75rem;
		min-width: 300px;
		max-width: 500px;
		animation: slideIn 0.3s ease-out;
	}

	.toast-icon {
		font-size: 1.25rem;
		font-weight: bold;
		flex-shrink: 0;
	}

	.toast-message {
		flex: 1;
		font-size: 0.95rem;
	}

	.toast-close {
		background: none;
		border: none;
		color: white;
		font-size: 1.5rem;
		cursor: pointer;
		padding: 0;
		width: 24px;
		height: 24px;
		display: flex;
		align-items: center;
		justify-content: center;
		opacity: 0.8;
		transition: opacity 0.2s;
		flex-shrink: 0;
	}

	.toast-close:hover {
		opacity: 1;
	}

	@keyframes slideIn {
		from {
			transform: translateX(400px);
			opacity: 0;
		}
		to {
			transform: translateX(0);
			opacity: 1;
		}
	}
</style>
