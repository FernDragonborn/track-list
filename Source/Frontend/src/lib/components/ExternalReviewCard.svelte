<script lang="ts">
	/* eslint-disable svelte/no-navigation-without-resolve -- dynamic external URLs cannot use resolve() */
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import { formatDate } from '$lib/utils/format';
	import { truncate } from '$lib/utils/text';
	import { api } from '$lib/api';
	import { selectedLang } from '$lib/stores/language';
	import type { ExternalReview } from '$lib/types/externalTypes';
	import StarRating from './StarRating.svelte';
	import ReviewCardShell from './ReviewCardShell.svelte';
	import SafeHtml from './SafeHtml.svelte';

	interface Props {
		review: ExternalReview;
		/** Optional media-page link for feed/profile contexts that lack their own anchor. */
		mediaLink?: { href: string; title?: string | null; year?: number | null; posterUrl?: string | null };
	}
	let { review, mediaLink }: Props = $props();

	let translationCache = $state<Record<string, string>>(untrack(() => ({ ...(review.translations ?? {}) })));
	let translating = $state(false);
	let showOriginal = $state(false);
	let expandedLong = $state(false);

	const LONG_THRESHOLD = 1000;
	const PREVIEW_LIMIT = 500;

	const hasTranslation = $derived(!!translationCache[$selectedLang]);
	const displayed = $derived.by(() => {
		const lang = $selectedLang;
		if (lang === 'en' || showOriginal) return { text: review.content, translated: false };
		const cached = translationCache[lang];
		if (cached) return { text: cached, translated: true };
		return { text: review.content, translated: false };
	});
	const isLong = $derived(displayed.text.length > LONG_THRESHOLD);
	const visibleContent = $derived(
		!isLong || expandedLong ? displayed.text : truncate(displayed.text, PREVIEW_LIMIT),
	);
	const canToggleTranslation = $derived(!!review.content && $selectedLang !== 'en');
	const translateButtonLabel = $derived.by(() => {
		if (translating) return 'Перекладаю…';
		if (!hasTranslation) return `Перекласти на ${$selectedLang.toUpperCase()}`;
		return showOriginal ? `Показати переклад (${$selectedLang.toUpperCase()})` : 'Показати оригінал';
	});

	async function toggleTranslation() {
		if (!canToggleTranslation) return;
		if (hasTranslation) {
			showOriginal = !showOriginal;
			return;
		}
		if (translating) return;
		translating = true;
		try {
			const res = await api.translateExternalReview(review.id, $selectedLang);
			translationCache = { ...translationCache, [res.lang]: res.translation };
			showOriginal = false;
		} catch {
			// silent
		} finally {
			translating = false;
		}
	}

	const meta = $derived.by(() => {
		switch (review.source) {
			case 'letterboxd':
				return { label: 'Letterboxd', pill: 'bg-orange-500/15 text-orange-300', icon: '◐' };
			case 'wikipedia_reception':
				return { label: 'Wikipedia', pill: 'bg-gray-700 text-gray-300', icon: 'W' };
			default:
				return { label: review.source, pill: 'bg-gray-700 text-gray-300', icon: '◇' };
		}
	});
</script>

