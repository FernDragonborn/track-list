<script lang="ts">
	import { onEnterView } from '$lib/utils/onEnterView';

	interface Props {
		hasMore: boolean;
		loading: boolean;
		onLoadMore: () => void;
		label?: string;
		loadingLabel?: string;
	}
	let {
		hasMore,
		loading,
		onLoadMore,
		label = 'Завантажити ще',
		loadingLabel = 'Завантаження…',
	}: Props = $props();

	function triggerIfIdle() {
		if (!loading) onLoadMore();
	}
</script>

{#if hasMore}
	<div use:onEnterView={triggerIfIdle} class="h-px w-full"></div>
	<button
		type="button"
		class="mt-4 w-full rounded-lg border border-gray-700/50 bg-bkg-header py-2 text-sm text-white hover:border-brand-accent/40 transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
		onclick={onLoadMore}
		disabled={loading}
	>
		{#if loading}
			<span
				class="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin"
			></span>
			{loadingLabel}
		{:else}
			{label}
		{/if}
	</button>
{/if}
