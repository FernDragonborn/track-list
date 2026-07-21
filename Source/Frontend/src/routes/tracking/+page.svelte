<script lang="ts">
	import TrackingTab from '$lib/components/TrackingTab.svelte';
	import type { TrackingStatusCode } from '$lib/types/trackingTypes';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	const TABS: { code: TrackingStatusCode | 'all'; label: string }[] = [
		{ code: 'all',       label: 'Усі' },
		{ code: 'watching',  label: 'Дивлюся' },
		{ code: 'planned',   label: 'У планах' },
		{ code: 'completed', label: 'Переглянуто' },
		{ code: 'dropped',   label: 'Кинуто' },
	];

	let activeTab = $state<TrackingStatusCode | 'all'>('all');

	const filtered = $derived(
		activeTab === 'all' ? data.items : data.items.filter((i) => i.status === activeTab),
	);

	function countFor(code: TrackingStatusCode | 'all') {
		return code === 'all' ? data.items.length : data.items.filter((i) => i.status === code).length;
	}
</script>

<svelte:head>
	<title>Мій трекінг · TrackList</title>
</svelte:head>

<div class="max-w-4xl mx-auto">
	<h1 class="text-xl font-bold text-white mb-6">Мій трекінг</h1>

	<!-- Status filter tabs -->
	<div class="flex gap-1 bg-bkg-header rounded-lg p-1 border border-gray-700/50 mb-6 flex-wrap">
		{#each TABS as tab (tab.code)}
			{@const count = countFor(tab.code)}
			<button
				onclick={() => (activeTab = tab.code)}
				class="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-semibold transition-all
				       {activeTab === tab.code
					       ? 'bg-brand-accent text-white shadow-sm'
					       : 'text-text-muted hover:text-white/90'}"
			>
				{tab.label}
				{#if count > 0}
					<span
						class="px-1.5 py-0.5 rounded-full text-[10px] font-bold
						       {activeTab === tab.code ? 'bg-white/20 text-white' : 'bg-gray-700 text-text-muted'}"
					>
						{count}
					</span>
				{/if}
			</button>
		{/each}
	</div>

	<TrackingTab items={filtered} isOwnProfile={true} token={data.token} />
</div>
