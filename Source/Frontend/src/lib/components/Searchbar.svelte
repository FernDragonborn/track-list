<script lang="ts">
	import { goto } from '$app/navigation';
	import { api } from '$lib/api';
	import { getMediaTitle, getMediaUrl } from '$lib/utils/media';
	import type { SearchResult, MediaEntity } from '$lib/types/searchTypes';

	let searchQuery = $state('');
	let searchResults: SearchResult[] = $state([]);
	let isSearching = $state(false);
	let showDropdown = $state(false);
	let selectedIndex = $state(-1);
	let debounceTimer: ReturnType<typeof setTimeout> | undefined;
	$effect(() => {
		return () => { if (debounceTimer) clearTimeout(debounceTimer); };
	});

	// Refs
	let inputElement = $state<HTMLInputElement>();
	let dropdownElement = $state<HTMLDivElement>();

	function transformToSearchResult(media: MediaEntity): SearchResult {
		return {
			id: media.id,
			title: getMediaTitle(media.translations),
			year: media.releaseYear,
			posterUrl: media.posterUrl,
			url: getMediaUrl(media.id, media.externalApiId),
		};
	}

	// Debounced search function
	function debouncedSearch(query: string) {
		if (debounceTimer) {
			clearTimeout(debounceTimer);
		}

		if (!query.trim()) {
			searchResults = [];
			showDropdown = false;
			return;
		}

		debounceTimer = setTimeout(async () => {
			await performSearch(query);
		}, 500);
	}

	async function performSearch(query: string) {
		isSearching = true;
		showDropdown = true;
		try {
			const response = await api.get<MediaEntity[]>(
				`media/search?query=${encodeURIComponent(query)}`,
			);
			searchResults = response.map(transformToSearchResult);
			selectedIndex = -1;
		} catch (err: unknown) {
			const status = (err as { status?: number })?.status;
			if (status !== 404) {
				console.error('Search error:', err);
			}
			searchResults = [];
			selectedIndex = -1;
		} finally {
			isSearching = false;
		}
	}

	function handleInput(event: Event) {
		const target = event.target as HTMLInputElement;
		searchQuery = target.value;
		debouncedSearch(searchQuery);
	}

	function handleKeydown(event: KeyboardEvent) {
		if (!showDropdown || searchResults.length === 0) return;

		switch (event.key) {
			case 'ArrowDown':
				event.preventDefault();
				selectedIndex = Math.min(selectedIndex + 1, searchResults.length - 1);
				break;
			case 'ArrowUp':
				event.preventDefault();
				selectedIndex = Math.max(selectedIndex - 1, -1);
				break;
			case 'Enter':
				event.preventDefault();
				if (selectedIndex >= 0) {
					navigateToResult(searchResults[selectedIndex]);
				}
				break;
			case 'Escape':
				showDropdown = false;
				selectedIndex = -1;
				inputElement?.blur();
				break;
		}
	}

	function handleResultClick(result: SearchResult) {
		navigateToResult(result);
	}

	function navigateToResult(result: SearchResult) {
		showDropdown = false;
		searchQuery = '';
		searchResults = [];
		selectedIndex = -1;
		// eslint-disable-next-line svelte/no-navigation-without-resolve -- url already resolved via getMediaUrl()
		goto(result.url);
	}

	function handleFocus() {
		if (searchResults.length > 0) {
			showDropdown = true;
		}
	}

	function handleBlur() {
		// Delay hiding dropdown to allow for clicks
		setTimeout(() => {
			showDropdown = false;
			selectedIndex = -1;
		}, 150);
	}
</script>

<div class="relative hidden md:block group/search">
	<div class="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none">
		<svg
			class="w-4 h-4 text-text-muted group-focus-within/search:text-brand-accent transition-colors"
			aria-hidden="true"
			xmlns="http://www.w3.org/2000/svg"
			fill="none"
			viewBox="0 0 20 20"
		>
			<path
				stroke="currentColor"
				stroke-linecap="round"
				stroke-linejoin="round"
				stroke-width="2"
				d="m19 19-4-4m0-7A7 7 0 1 1 1 8a7 7 0 0 1 14 0Z"
			/>
		</svg>
	</div>
	<input
		bind:this={inputElement}
		type="search"
		placeholder="Пошук..."
		value={searchQuery}
		oninput={handleInput}
		onkeydown={handleKeydown}
		onfocus={handleFocus}
		onblur={handleBlur}
		class="bg-bkg-main text-sm text-white/95 pl-10 pr-4 py-2 rounded-full w-64 border border-transparent focus:border-brand-accent focus:outline-none focus:ring-1 focus:ring-brand-accent transition-all placeholder-text-muted"
	/>

	{#if showDropdown}
		<div
			bind:this={dropdownElement}
			class="absolute top-full left-0 right-0 mt-2 bg-bkg-header border border-gray-700 rounded-lg shadow-xl max-h-96 overflow-y-auto z-50"
		>
			{#if isSearching}
				<div class="px-4 py-3 text-text-muted text-sm">
					<div class="flex items-center gap-2">
						<div
							class="w-4 h-4 border-2 border-brand-accent border-t-transparent rounded-full animate-spin"
						></div>
						Шукаю...
					</div>
				</div>
			{:else if searchResults.length === 0}
				<div class="px-4 py-3 text-text-muted text-sm">Нічого не знайдено</div>
			{:else}
				{#each searchResults as result, index (result.id)}
					<button
						class="w-full text-left px-4 py-3 hover:bg-white/10 transition-colors flex items-center gap-3 {selectedIndex ===
						index
							? 'bg-white/10'
							: ''}"
						onclick={() => handleResultClick(result)}
					>
						{#if result.posterUrl}
							<img
								src={result.posterUrl}
								alt={result.title}
								class="w-10 h-14 object-cover rounded"
							/>
						{:else}
							<div
								class="w-10 h-14 bg-gray-600 rounded flex items-center justify-center"
							>
								<svg
									class="w-6 h-6 text-gray-400"
									fill="currentColor"
									viewBox="0 0 20 20"
								>
									<path
										fill-rule="evenodd"
										d="M4 3a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2H4zm12 12H4l4-8 3 6 2-4 3 6z"
										clip-rule="evenodd"
									/>
								</svg>
							</div>
						{/if}
						<div class="flex-1 min-w-0">
							<div class="text-white/95 font-medium truncate">
								{result.title}
							</div>
							{#if result.year}
								<div class="text-text-muted text-sm">
									{result.year}
								</div>
							{/if}
						</div>
					</button>
				{/each}
			{/if}
		</div>
	{/if}
</div>
