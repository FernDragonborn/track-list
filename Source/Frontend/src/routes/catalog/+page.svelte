<script lang="ts">
	import { resolve } from '$app/paths';
	import { tick, untrack } from 'svelte';
	import { SvelteURLSearchParams } from 'svelte/reactivity';
	import { api } from '$lib/api';
	import { getMediaTitle } from '$lib/utils/media';
	import type { MediaEntity } from '$lib/types/searchTypes';
	import type { GenreOption } from '$lib/types/genreTypes';
	import type { PageData } from './$types';
	import MultiSelectDropdown from '$lib/components/MultiSelectDropdown.svelte';
	import ExternalRatingBadge from '$lib/components/ExternalRatingBadge.svelte';
	import type { MediaRatingsBatchEntry } from '$lib/types/externalTypes';

	let { data }: { data: PageData } = $props();

	type MediaFilter = '' | 'movie' | 'series';
	type SortOption =
		| 'added'
		| 'year_desc'
		| 'year_asc'
		| 'title_asc'
		| 'title_desc'
		| 'rating_desc'
		| 'rating_asc';

	let items = $state<MediaEntity[]>(untrack(() => data.items));
	let totalCount = $state(untrack(() => data.totalCount));
	let activeType = $state<MediaFilter>(untrack(() => (data.activeType ?? '') as MediaFilter));
	let yearFrom = $state<string>(untrack(() => (data.activeYearFrom ? String(data.activeYearFrom) : '')));
	let yearTo = $state<string>(untrack(() => (data.activeYearTo ? String(data.activeYearTo) : '')));
	let activeSort = $state<SortOption>(untrack(() => (data.activeSort ?? 'added') as SortOption));
	let pageNum = $state(1);
	let loading = $state(false);
	let genreOptions = $state<GenreOption[]>([]);
	let selectedGenres = $state<number[]>(untrack(() => data.activeGenres ?? []));
	let ratingsBatch = $state<Record<string, MediaRatingsBatchEntry>>({});

	async function loadRatingsForVisible(ids: string[]) {
		const missing = ids.filter((id) => !(id in ratingsBatch));
		if (missing.length === 0) return;
		try {
			const result = await api.getMediaRatingsBatch(missing);
			ratingsBatch = { ...ratingsBatch, ...result };
		} catch {
			// silent — catalog still works
		}
	}

	$effect(() => {
		// reactively pull ratings whenever items change
		const ids = items.map((i) => i.id);
		if (ids.length > 0) loadRatingsForVisible(ids);
	});

	const supportsGenres = $derived(activeType === 'movie' || activeType === 'series');

	const hasMore = $derived(items.length < totalCount);

	const typeChips: { value: MediaFilter; label: string; disabled?: boolean; tooltip?: string }[] = [
		{ value: '', label: 'Всі' },
		{ value: 'movie', label: 'Фільми' },
		{ value: 'series', label: 'Серіали' },
	];
	const wipChips: { label: string }[] = [
		{ label: 'Книги (WIP)' },
		{ label: 'Ігри (WIP)' },
		{ label: 'Інше (WIP)' },
	];

	const sortOptions: { value: SortOption; label: string }[] = [
		{ value: 'added', label: 'Додано (нові)' },
		{ value: 'rating_desc', label: 'Рейтинг (високий)' },
		{ value: 'rating_asc', label: 'Рейтинг (низький)' },
		{ value: 'year_desc', label: 'Рік (нові)' },
		{ value: 'year_asc', label: 'Рік (старі)' },
		{ value: 'title_asc', label: 'Назва (А-Я)' },
		{ value: 'title_desc', label: 'Назва (Я-А)' },
	];

	const currentYear = new Date().getFullYear();
	const years: number[] = [];
	for (let y = currentYear; y >= 1950; y--) years.push(y);

	function getTitle(media: MediaEntity): string {
		return getMediaTitle(media.translations);
	}

	function getMediaHref(media: MediaEntity): string {
		return media.externalApiId ? `/media/external/${media.externalApiId}` : `/media/${media.id}`;
	}

	function buildQuery(): string {
		const params = new SvelteURLSearchParams();
		if (activeType) params.set('type', activeType);
		if (yearFrom) params.set('yearFrom', yearFrom);
		if (yearTo) params.set('yearTo', yearTo);
		if (activeSort !== 'added') params.set('sortBy', activeSort);
		if (selectedGenres.length > 0) params.set('genres', selectedGenres.join(','));
		return params.toString() ? `?${params.toString()}` : '';
	}

	async function fetchGenres(type: string) {
		try {
			genreOptions = await api.getGenres(type);
		} catch {
			genreOptions = [];
		}
	}

	// Reload genre options when type changes
	$effect(() => {
		if (supportsGenres) {
			fetchGenres(activeType);
		} else {
			genreOptions = [];
		}
	});

	// Re-apply filters when genres change
	let lastGenresKey = $state('');
	$effect(() => {
		const key = selectedGenres.slice().sort().join(',');
		if (key !== lastGenresKey) {
			lastGenresKey = key;
			applyFilters();
		}
	});

	async function applyFilters() {
		if (loading) return;
		pageNum = 1;
		loading = true;
		const query = buildQuery();
		if (typeof window !== 'undefined') {
			history.replaceState(history.state, '', `${resolve('/catalog')}${query}`);
		}
		try {
			const result = await api.getCatalog(
				1,
				20,
				activeType || undefined,
				yearFrom ? parseInt(yearFrom, 10) : undefined,
				yearTo ? parseInt(yearTo, 10) : undefined,
				activeSort,
				selectedGenres.length > 0 ? selectedGenres : undefined,
			);
			items = result.items;
			totalCount = result.totalCount;
		} catch {
			items = [];
			totalCount = 0;
		} finally {
			loading = false;
		}
	}

	function switchType(type: MediaFilter) {
		if (type === activeType) return;
		activeType = type;
		selectedGenres = []; // reset genres on type change
		lastGenresKey = '';
		applyFilters();
	}

	function onYearFromChange(e: Event) {
		const newVal = (e.target as HTMLSelectElement).value;
		yearFrom = newVal;
		const fromN = newVal ? parseInt(newVal, 10) : null;
		const toN = yearTo ? parseInt(yearTo, 10) : null;
		if (fromN !== null && toN !== null && fromN > toN) {
			yearTo = newVal;
		}
		applyFilters();
	}

	function onYearToChange(e: Event) {
		const newVal = (e.target as HTMLSelectElement).value;
		yearTo = newVal;
		const toN = newVal ? parseInt(newVal, 10) : null;
		const fromN = yearFrom ? parseInt(yearFrom, 10) : null;
		if (fromN !== null && toN !== null && fromN > toN) {
			yearFrom = newVal;
		}
		applyFilters();
	}

	function changeSort(e: Event) {
		activeSort = (e.target as HTMLSelectElement).value as SortOption;
		applyFilters();
	}

	async function loadMore(autoFillDepth = 0) {
		if (loading || !hasMore) return;
		loading = true;
		const nextPage = pageNum + 1;
		let fresh: typeof items = [];
		try {
			const result = await api.getCatalog(
				nextPage,
				20,
				activeType || undefined,
				yearFrom ? parseInt(yearFrom, 10) : undefined,
				yearTo ? parseInt(yearTo, 10) : undefined,
				activeSort,
				selectedGenres.length > 0 ? selectedGenres : undefined,
			);
			const existing = new Set(items.map((i) => i.id));
			fresh = result.items.filter((i) => !existing.has(i.id));
			console.debug('[catalog] loadMore', {
				page: nextPage,
				returned: result.items.length,
				fresh: fresh.length,
				total: result.totalCount,
				items: items.length + fresh.length,
				depth: autoFillDepth,
			});
			if (fresh.length === 0) {
				// Force-stop: no new ids — either exhausted OR duplicates only
				totalCount = items.length;
			} else {
				items = [...items, ...fresh];
				pageNum = nextPage;
				totalCount = result.totalCount;
			}
		} catch (e) {
			console.error('[catalog] loadMore error', e);
		} finally {
			loading = false;
		}
		// Auto-fill (capped): handles short-page case
		if (fresh.length === 0 || autoFillDepth >= 3) return;
		await tick();
		if (
			hasMore &&
			document.documentElement.scrollHeight <= window.innerHeight + 100
		) {
			loadMore(autoFillDepth + 1);
		}
	}

	// Svelte action: IntersectionObserver wrapper
	import InfiniteScrollSentinel from '$lib/components/InfiniteScrollSentinel.svelte';
