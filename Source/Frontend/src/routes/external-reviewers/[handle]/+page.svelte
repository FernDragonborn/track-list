<script lang="ts">
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import ExternalReviewCard from '$lib/components/ExternalReviewCard.svelte';
	import InfiniteScrollSentinel from '$lib/components/InfiniteScrollSentinel.svelte';
	import EmptyState from '$lib/components/EmptyState.svelte';
	import type {
		ExternalReview,
		ExternalReviewer,
		ExternalReviewerProfile,
		ExternalReviewWithMedia,
	} from '$lib/types/externalTypes';

	interface Props {
		data: { profile: ExternalReviewerProfile };
	}
	let { data }: Props = $props();

	let reviews = $state<ExternalReviewWithMedia[]>(untrack(() => data.profile.recentReviews));
	let cursor = $state<string | null | undefined>(undefined);
	let initialCursor = $state(true);
	let loadingMore = $state(false);
	let exhausted = $state(false);

	// Reset when navigating between handles (same /external-reviewers/[handle] route).
	$effect(() => {
		const _key = `${data.profile.source}:${data.profile.handle}`;
		untrack(() => {
			reviews = data.profile.recentReviews;
			cursor = undefined;
			initialCursor = true;
			loadingMore = false;
			exhausted = false;
		});
	});

	async function loadMore() {
		if (loadingMore || exhausted) return;
		loadingMore = true;
		try {
			const page = await api.getExternalReviewerReviews(
				data.profile.handle,
				data.profile.source,
				cursor ?? undefined,
				20,
			);
			if (initialCursor) {
				reviews = page.items;
				initialCursor = false;
			} else {
				reviews = [...reviews, ...page.items];
			}
			cursor = page.nextCursor;
			if (!cursor) exhausted = true;
		} finally {
			loadingMore = false;
		}
	}

	const displayName = $derived(data.profile.displayName ?? data.profile.handle);

	const reviewerSummary: ExternalReviewer = $derived({
		id: data.profile.id,
		source: data.profile.source,
		handle: data.profile.handle,
		displayName: data.profile.displayName,
		bio: data.profile.bio,
		avatarUrl: data.profile.avatarUrl,
		sourceProfileUrl: data.profile.sourceProfileUrl,
		lastSyncedAt: data.profile.lastSyncedAt,
	});

	function toReview(r: ExternalReviewWithMedia): ExternalReview {
		return {
			id: r.id,
			source: r.source,
			authorHandle: r.authorHandle,
			authorUrl: r.authorUrl,
			content: r.content,
			rating: r.rating,
			likeCountOnSource: null,
			sourceUrl: r.sourceUrl,
			publishedAt: r.publishedAt,
			fetchedAt: r.fetchedAt,
			reviewer: reviewerSummary,
		};
	}
</script>

<svelte:head>
	<title>{displayName} — зовнішній критик · TrackList</title>
</svelte:head>

<div class="max-w-3xl mx-auto px-4 py-8 space-y-6">
	<header class="rounded-2xl border border-gray-700/50 bg-bkg-header p-6">
		<div class="flex items-start gap-4">
			{#if data.profile.avatarUrl}
				<img
					src={data.profile.avatarUrl}
					alt="Аватар {displayName}"
					class="w-20 h-20 rounded-full object-cover bg-bkg-main"
					loading="lazy"
				/>
			{/if}
			<div class="flex-1 min-w-0">
				<h1 class="text-2xl font-bold text-white truncate">{displayName}</h1>
				<p class="text-text-muted text-sm">@{data.profile.handle}</p>
				<div
					class="mt-2 inline-flex items-center gap-2 px-2 py-1 rounded-md bg-brand-accent/15 border border-brand-accent/40 text-brand-accent text-xs font-semibold uppercase tracking-wide"
				>
					Зовнішній критик · {data.profile.source}
				</div>
				{#if data.profile.bio}
					<p class="mt-3 text-white/80 text-sm leading-snug">{data.profile.bio}</p>
				{/if}
				<div class="mt-3 flex items-center gap-4 text-sm">
					<span class="text-white">
						<strong>{data.profile.reviewCount}</strong>
						<span class="text-text-muted">рецензій у нас</span>
					</span>
					{#if data.profile.averageRating != null}
						<span class="text-white">
							Середня <strong>{data.profile.averageRating.toFixed(1)}</strong>/10
						</span>
					{/if}
				</div>
				{#if data.profile.sourceProfileUrl}
					<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -->
					<a href={data.profile.sourceProfileUrl} target="_blank" rel="noopener noreferrer" class="mt-3 inline-flex items-center gap-1.5 text-sm text-brand-accent hover:underline">↗ Відкрити на {data.profile.source}</a>
				{/if}
			</div>
		</div>
		<p class="mt-4 text-xs text-text-muted leading-snug border-t border-gray-700/50 pt-3">
			Цей профіль агрегує публічні рецензії з {data.profile.source}. Це <strong>не</strong>
			акаунт у TrackList — підписатись, написати або поскаржитись не можна.
		</p>
	</header>

	<section>
		<h2 class="text-xl font-semibold text-white mb-3">Рецензії</h2>
		{#if reviews.length === 0}
			<EmptyState message="Поки немає рецензій." dashed={false} />
		{:else}
			<ul class="space-y-3">
				{#each reviews as r (r.id)}
					<li>
						<ExternalReviewCard
							review={toReview(r)}
							mediaLink={{
								href: `/media/${r.mediaId}`,
								title: r.mediaTitle,
								year: r.mediaReleaseYear,
								posterUrl: r.mediaPosterUrl,
							}}
						/>
					</li>
				{/each}
			</ul>
			<InfiniteScrollSentinel
				hasMore={!exhausted}
				loading={loadingMore}
				onLoadMore={loadMore}
				label="Показати ще"
			/>
		{/if}
	</section>
</div>
