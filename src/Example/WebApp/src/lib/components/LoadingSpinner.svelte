<script lang="ts">
    export let size: 'small' | 'medium' | 'large' = 'medium';
    export let color: string = '#3b82f6';

    const sizeMap = {
        small: '24px',
        medium: '48px',
        large: '64px'
    };
</script>

<div class="spinner-container">
    <div
        class="spinner"
        style="width: {sizeMap[size]}; height: {sizeMap[size]}; border-color: {color} transparent transparent transparent;"
    ></div>
</div>

<style>
    .spinner-container {
        display: flex;
        justify-content: center;
        align-items: center;
        padding: 2rem;
    }

    .spinner {
        border: 4px solid;
        border-radius: 50%;
        animation: spin 1s linear infinite;
    }

    @keyframes spin {
        0% { transform: rotate(0deg); }
        100% { transform: rotate(360deg); }
    }
</style>