</script>

<svelte:head>
	<title>Каталог · TrackList</title>
</svelte:head>

<div class="max-w-6xl mx-auto">
	<h1 class="text-2xl font-bold mb-6 text-white/95">Каталог</h1>

	<!-- Type chips -->
	<div class="flex flex-wrap items-center gap-1 mb-4">
		<div class="flex gap-1 bg-bkg-header rounded-lg p-1 border border-gray-700/50 w-fit">
			{#each typeChips as chip (chip.value)}
				<button
					onclick={() => switchType(chip.value)}
					class="px-4 py-2 rounded-md text-sm font-semibold transition-all
						{activeType === chip.value
						? 'bg-brand-accent text-white shadow-sm'
						: 'text-text-muted hover:text-white/90'}"
				>
					{chip.label}
				</button>
			{/each}
		</div>
		<div class="flex gap-1 bg-bkg-header/40 rounded-lg p-1 border border-dashed border-gray-700/40 w-fit ml-2">
			{#each wipChips as chip (chip.label)}
				<span
					class="px-3 py-2 rounded-md text-xs font-semibold text-gray-500 cursor-not-allowed select-none"
					title="Work in progress"
				>
					{chip.label}
				</span>
			{/each}
		</div>
	</div>

	<!-- Year range + Sort filters -->
	<div class="flex flex-wrap items-center gap-3 mb-6">
		<div class="flex items-center gap-2">
			<span class="text-sm text-text-muted">Рік:</span>
			<label for="year-from" class="text-xs text-text-muted">від</label>
			<select
				id="year-from"
				value={yearFrom}
				onchange={onYearFromChange}
				class="bg-bkg-header text-white/95 text-sm px-3 py-1.5 rounded-md border border-gray-700/50 focus:border-brand-accent outline-none"
			>
				<option value="">—</option>
				{#each years as y (y)}
					<option value={String(y)}>{y}</option>
				{/each}
			</select>
			<label for="year-to" class="text-xs text-text-muted">до</label>
			<select
				id="year-to"
				value={yearTo}
				onchange={onYearToChange}
				class="bg-bkg-header text-white/95 text-sm px-3 py-1.5 rounded-md border border-gray-700/50 focus:border-brand-accent outline-none"
			>
				<option value="">—</option>
				{#each years as y (y)}
					<option value={String(y)}>{y}</option>
				{/each}
			</select>
		</div>

		<div class="flex items-center gap-2">
			<label for="sort-filter" class="text-sm text-text-muted">Сортування:</label>
			<select
				id="sort-filter"
				value={activeSort}
				onchange={changeSort}
				class="bg-bkg-header text-white/95 text-sm px-3 py-1.5 rounded-md border border-gray-700/50 focus:border-brand-accent outline-none"
			>
				{#each sortOptions as opt (opt.value)}
					<option value={opt.value}>{opt.label}</option>
				{/each}
			</select>
		</div>

		{#if supportsGenres}
			<div class="flex items-center gap-2">
				<span class="text-sm text-text-muted">Жанри:</span>
				<MultiSelectDropdown
					options={genreOptions.map((g) => ({ value: g.id, label: g.nameUk || g.name }))}
					bind:selected={selectedGenres}
					placeholder="Жанри"
				/>
			</div>
		{/if}
	</div>

	<!-- Media grid -->
	{#if loading && items.length === 0}
		<div class="flex justify-center py-16">
			<span
				class="w-6 h-6 border-2 border-brand-accent border-t-transparent rounded-full animate-spin"
			></span>
		</div>
	{:else if items.length === 0}
		<div class="py-16 text-center text-text-muted border border-dashed border-gray-700 rounded-lg">
			<p class="text-base">Медіа ще немає в каталозі</p>
		</div>
	{:else}
		<div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
			{#each items as media (media.id)}
				<a
					href={resolve(getMediaHref(media) as '/')}
					class="group bg-bkg-header rounded-lg overflow-hidden border border-gray-700/50 hover:border-brand-accent/50 transition-all"
				>
					{#if media.posterUrl}
						<img
							src={media.posterUrl}
							alt={getTitle(media)}
							class="w-full aspect-[2/3] object-cover group-hover:opacity-90 transition-opacity"
						/>
					{:else}
						<div class="w-full aspect-[2/3] bg-gray-700 flex items-center justify-center">
							<svg class="w-12 h-12 text-gray-500" fill="currentColor" viewBox="0 0 20 20">
								<path
									fill-rule="evenodd"
									d="M4 3a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2H4zm12 12H4l4-8 3 6 2-4 3 6z"
									clip-rule="evenodd"
								/>
							</svg>
						</div>
					{/if}
					<div class="p-2">
						<p class="text-sm font-medium text-white/95 truncate">{getTitle(media)}</p>
						{#if media.releaseYear}
							<p class="text-xs text-text-muted">{media.releaseYear}</p>
						{/if}
						{#if ratingsBatch[media.id]}
							<div class="mt-1.5 flex flex-wrap gap-1">
								{#if ratingsBatch[media.id].ourAvg !== null}
									<span class="inline-flex items-center gap-1 text-[10px] px-1.5 py-0.5 rounded-md border border-brand-accent/40 bg-brand-accent/10 text-brand-accent font-semibold" title={`Наш рейтинг — ${ratingsBatch[media.id].ourAvg!.toFixed(1)}/10 (${ratingsBatch[media.id].ourCount} рецензій)`}>
										<span class="font-extrabold tracking-tight">★ Наш</span>
										<span>{ratingsBatch[media.id].ourAvg!.toFixed(1)}</span>
									</span>
								{/if}
								{#each ratingsBatch[media.id].external.slice(0, 2) as r (r.source)}
									<ExternalRatingBadge rating={r} size="sm" />
								{/each}
							</div>
						{/if}
					</div>
				</a>
			{/each}
		</div>

		<InfiniteScrollSentinel
			{hasMore}
			{loading}
			onLoadMore={loadMore}
			label={`Завантажити ще (${totalCount - items.length})`}
		/>
	{/if}
</div>
