<script lang="ts">
	import { untrack } from 'svelte';
	import { SvelteURLSearchParams } from 'svelte/reactivity';
	import { resolve } from '$app/paths';
	import { api } from '$lib/api';
	import FeedCard from '$lib/components/FeedCard.svelte';
	import InfiniteScrollSentinel from '$lib/components/InfiniteScrollSentinel.svelte';
	import type { FeedItemDto } from '$lib/types/reviewTypes';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	type Tab = 'personal' | 'global';

	let activeTab = $state<Tab>(untrack(() => data.initialTab as Tab));
	let items = $state<FeedItemDto[]>(untrack(() => data.feedItems));
	let totalCount = $state(untrack(() => data.totalCount));
	let page = $state(1);
	let loading = $state(false);

	// Personal feed: hide reviews shorter than 100 chars (low-signal). Persisted via the
	// `feedHideShort` cookie so SSR can already filter the first page (no client re-fetch race).
	let hideShortPersonal = $state(untrack(() => data.hideShortPref ?? false));

	function persistHideShort(v: boolean) {
		if (typeof document !== 'undefined') {
			// 1 year, root path so the server-side load function picks it up on every route.
			document.cookie = `feedHideShort=${v ? '1' : '0'}; path=/; max-age=${60 * 60 * 24 * 365}; SameSite=Lax`;
		}
	}

	const hasMore = $derived(items.length < totalCount);
	const isPersonal = $derived(activeTab === 'personal');

	async function fetchPage(tab: Tab, p: number) {
		if (tab === 'personal') {
			return api.getPersonalFeed(p, 10, data.token ?? undefined, !hideShortPersonal);
		}
		return api.getGlobalFeed(p, 10, data.token ?? undefined);
	}

	async function switchTab(tab: Tab) {
		if (tab === activeTab || loading) return;
		activeTab = tab;
		page = 1;
		// Reflect the active tab in the URL bar without a full reload.
		if (typeof window !== 'undefined') {
			const params = new SvelteURLSearchParams(window.location.search);
			params.set('tab', tab);
			history.replaceState(history.state, '', `${resolve('/')}?${params.toString()}`);
		}
		loading = true;
		try {
			const result = await fetchPage(tab, 1);
			items = result.items;
			totalCount = result.totalCount;
		} catch {
			items = [];
			totalCount = 0;
		} finally {
			loading = false;
		}
	}

	async function loadMore() {
		if (loading || !hasMore) return;
		loading = true;
		const nextPage = page + 1;
		try {
			const result = await fetchPage(activeTab, nextPage);
			items = [...items, ...result.items];
			page = nextPage;
		} catch {
			// keep current items
		} finally {
			loading = false;
		}
	}

	async function togglePersonalShortFilter() {
		hideShortPersonal = !hideShortPersonal;
		persistHideShort(hideShortPersonal);
		if (activeTab !== 'personal' || loading) return;
		page = 1;
		loading = true;
		try {
			const result = await fetchPage('personal', 1);
			items = result.items;
			totalCount = result.totalCount;
		} catch {
			items = [];
			totalCount = 0;
		} finally {
			loading = false;
		}
	}
</script>

<svelte:head>
	<title>Головна · TrackList</title>
</svelte:head>

<div class="max-w-2xl mx-auto">
	<!-- Tab switcher -->
	<div class="flex gap-1 mb-6 bg-bkg-header rounded-lg p-1 border border-gray-700/50">
		{#if data.token}
			<button
				onclick={() => switchTab('personal')}
				class="flex-1 py-2 rounded-md text-sm font-semibold transition-all
				       {isPersonal
					? 'bg-brand-accent text-white shadow-sm'
					: 'text-text-muted hover:text-white/90'}"
			>
				Моя стрічка
			</button>
		{/if}
		<button
			onclick={() => switchTab('global')}
			class="flex-1 py-2 rounded-md text-sm font-semibold transition-all
			       {!isPersonal
				? 'bg-brand-accent text-white shadow-sm'
				: 'text-text-muted hover:text-white/90'}"
		>
			Глобальна стрічка
		</button>
	</div>

	<!-- Personal feed: short-review filter toggle -->
	{#if isPersonal && data.token}
		<label class="flex items-center gap-2 mb-4 text-xs text-text-muted cursor-pointer select-none">
			<input
				type="checkbox"
				checked={hideShortPersonal}
				onchange={togglePersonalShortFilter}
				class="w-4 h-4 accent-brand-accent"
			/>
			<span>Приховувати короткі рецензії (&lt; 100 символів)</span>
		</label>
	{/if}

	<!-- Feed list -->
	{#if loading && items.length === 0}
		<div class="flex justify-center py-16">
			<span
				class="w-6 h-6 border-2 border-brand-accent border-t-transparent rounded-full animate-spin"
			></span>
		</div>
	{:else if items.length === 0}
		<div
			class="py-16 text-center text-text-muted border border-dashed border-gray-700 rounded-lg"
		>
			{#if isPersonal}
				<p class="text-base mb-1">Ваша стрічка порожня.</p>
				<p class="text-sm opacity-70">
					Підпишіться на когось або перегляньте
					<button
						onclick={() => switchTab('global')}
						class="text-brand-accent hover:underline">Глобальну стрічку</button
					>.
				</p>
			{:else}
				<p class="text-base">Рецензій ще немає</p>
			{/if}
		</div>
	{:else}
		<div class="flex flex-col gap-4">
			{#each items as item (item.reviewId)}
				<FeedCard {item} token={data.token} />
			{/each}
		</div>

		<InfiniteScrollSentinel {hasMore} {loading} onLoadMore={loadMore} />
	{/if}
</div>
