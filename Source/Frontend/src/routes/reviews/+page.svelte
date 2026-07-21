<script lang="ts">
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import FeedCard from '$lib/components/FeedCard.svelte';
	import ReviewForm from '$lib/components/ReviewForm.svelte';
	import InfiniteScrollSentinel from '$lib/components/InfiniteScrollSentinel.svelte';
	import type { FeedItemDto } from '$lib/types/reviewTypes';
	import type { ReviewItem } from '$lib/types/searchTypes';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();

	type SortKey = 'newest' | 'oldest' | 'rating_desc' | 'rating_asc';

	const SORT_LABELS: Record<SortKey, string> = {
		newest: 'Нові',
		oldest: 'Старі',
		rating_desc: 'Рейтинг ↓',
		rating_asc: 'Рейтинг ↑',
	};

	let items = $state<FeedItemDto[]>(untrack(() => data.feedItems));
	let totalCount = $state(untrack(() => data.totalCount));
	let page = $state(1);
	let loading = $state(false);
	let sort = $state<SortKey>('newest');
	let editingItem = $state<FeedItemDto | null>(null);

	const hasMore = $derived(items.length < totalCount);

	const editingAsReviewItem = $derived<ReviewItem | null>(
		editingItem
			? {
					id: editingItem.reviewId,
					mediaId: editingItem.mediaId,
					userId: editingItem.userId,
					username: editingItem.username,
					profilePicUrl: editingItem.profilePicUrl,
					rating: editingItem.rating,
					content: editingItem.content,
					createdAt: editingItem.createdAt,
					likeCount: editingItem.likeCount,
					commentCount: editingItem.commentCount,
					isLikedByMe: editingItem.isLikedByMe,
				}
			: null,
	);

	async function applySort(s: SortKey) {
		if (s === sort && !loading) return;
		sort = s;
		page = 1;
		loading = true;
		try {
			const result = await api.getMyReviews(1, 10, data.token ?? undefined, s);
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
			const result = await api.getMyReviews(nextPage, 10, data.token ?? undefined, sort);
			items = [...items, ...result.items];
			page = nextPage;
		} catch {
			// keep current items
		} finally {
			loading = false;
		}
	}

	function handleEdit(item: FeedItemDto) {
		editingItem = item;
	}

	async function handleDelete(reviewId: string) {
		const target = items.find((i) => i.reviewId === reviewId);
		if (!target || !data.token) return;
		items = items.filter((i) => i.reviewId !== reviewId);
		totalCount -= 1;
		try {
			await api.deleteReview(target.mediaId, reviewId, data.token);
		} catch {
			// restore on failure
			items = [...items, target].sort(
				(a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
			);
			totalCount += 1;
		}
	}

	function handleEditSave(updated: ReviewItem) {
		items = items.map((i) =>
			i.reviewId === updated.id
				? { ...i, rating: updated.rating, content: updated.content }
				: i,
		);
		editingItem = null;
	}
</script>

<svelte:head>
	<title>Мої рецензії · TrackList</title>
</svelte:head>

{#if editingItem && editingAsReviewItem && data.token}
	<ReviewForm
		mediaId={editingItem.mediaId}
		token={data.token}
		existing={editingAsReviewItem}
		onSave={handleEditSave}
		onClose={() => (editingItem = null)}
	/>
{/if}

<div class="max-w-2xl mx-auto">
	<div class="flex items-center justify-between gap-4 mb-6">
		<h1 class="text-xl font-bold text-white">Мої рецензії</h1>

		<!-- Sort buttons -->
		<div class="flex gap-1 bg-bkg-header rounded-lg p-1 border border-gray-700/50">
			{#each Object.entries(SORT_LABELS) as [key, label] (key)}
				<button
					onclick={() => applySort(key as SortKey)}
					class="px-3 py-1.5 rounded-md text-xs font-semibold transition-all
					       {sort === key
						? 'bg-brand-accent text-white shadow-sm'
						: 'text-text-muted hover:text-white/90'}"
				>
					{label}
				</button>
			{/each}
		</div>
	</div>

	{#if loading && items.length === 0}
		<div class="flex justify-center py-16">
			<span class="w-6 h-6 border-2 border-brand-accent border-t-transparent rounded-full animate-spin"></span>
		</div>
	{:else if items.length === 0}
		<div class="py-16 text-center text-text-muted border border-dashed border-gray-700 rounded-lg">
			<p class="text-base">Ви ще не написали жодної рецензії</p>
		</div>
	{:else}
		<div class="flex flex-col gap-4">
			{#each items as item (item.reviewId)}
				<FeedCard
					{item}
					token={data.token}
					onEdit={handleEdit}
					onDelete={handleDelete}
				/>
			{/each}
		</div>

		<InfiniteScrollSentinel {hasMore} {loading} onLoadMore={loadMore} />
	{/if}
</div>