<ReviewCardShell>
	{#snippet header()}
		<div class="flex items-start gap-3 min-w-0 w-full">
			{#if mediaLink?.posterUrl}
				<a href={resolve(mediaLink.href as '/')} class="shrink-0">
					<img
						src={mediaLink.posterUrl}
						alt={mediaLink.title ?? ''}
						class="w-16 h-24 object-cover rounded-md bg-bkg-main"
						loading="lazy"
					/>
				</a>
			{/if}
			<div class="flex items-center gap-2 min-w-0 flex-1">
				{#if review.reviewer?.avatarUrl}
					<img
						src={review.reviewer.avatarUrl}
						alt="Аватар {review.reviewer.displayName ?? review.reviewer.handle}"
						class="w-9 h-9 rounded-full object-cover bg-bkg-main shrink-0"
						loading="lazy"
					/>
				{:else}
					<span
						class="inline-flex items-center justify-center w-9 h-9 rounded-full bg-bkg-main text-text-muted font-bold shrink-0"
					>
						{meta.icon}
					</span>
				{/if}
				<div class="min-w-0">
					<div class="flex items-center gap-1.5 flex-wrap">
						{#if review.authorHandle && review.source === 'letterboxd'}
							<a href={resolve(`/external-reviewers/${review.authorHandle}`)} class="text-white/90 font-semibold text-sm hover:text-brand-accent transition-colors truncate" title="Профіль критика на TrackList">{review.reviewer?.displayName ?? review.authorHandle}</a>
						{:else if review.authorUrl}
							<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
							<a href={review.authorUrl} target="_blank" rel="noopener noreferrer" class="text-white/90 font-semibold text-sm hover:text-brand-accent transition-colors truncate">{review.authorHandle ?? '—'}</a>
						{:else}
							<span class="text-white/90 font-semibold text-sm truncate">
								{review.authorHandle ?? '—'}
							</span>
						{/if}
						<span
							class="text-[10px] uppercase tracking-wide px-1.5 py-0.5 rounded-md font-bold {meta.pill}"
						>
							{meta.label}
						</span>
						<span
							class="text-[9px] uppercase tracking-wide px-1.5 py-0.5 rounded-md font-bold border border-gray-600 text-gray-400"
						>
							external
						</span>
					</div>
					{#if mediaLink}
						<a href={resolve(mediaLink.href as '/')} class="block text-white text-sm font-medium hover:text-brand-accent transition-colors mt-0.5 truncate">{mediaLink.title ?? 'Без назви'}{#if mediaLink.year}
								<span class="text-text-muted font-normal"> ({mediaLink.year})</span>
							{/if}</a>
					{/if}
					{#if review.rating != null}
						<div class="mt-0.5 flex items-center gap-1.5">
							<StarRating value={review.rating} size="sm" />
							<span class="text-text-muted text-xs">{review.rating}/10</span>
						</div>
					{/if}
				</div>
			</div>
		</div>
		<div class="flex items-center gap-2 shrink-0">
			{#if canToggleTranslation}
				<button
					type="button"
					onclick={toggleTranslation}
					disabled={translating}
					class="text-xs text-brand-accent hover:underline disabled:opacity-50 whitespace-nowrap"
				>
					{translateButtonLabel}
				</button>
			{/if}
			{#if review.publishedAt}
				<span class="text-text-muted text-xs whitespace-nowrap">{formatDate(review.publishedAt)}</span>
			{/if}
			{#if review.sourceUrl}
				<a
					href={review.sourceUrl}
					target="_blank"
					rel="noopener noreferrer"
					class="text-xs text-text-muted hover:text-brand-accent transition-colors"
					title="Відкрити на джерелі"
				>
					↗
				</a>
			{/if}
		</div>
	{/snippet}
	{#snippet body()}
		{#if review.content}
			{#if displayed.translated}
				<p class="text-[10px] text-text-muted mb-2 italic">Перекладено DeepL</p>
			{/if}
			<SafeHtml
				content={visibleContent}
				class="tl-review-prose text-white/80 text-sm leading-relaxed prose prose-invert max-w-none whitespace-pre-line"
			/>
			{#if isLong}
				<button
					type="button"
					onclick={() => (expandedLong = !expandedLong)}
					class="mt-2 text-xs text-brand-accent hover:underline"
				>
					{expandedLong ? 'Згорнути' : 'Розгорнути повністю'}
				</button>
			{/if}
		{/if}
	{/snippet}
	{#snippet footer()}
		{#if review.likeCountOnSource}
			<span>♥ {review.likeCountOnSource} on {meta.label}</span>
		{:else}
			<span></span>
		{/if}
	{/snippet}
</ReviewCardShell>

<style>
	.tl-review-prose :global(p) {
		margin-bottom: 1em;
	}
	.tl-review-prose :global(p:last-child) {
		margin-bottom: 0;
	}
	.tl-review-prose :global(ul),
	.tl-review-prose :global(ol) {
		margin: 0.75em 0;
	}
</style>
