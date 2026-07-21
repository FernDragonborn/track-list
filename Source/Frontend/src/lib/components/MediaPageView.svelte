<script lang="ts">
	import { untrack } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { api } from '$lib/api';
	import type { MediaDetails, ReviewItem } from '$lib/types/searchTypes';
	import ReviewCard from './ReviewCard.svelte';
	import ReviewForm from './ReviewForm.svelte';
	import StarRating from './StarRating.svelte';
	import StatusButton from './StatusButton.svelte';
	import SearchableSelect from './SearchableSelect.svelte';
	import AddToCollectionModal from './AddToCollectionModal.svelte';
	import ReportModal from './ReportModal.svelte';
	import ExternalRatingsRow from './ExternalRatingsRow.svelte';
	import WikiReceptionCard from './WikiReceptionCard.svelte';
	import ExternalReviewCard from './ExternalReviewCard.svelte';
	import Shimmer from './Shimmer.svelte';
	import { lastCrumbLabel } from '$lib/stores/breadcrumb';
	import { selectedLang } from '$lib/stores/language';
	import type { ExternalContent } from '$lib/types/externalTypes';
	import type { TrackingStats } from '$lib/types/trackingTypes';
	import { DEFAULT_COLLECTION_NAME } from '$lib/constants';
	import type { CollectionResponseDto } from '$lib/types/collectionTypes';

	interface Props {
		media: MediaDetails;
		reviews: ReviewItem[];
		trackingStats?: TrackingStats;
		inCollections?: CollectionResponseDto[];
		token: string | null;
		username: string | null;
		userRole: string | null;
		currentUserId?: string | null;
	}

	let {
		media,
		reviews: initialReviews = [],
		trackingStats: initialTrackingStats = { planned: 0, watching: 0, completed: 0, dropped: 0 },
		inCollections: initialInCollections = [],
		token = null,
		username = null,
		userRole = null,
		currentUserId = null,
	}: Props = $props();

	let inCollections = $state<CollectionResponseDto[]>(untrack(() => initialInCollections));
	const visibleCollections = $derived(inCollections.filter((c) => c.name !== DEFAULT_COLLECTION_NAME));
	let showCollectionModal = $state(false);
	let showAllCollections = $state(false);
	let showSuggestForm = $state(false);
	let showReportModal = $state(false);
	let suggestLang = $state<'uk' | 'en'>('uk');
	let suggestTitle = $state('');
	let suggestDesc = $state('');
	let suggestBusy = $state(false);
	let suggestError = $state<string | null>(null);
	let suggestDone = $state(false);

	let reviews = $state<ReviewItem[]>(untrack(() => initialReviews));
	let reviewSort = $state<'newest' | 'oldest' | 'highest' | 'lowest'>('newest');
	let reviewSource = $state<'all' | 'ours' | 'external'>('all');
	let showWriteForm = $state(false);
	let sidebarLoading = $state(false);
	let trackingStats = $state(untrack(() => ({ ...initialTrackingStats })));

	const highlightReviewId = $derived(page.url.searchParams.get('review'));
	const highlightCommentId = $derived(page.url.searchParams.get('comment'));

	$effect(() => {
		// Only re-run when navigating to a different media page
		const _id = media.id;
		untrack(() => {
			reviews = initialReviews;
			inCollections = initialInCollections;
			showWriteForm = false;
			showCollectionModal = false;
			showAllCollections = false;
			showSuggestForm = false;
			suggestDone = false;
			suggestError = null;
			sidebarLoading = false;
			trackingStats = { ...initialTrackingStats };
			external = null;
		});
	});

	// External content: fetched async, polled until ready
	let external = $state<ExternalContent | null>(null);
	$effect(() => {
		const id = media.id;
		let cancelled = false;
		let pollTimer: ReturnType<typeof setTimeout> | undefined;

		async function load() {
			try {
				const data = await api.getExternalContent(id);
				if (cancelled) return;
				external = data;
				if (data.status === 'loading') {
					pollTimer = setTimeout(load, 2000);
				}
			} catch {
				if (cancelled) return;
				external = { status: 'error', ratings: [], wikiReception: null, reviews: [], lastFetchedAt: null, nextFetchDueAt: null, lastError: null };
			}
		}
		load();
		return () => {
			cancelled = true;
			if (pollTimer) clearTimeout(pollTimer);
		};
	});

	const externalLoading = $derived(external === null || external.status === 'loading');
	const externalReviews = $derived(external?.reviews ?? []);
	// Prioritize: ours (already shown via sortedReviews) > external with likes > rest.
	const sortedExternalReviews = $derived.by(() => {
		const arr = [...externalReviews];
		arr.sort((a, b) => (b.likeCountOnSource ?? 0) - (a.likeCountOnSource ?? 0)
			|| (new Date(b.publishedAt ?? 0).getTime() - new Date(a.publishedAt ?? 0).getTime()));
		return arr;
	});

	import type { TrackingStatusCode } from '$lib/types/trackingTypes';

	function handleStatusChange(next: TrackingStatusCode | null, prev: TrackingStatusCode | null) {
		if (prev) trackingStats[prev] = Math.max(0, trackingStats[prev] - 1);
		if (next) trackingStats[next] = trackingStats[next] + 1;
	}

	const isSeries = $derived(media.type === 'series');
	const typeLabel = $derived(isSeries ? 'Серіал' : 'Фільм');

	const ukT = $derived(media.translations.find((t) => t.languageCode === 'uk'));
	const enT = $derived(media.translations.find((t) => t.languageCode === 'en'));
	const firstT = $derived(media.translations.find((t) => t.title));

	const title = $derived.by(() => {
		const native = media.translations.find((t) => t.languageCode === $selectedLang)?.title;
		if (native) return native;
		return ukT?.title ?? enT?.title ?? firstT?.title ?? 'Без назви';
	});

	// Inject real media title into the global breadcrumb (last crumb).
	$effect(() => {
		lastCrumbLabel.set(title);
		return () => lastCrumbLabel.set(null);
	});
	const originalTitle = $derived(
		ukT?.title && enT?.title && ukT.title !== enT.title ? enT.title : null,
	);
	let manualDescTranslations = $state<Record<string, string>>({});
	let translatingDescription = $state(false);
	let descShowOriginal = $state(false);

	const cachedDescTranslation = $derived(
		external?.descriptionTranslations?.[$selectedLang] ?? manualDescTranslations[$selectedLang] ?? null
	);
	const hasDescTranslation = $derived(!!cachedDescTranslation);
	const descriptionInfo = $derived.by(() => {
		const lang = $selectedLang;
		// Prefer native TMDB translation if available for the selected language.
		const native = media.translations.find((t) => t.languageCode === lang)?.description;
		if (native) return { text: native, translated: false };
		// Cached DeepL translation, unless user toggled "show original".
		if (cachedDescTranslation && lang !== 'en' && !descShowOriginal)
			return { text: cachedDescTranslation, translated: true };
		// Else show original (UA → EN → first available).
		const ua = media.translations.find((t) => t.languageCode === 'uk')?.description;
		const en = media.translations.find((t) => t.languageCode === 'en')?.description;
		return { text: ua ?? en ?? '', translated: false };
	});
	const description = $derived(descriptionInfo.text);
	const canToggleDescription = $derived($selectedLang !== 'en' && !!description);
	const descButtonLabel = $derived.by(() => {
		if (translatingDescription) return 'Перекладаю…';
		if (!hasDescTranslation) return `Перекласти на ${$selectedLang.toUpperCase()}`;
		return descShowOriginal ? `Показати переклад (${$selectedLang.toUpperCase()})` : 'Показати оригінал';
	});

	async function toggleDescription() {
		if (!canToggleDescription) return;
		if (hasDescTranslation) {
			descShowOriginal = !descShowOriginal;
			return;
		}
		if (translatingDescription) return;
		translatingDescription = true;
		try {
			const r = await api.translateDescription(media.id, $selectedLang);
			manualDescTranslations = { ...manualDescTranslations, [r.lang]: r.translation };
			descShowOriginal = false;
		} catch {
			// noop
		} finally {
			translatingDescription = false;
		}
	}

	const avgRating = $derived(
		reviews.length > 0 ? reviews.reduce((s, r) => s + r.rating, 0) / reviews.length : null,
	);

	const myReview = $derived(
		username !== null ? reviews.find((r) => r.username === username) ?? null : null,
	);

	const hasText = (r: ReviewItem) => !!r.content && r.content.trim() !== '';
	const sortedReviews = $derived.by(() => {
		const arr = [...reviews];
		const secondary = (a: ReviewItem, b: ReviewItem) => {
			if (reviewSort === 'newest') return new Date(b.createdAt ?? 0).getTime() - new Date(a.createdAt ?? 0).getTime();
			if (reviewSort === 'oldest') return new Date(a.createdAt ?? 0).getTime() - new Date(b.createdAt ?? 0).getTime();
			if (reviewSort === 'highest') return b.rating - a.rating;
			return a.rating - b.rating;
		};
		// Priority: 1) from-following first (if logged-in), 2) reviews with text, 3) user-selected order.
		return arr.sort((a, b) => {
			const fa = a.isFromFollowing ? 1 : 0;
			const fb = b.isFromFollowing ? 1 : 0;
			if (fa !== fb) return fb - fa;
			const ta = hasText(a) ? 1 : 0;
			const tb = hasText(b) ? 1 : 0;
			if (ta !== tb) return tb - ta;
			return secondary(a, b);
		});
	});

	function pluralReviews(n: number) {
		if (n === 1) return 'рецензія';
		if (n >= 2 && n <= 4) return 'рецензії';
		return 'рецензій';
	}

	async function handleSidebarRate(v: number) {
		if (!token) { goto(resolve('/auth/login')); return; }
		if (sidebarLoading) return;
		sidebarLoading = true;
		try {
			if (myReview) {
				await api.updateReview(media.id, myReview.id, { rating: v, content: myReview.content }, token);
				reviews = reviews.map((r) => (r.id === myReview!.id ? { ...r, rating: v } : r));
			} else {
				const created = await api.createReview(media.id, { rating: v }, token);
				reviews = [created, ...reviews];
			}
		} catch {
			// silent — user can retry
		} finally {
			sidebarLoading = false;
		}
	}

	function handleReviewSaved(review: ReviewItem) {
		const idx = reviews.findIndex((r) => r.id === review.id);
		if (idx >= 0) {
			reviews = reviews.map((r) => (r.id === review.id ? review : r));
		} else {
			reviews = [review, ...reviews];
		}
		showWriteForm = false;
	}

	function handleReviewDeleted(reviewId: string) {
		reviews = reviews.filter((r) => r.id !== reviewId);
	}

	function handleReviewUpdated(updated: ReviewItem) {
		reviews = reviews.map((r) => (r.id === updated.id ? updated : r));
	}

	async function submitSuggestion() {
		if (!token || !suggestTitle.trim()) return;
		suggestBusy = true;
		suggestError = null;
		try {
			await api.suggestTranslation(media.id, {
				languageCode: suggestLang,
				title: suggestTitle.trim(),
				description: suggestDesc.trim() || undefined,
			}, token);
			suggestDone = true;
			suggestTitle = '';
			suggestDesc = '';
		} catch (e: unknown) {
			suggestError = e instanceof Error ? e.message : 'Помилка при відправці';
		} finally {
			suggestBusy = false;
		}
	}

	function handleModalKeydown(e: KeyboardEvent) {
		if (e.key !== 'Escape') return;
		if (showSuggestForm) { showSuggestForm = false; suggestDone = false; suggestError = null; return; }
		if (showAllCollections) { showAllCollections = false; return; }
	}
