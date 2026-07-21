<script lang="ts">
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import ExternalReviewCard from '$lib/components/ExternalReviewCard.svelte';
	import InfiniteScrollSentinel from '$lib/components/InfiniteScrollSentinel.svelte';
	import EmptyState from '$lib/components/EmptyState.svelte';
	import type { ExternalReview, ExternalReviewFeedItem } from '$lib/types/externalTypes';

	interface Props {
		data: { initialItems: ExternalReviewFeedItem[]; initialCursor: string | null };
	}
	let { data }: Props = $props();

	let items = $state<ExternalReviewFeedItem[]>(untrack(() => data.initialItems));
	let cursor = $state<string | null>(untrack(() => data.initialCursor));
	let loading = $state(false);

	async function loadMore() {
		if (loading || !cursor) return;
		loading = true;
		try {
			const page = await api.getExternalFeed(cursor, 20);
			items = [...items, ...page.items];
			cursor = page.nextCursor ?? null;
		} finally {
			loading = false;
		}
	}

	function toReview(it: ExternalReviewFeedItem): ExternalReview {
		return {
			id: it.id,
			source: it.source,
			authorHandle: it.authorHandle,
			authorUrl: it.authorUrl,
			content: it.content,
			rating: it.rating,
			likeCountOnSource: null,
			sourceUrl: it.sourceUrl,
			publishedAt: it.publishedAt,
			fetchedAt: it.fetchedAt,
			reviewer: it.reviewer,
		};
	}
</script>

<svelte:head>
	<title>Стрічка критиків · TrackList</title>
</svelte:head>

<div class="max-w-3xl mx-auto px-4 py-8 space-y-4">
	<header class="space-y-1">
		<h1 class="text-2xl font-bold text-white">Стрічка критиків</h1>
		<p class="text-text-muted text-sm">
			Хронологічна стрічка рецензій від зовнішніх критиків (Letterboxd, …). Це <strong>не</strong>
			акаунти у TrackList — лише агрегація публічного контенту.
		</p>
	</header>

	{#if items.length === 0}
		<EmptyState message="Поки немає рецензій." dashed={false} />
	{:else}
		<ul class="space-y-3">
			{#each items as r (r.id)}
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
			hasMore={!!cursor}
			{loading}
			onLoadMore={loadMore}
			label="Показати ще"
		/>
	{/if}
</div>
