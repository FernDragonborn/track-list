<script lang="ts">
	import type { ExternalRating } from '$lib/types/externalTypes';

	interface Props {
		rating: ExternalRating;
		size?: 'sm' | 'md';
	}
	let { rating, size = 'md' }: Props = $props();

	const meta = $derived.by(() => {
		switch (rating.source) {
			case 'imdb':
				return { label: 'IMDb', bg: 'bg-yellow-500/15', border: 'border-yellow-400/30', text: 'text-yellow-200', icon: 'https://cdn.simpleicons.org/imdb/F5C518' };
			case 'rotten_tomatoes':
				return { label: 'RT', bg: 'bg-red-500/15', border: 'border-red-400/30', text: 'text-red-200', icon: 'https://cdn.simpleicons.org/rottentomatoes/FA320A' };
			case 'metacritic':
				return { label: 'MC', bg: 'bg-emerald-500/15', border: 'border-emerald-400/30', text: 'text-emerald-200', icon: 'https://cdn.simpleicons.org/metacritic/FFCC33' };
			default:
				return { label: rating.source, bg: 'bg-gray-500/15', border: 'border-gray-400/30', text: 'text-gray-200', icon: null as string | null };
		}
	});

	const iconCls = $derived(size === 'sm' ? 'w-3 h-3' : 'w-3.5 h-3.5');
	const sizeCls = $derived(size === 'sm' ? 'text-[10px] px-1.5 py-0.5 gap-1' : 'text-xs px-2 py-1 gap-1.5');
	const display = $derived(rating.rawScore ?? rating.score.toFixed(1));
</script>

<span class="inline-flex items-center rounded-md border font-semibold {meta.bg} {meta.border} {meta.text} {sizeCls}" title={`${meta.label}: ${display}${rating.voteCount ? ` (${rating.voteCount.toLocaleString()} votes)` : ''} · External`}>
	{#if meta.icon}
		<img src={meta.icon} alt={meta.label} class={iconCls} loading="lazy" />
	{:else}
		<span class="font-extrabold tracking-tight">{meta.label}</span>
	{/if}
	<span>{display}</span>
</span>