</script>

<svelte:window onkeydown={handleModalKeydown} />

{#if showCollectionModal && token && currentUserId}
	<AddToCollectionModal
		bind:open={showCollectionModal}
		mediaId={media.id}
		{token}
		{currentUserId}
		onClose={() => (showCollectionModal = false)}
		onAdd={(col) => { if (!inCollections.some((c) => c.id === col.id)) inCollections = [...inCollections, col]; }}
		onRemove={(id) => { inCollections = inCollections.filter((c) => c.id !== id); }}
	/>
{/if}

{#if showAllCollections}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
		role="dialog"
		aria-modal="true"
		tabindex={-1}
		onclick={(e) => { if (e.target === e.currentTarget) showAllCollections = false; }}
		onkeydown={(e) => { if (e.key === 'Escape') showAllCollections = false; }}
	>
		<div
			class="w-full max-w-sm bg-bkg-header border border-gray-700 rounded-2xl shadow-2xl overflow-hidden"
		>
			<div class="flex items-center justify-between px-5 py-4 border-b border-gray-800">
				<h2 class="text-base font-bold text-white/95">В добірках ({visibleCollections.length})</h2>
				<button onclick={() => (showAllCollections = false)} class="text-xl text-gray-400 hover:text-white/95 transition-colors">×</button>
			</div>
			<div class="max-h-96 overflow-y-auto">
				{#each visibleCollections as c (c.id)}
					<a
						href={resolve(`/collections/${c.id}`)}
						onclick={() => (showAllCollections = false)}
						class="flex items-center gap-3 px-5 py-3 hover:bg-white/10 transition-colors border-b border-gray-800 last:border-0 group"
					>
						<div class="w-7 h-10 rounded bg-gray-800 shrink-0 flex items-center justify-center">
							<svg class="w-3.5 h-3.5 text-gray-500" fill="currentColor" viewBox="0 0 20 20">
								<path d="M7 3a1 1 0 000 2h6a1 1 0 100-2H7zM4 7a1 1 0 011-1h10a1 1 0 110 2H5a1 1 0 01-1-1zM2 11a2 2 0 012-2h12a2 2 0 012 2v4a2 2 0 01-2 2H4a2 2 0 01-2-2v-4z"/>
							</svg>
						</div>
						<div class="min-w-0 flex-1">
							<p class="text-sm font-semibold text-white/90 truncate group-hover:text-brand-accent transition-colors">{c.name}</p>
							<p class="text-[10px] text-text-muted">{c.ownerUsername} · {c.itemCount} медіа</p>
						</div>
					</a>
				{/each}
			</div>
		</div>
	</div>
{/if}

{#if showWriteForm && token}
	<ReviewForm
		mediaId={media.id}
		{token}
		existing={myReview}
		onSave={handleReviewSaved}
		onClose={() => (showWriteForm = false)}
	/>
{/if}

{#if showSuggestForm && token}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
		role="dialog"
		aria-modal="true"
		tabindex={-1}
		onclick={(e) => {
			if (e.target === e.currentTarget) { showSuggestForm = false; suggestDone = false; suggestError = null; }
		}}
		onkeydown={(e) => {
			if (e.key === 'Escape') { showSuggestForm = false; suggestDone = false; suggestError = null; }
		}}
	>
		<div
			class="w-full max-w-md bg-bkg-header border border-gray-700 rounded-2xl shadow-2xl overflow-hidden"
		>
			<div class="flex items-center justify-between px-5 py-4 border-b border-gray-800">
				<h2 class="text-base font-bold text-white/95">Запропонувати переклад</h2>
				<button
					onclick={() => { showSuggestForm = false; suggestDone = false; suggestError = null; }}
					class="text-xl text-gray-400 hover:text-white/95 transition-colors"
				>×</button>
			</div>

			<div class="px-5 py-4 flex flex-col gap-4">
				{#if suggestDone}
					<p class="text-green-400 text-sm font-semibold text-center py-4">
						Переклад надіслано на розгляд модератору ✓
					</p>
					<button
						onclick={() => { showSuggestForm = false; suggestDone = false; }}
						class="w-full py-2 rounded-lg bg-gray-700/60 text-text-muted text-sm font-semibold hover:bg-gray-700 transition-colors"
					>Закрити</button>
				{:else}
					<!-- Language -->
					<fieldset class="flex flex-col gap-1.5 border-0 p-0 m-0">
						<legend class="text-xs font-semibold text-text-muted uppercase tracking-wide mb-0">Мова</legend>
						<div class="flex gap-2">
							{#each (['uk', 'en'] as const) as lang (lang)}
								<button
									onclick={() => (suggestLang = lang)}
									class="flex-1 py-1.5 text-sm font-bold rounded-lg border transition-colors
									       {suggestLang === lang
									           ? 'border-brand-accent bg-brand-accent/15 text-brand-accent'
									           : 'border-gray-700 bg-transparent text-text-muted hover:border-gray-600'}"
								>{lang === 'uk' ? 'Українська' : 'English'}</button>
							{/each}
						</div>
					</fieldset>

				<!-- Title -->
					<div class="flex flex-col gap-1.5">
						<label class="text-xs font-semibold text-text-muted uppercase tracking-wide" for="suggest-title">Назва *</label>
						<input
							id="suggest-title"
							type="text"
							bind:value={suggestTitle}
							placeholder="Назва медіа"
							maxlength="200"
							class="w-full bg-black/30 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white/90
							       placeholder:text-text-muted focus:outline-none focus:border-brand-accent transition-colors"
						/>
					</div>

					<!-- Description -->
					<div class="flex flex-col gap-1.5">
						<label class="text-xs font-semibold text-text-muted uppercase tracking-wide" for="suggest-desc">Опис</label>
						<textarea
							id="suggest-desc"
							bind:value={suggestDesc}
							placeholder="Короткий опис (необов'язково)"
							rows="4"
							maxlength="2000"
							class="w-full bg-black/30 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white/90
							       placeholder:text-text-muted focus:outline-none focus:border-brand-accent transition-colors resize-none"
						></textarea>
					</div>

					{#if suggestError}
						<p class="text-red-400 text-xs">{suggestError}</p>
					{/if}

					<div class="flex gap-2 pt-1">
						<button
							onclick={submitSuggestion}
							disabled={suggestBusy || !suggestTitle.trim()}
							class="flex-1 py-2 rounded-lg bg-brand-accent hover:bg-brand-hover text-white/95 text-sm font-bold
							       transition-all disabled:opacity-40"
						>{suggestBusy ? '...' : 'Надіслати'}</button>
						<button
							onclick={() => { showSuggestForm = false; suggestError = null; }}
							class="px-4 py-2 rounded-lg bg-gray-700/60 text-text-muted text-sm font-semibold hover:bg-gray-700 transition-colors"
						>Скасувати</button>
					</div>
				{/if}
			</div>
		</div>
	</div>
{/if}

<div>
	<!-- Three-column layout -->
	<div class="flex gap-6 items-start">
		<!-- ── Left sidebar: poster + user actions ── -->
		<aside class="w-56 flex-shrink-0 hidden md:flex flex-col gap-3">
			<!-- Poster -->
			<div
				class="aspect-[2/3] rounded-xl overflow-hidden bg-bkg-header shadow-2xl ring-1 ring-white/5"
			>
				{#if media.posterUrl}
					<img
						src={media.posterUrl}
						alt={title}
						class="w-full h-full object-cover"
						loading="lazy"
					/>
				{:else}
					<div class="w-full h-full flex items-center justify-center">
						<svg class="w-12 h-12 text-gray-600" fill="currentColor" viewBox="0 0 20 20">
							<path
								fill-rule="evenodd"
								d="M4 3a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2H4zm12 12H4l4-8 3 6 2-4 3 6z"
								clip-rule="evenodd"
							/>
						</svg>
					</div>
				{/if}
			</div>

			<!-- User star rating -->
			<div class="flex items-center w-full gap-1">
				<span class="text-xs text-text-muted shrink-0">0</span>
				<StarRating
					value={myReview?.rating ?? 0}
					interactive={!sidebarLoading}
					size="lg"
					onchange={handleSidebarRate}
					class="flex-1 justify-center gap-2"
				/>
				<span class="text-xs text-text-muted shrink-0">10</span>
			</div>

			<!-- Tracking status -->
			<StatusButton
				mediaId={media.id}
				{token}
				mediaType={media.type}
				episodeCount={media.episodeCount ?? undefined}
				seasonCount={media.seasonCount ?? undefined}
				onStatusChange={handleStatusChange}
			/>

			<!-- Action buttons -->
			{#if token && currentUserId}
				<button
					onclick={() => (showCollectionModal = true)}
					class="w-full py-2 rounded-lg bg-brand-accent hover:bg-brand-hover text-white/95 text-xs font-bold transition-all active:scale-95 shadow-lg shadow-brand-accent/20"
				>
					+ Додати до добірки
				</button>
			{/if}

			<!-- My review button -->
			{#if token}
				{#if myReview === null}
					<button
						onclick={() => (showWriteForm = true)}
						class="w-full py-2 rounded-lg bg-bkg-header hover:bg-white/10 border border-brand-accent/50
						       text-brand-accent text-xs font-bold transition-all"
					>
						Моя рецензія
					</button>
				{:else}
					<button
						onclick={() => (showWriteForm = true)}
						class="w-full py-2 rounded-lg bg-bkg-header hover:bg-white/10 border border-brand-accent/50
						       text-brand-accent text-xs font-bold transition-all"
					>
						Редагувати рецензію
					</button>
				{/if}
			{:else}
				<a
					href={resolve('/auth/login')}
					class="w-full py-2 rounded-lg bg-bkg-header hover:bg-white/10 border border-gray-700
					       text-white/95 text-xs font-bold transition-all text-center block"
				>
					Моя рецензія
				</a>
			{/if}

			<!-- Suggest translation -->
			{#if token}
				<button
					onclick={() => { showSuggestForm = true; suggestDone = false; suggestError = null; }}
					class="w-full py-2 rounded-lg bg-bkg-header hover:bg-white/10 border border-gray-700
					       text-text-muted text-xs font-semibold transition-all"
				>
					Запропонувати переклад
				</button>
			{/if}

			<!-- Report media -->
			{#if token && currentUserId}
				<button
					onclick={() => (showReportModal = true)}
					title="Поскаржитись на медіа"
					class="w-full py-2 rounded-lg bg-bkg-header hover:bg-yellow-500/10 border border-gray-700 hover:border-yellow-500/50
					       text-text-muted hover:text-yellow-400 text-xs font-semibold transition-all"
				>
					⚑ Поскаржитись
				</button>
			{/if}
		</aside>

		{#if showReportModal && token && currentUserId}
			<ReportModal
				bind:open={showReportModal}
				targetId={media.id}
				targetType="Media"
				{token}
				userId={currentUserId}
				onClose={() => (showReportModal = false)}
			/>
		{/if}

		<!-- ── Middle: info + reviews ── -->
		<main class="flex-1 min-w-0">
			<!-- Type badge + year -->
			<div class="flex flex-wrap items-center gap-2 mb-3">
				<span
					class="text-xs font-bold uppercase tracking-wider px-2.5 py-0.5 rounded-full bg-brand-accent/15 text-brand-accent border border-brand-accent/30"
				>
					{typeLabel}
				</span>
				{#if media.releaseYear}
					<span class="text-text-muted text-sm">{media.releaseYear}</span>
				{/if}
				{#if isSeries}
					<span class="text-gray-600 text-sm">·</span>
					<span class="text-text-muted text-sm">Сезони: {media.seasonCount ?? '—'}</span>
					<span class="text-text-muted text-sm">Серії: {media.episodeCount ?? '—'}</span>
				{/if}
			</div>

			<!-- Title -->
			<h1 class="text-4xl font-black text-white/95 leading-tight mb-1 break-words">
				{title}
			</h1>

			<!-- Original title -->
			{#if originalTitle}
				<p class="text-text-muted text-lg mb-4">{originalTitle}</p>
			{:else}
				<div class="mb-4"></div>
			{/if}

			<!-- Ratings: our + external -->
			<section class="mb-6">
				<ExternalRatingsRow {external} ourRating={avgRating} ourCount={reviews.length} />
			</section>

			<!-- Description -->
			{#if description}
				<section class="mb-6">
					<div class="flex items-center justify-between gap-3 mb-2 flex-wrap">
						<h2 class="text-xs font-semibold uppercase tracking-wider text-text-muted flex items-center gap-2">
							<span>Опис</span>
							{#if descriptionInfo.translated}
								<span class="text-[10px] normal-case tracking-normal italic text-text-muted/80">· Перекладено DeepL</span>
							{/if}
						</h2>
						{#if canToggleDescription}
							<button
								type="button"
								onclick={toggleDescription}
								disabled={translatingDescription}
								class="text-xs text-brand-accent hover:underline disabled:opacity-50 whitespace-nowrap"
							>{descButtonLabel}</button>
						{/if}
					</div>
					<p class="text-white/80 leading-relaxed">{description}</p>
				</section>
			{/if}

			<!-- Critical reception (Wikipedia, collapsible, spoiler-protected) -->
			<WikiReceptionCard reception={external?.wikiReception ?? null} />

			<hr class="border-gray-700/60 mb-6" />

			<!-- Reviews -->
			<section>
				<div class="flex items-center justify-between mb-4 flex-wrap gap-3">
					<h2 class="text-lg font-bold text-white/95">Рецензії</h2>
					<div class="flex items-center gap-3 flex-wrap">
						<!-- Source filter chips: ours / external / all -->
						<div class="flex gap-1 bg-bkg-header rounded-md p-0.5 border border-gray-700/50">
							{#each [
								{ v: 'all', l: 'Усі' },
								{ v: 'ours', l: 'Наші' },
								{ v: 'external', l: 'Зовнішні' },
							] as opt (opt.v)}
								<button
									onclick={() => (reviewSource = opt.v as typeof reviewSource)}
									class="px-2.5 py-1 rounded text-xs font-semibold transition-all
									       {reviewSource === opt.v
										? 'bg-brand-accent text-white shadow-sm'
										: 'text-text-muted hover:text-white/90'}"
								>{opt.l}</button>
							{/each}
						</div>
						{#if reviews.length > 0}
							<span class="text-text-muted text-sm">
								{reviews.length}
								{pluralReviews(reviews.length)}
							</span>
							<SearchableSelect
								bind:value={reviewSort}
								placeholder=""
								options={[
									{ value: 'newest', label: 'Останні' },
									{ value: 'oldest', label: 'Найстаріші' },
									{ value: 'highest', label: 'Найвища оцінка' },
									{ value: 'lowest', label: 'Найнижча оцінка' },
								]}
							/>
						{/if}
						{#if myReview === null}
							<button
								onclick={token ? () => (showWriteForm = true) : () => goto(resolve('/auth/login'))}
								class="px-3 py-1 rounded-lg bg-brand-accent hover:bg-brand-hover text-white text-xs
								       font-semibold transition-all active:scale-95 shadow-sm shadow-brand-accent/20"
							>
								+ Написати
							</button>
						{/if}
					</div>
				</div>

				{#if reviewSource !== 'external'}
					{#if reviews.length === 0}
						<div
							class="py-10 text-center text-text-muted border border-dashed border-gray-700 rounded-xl"
						>
							<p class="text-base">Рецензій ще немає</p>
							<button
								onclick={token ? () => (showWriteForm = true) : () => goto(resolve('/auth/login'))}
								class="mt-3 px-4 py-1.5 rounded-lg bg-brand-accent hover:bg-brand-hover text-white
								       text-sm font-semibold transition-all active:scale-95"
							>
								Написати першу рецензію
							</button>
						</div>
					{:else}
						<div class="flex flex-col gap-4">
							{#each sortedReviews as review (review.id)}
								<ReviewCard
									mediaId={media.id}
									{review}
									{token}
									{username}
									{userRole}
									userId={currentUserId}
									onDelete={handleReviewDeleted}
									onUpdate={handleReviewUpdated}
									autoOpenComments={!!highlightCommentId && review.id === highlightReviewId}
									highlightCommentId={review.id === highlightReviewId ? (highlightCommentId ?? undefined) : undefined}
								/>
							{/each}
						</div>
					{/if}
				{/if}

				<!-- External reviews -->
				{#if reviewSource !== 'ours'}
					{#if externalLoading}
						<div class="mt-6 flex flex-col gap-3">
							<div class="text-xs uppercase tracking-wider text-text-muted">Зовнішні рецензії</div>
							<Shimmer cls="h-24 w-full" />
							<Shimmer cls="h-24 w-full" />
						</div>
					{:else if sortedExternalReviews.length > 0}
						<div class="mt-6 flex flex-col gap-3">
							<div class="flex items-center gap-2">
								<span class="text-xs uppercase tracking-wider text-text-muted">Зовнішні рецензії</span>
								<span class="text-[10px] text-text-muted px-1.5 py-0.5 border border-gray-600 rounded-md">{sortedExternalReviews.length}</span>
							</div>
							{#each sortedExternalReviews as ext (ext.id)}
								<ExternalReviewCard review={ext} />
							{/each}
						</div>
					{:else if reviewSource === 'external'}
						<div class="mt-6 py-10 text-center text-text-muted border border-dashed border-gray-700 rounded-xl">
							<p class="text-base">Зовнішніх рецензій ще немає</p>
						</div>
					{/if}
				{/if}
			</section>
		</main>

		<!-- ── Right sidebar: stats ── -->
		<aside class="w-52 flex-shrink-0 hidden lg:flex flex-col gap-4">
			<!-- Average rating -->
			<div class="bg-bkg-header rounded-xl p-4 border border-gray-700/50 flex flex-col items-center text-center">
				<h3 class="text-[10px] font-semibold uppercase tracking-wider text-text-muted mb-3">
					Середній рейтинг
				</h3>
				<div class="flex items-baseline gap-1 mb-1">
					<span class="text-5xl font-black text-white/95 leading-none">
						{avgRating !== null ? avgRating.toFixed(1) : '—'}
					</span>
					{#if avgRating !== null}
						<span class="text-lg font-semibold text-text-muted">/10</span>
					{/if}
				</div>
				<div class="mb-2">
					<StarRating value={avgRating ?? 0} size="sm" />
				</div>
				<p class="text-text-muted text-xs">
					{reviews.length}
					{pluralReviews(reviews.length)}
				</p>
			</div>

			<!-- Tracking stats -->
			<div class="bg-bkg-header rounded-xl p-4 border border-gray-700/50">
				<h3 class="text-[10px] font-semibold uppercase tracking-wider text-text-muted mb-3 text-center">
					Статистика
				</h3>
				<div class="flex flex-col gap-2">
					{#each [
						['Переглянули', trackingStats.completed],
						['Дивляться',   trackingStats.watching],
						['Заплановано', trackingStats.planned],
						['Кинули',      trackingStats.dropped],
					] as [label, count] (label)}
						<div class="flex justify-between items-center text-sm">
							<span class="text-text-muted">{label}</span>
							<span class="text-white/80 font-medium tabular-nums">{count}</span>
						</div>
					{/each}
				</div>
			</div>

			<!-- Collections containing this media -->
			{#if visibleCollections.length > 0}
				<div class="bg-bkg-header rounded-xl p-4 border border-gray-700/50">
					<h3 class="text-[10px] font-semibold uppercase tracking-wider text-text-muted mb-3 text-center">
						В добірках
					</h3>
					<div class="flex flex-col gap-2">
						{#each visibleCollections.slice(0, 5) as c (c.id)}
							<a
								href={resolve(`/collections/${c.id}`)}
								class="flex items-center gap-2 hover:opacity-75 transition-opacity group"
							>
								<div class="w-7 h-10 rounded bg-gray-800 shrink-0 flex items-center justify-center">
									<svg class="w-3.5 h-3.5 text-gray-500" fill="currentColor" viewBox="0 0 20 20">
										<path d="M7 3a1 1 0 000 2h6a1 1 0 100-2H7zM4 7a1 1 0 011-1h10a1 1 0 110 2H5a1 1 0 01-1-1zM2 11a2 2 0 012-2h12a2 2 0 012 2v4a2 2 0 01-2 2H4a2 2 0 01-2-2v-4z"/>
									</svg>
								</div>
								<div class="min-w-0">
									<p class="text-xs text-white/80 font-semibold truncate group-hover:text-brand-accent transition-colors">{c.name}</p>
									<p class="text-[10px] text-text-muted">{c.ownerUsername}</p>
								</div>
							</a>
						{/each}
					</div>
					{#if visibleCollections.length > 5}
						<button
							onclick={() => (showAllCollections = true)}
							class="mt-3 w-full text-[10px] text-brand-accent hover:text-brand-hover transition-colors text-center"
						>
							Показати всі ({visibleCollections.length})
						</button>
					{/if}
				</div>
			{/if}
		</aside>
	</div>
</div>
