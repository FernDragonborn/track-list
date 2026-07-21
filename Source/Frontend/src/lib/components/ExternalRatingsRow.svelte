<script lang="ts">
	import type { ExternalContent } from '$lib/types/externalTypes';
	import ExternalRatingBadge from './ExternalRatingBadge.svelte';
	import Shimmer from './Shimmer.svelte';

	interface Props {
		external: ExternalContent | null;
		ourRating: number | null;
		ourCount: number;
	}
	let { external, ourRating, ourCount }: Props = $props();

	const isLoading = $derived(external === null || external.status === 'loading');
</script>

<div class="flex flex-wrap items-center gap-2">
	<!-- Our rating -->
	<span class="inline-flex items-center gap-1.5 px-2 py-1 rounded-md border border-brand-accent/40 bg-brand-accent/10 text-brand-accent text-xs font-semibold">
		<span class="font-extrabold">★ Наш</span>
		<span>{ourRating !== null ? ourRating.toFixed(1) : '—'}</span>
		<span class="text-text-muted">· {ourCount}</span>
	</span>

	{#if isLoading}
		<Shimmer cls="h-6 w-16" />
		<Shimmer cls="h-6 w-16" />
		<Shimmer cls="h-6 w-16" />
	{:else if external}
		{#each external.ratings as r (r.source)}
			<ExternalRatingBadge rating={r} />
		{/each}
		{#if external.ratings.length === 0 && external.status === 'ready'}
			<span class="text-xs text-text-muted italic">Зовнішніх рейтингів немає</span>
		{/if}
	{/if}
</div>
