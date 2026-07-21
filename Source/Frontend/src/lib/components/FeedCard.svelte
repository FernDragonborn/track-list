<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import { getAvatarUrl } from '$lib/utils/avatar';
	import { formatDate } from '$lib/utils/format';
	import { renderMarkdown } from '$lib/utils/markdown';
	import type { FeedItemDto } from '$lib/types/reviewTypes';
	import SafeHtml from './SafeHtml.svelte';
	import StarRating from './StarRating.svelte';

	interface Props {
		item: FeedItemDto;
		token: string | null;
		onEdit?: (item: FeedItemDto) => void;
		onDelete?: (reviewId: string) => void;
	}

	let { item, token, onEdit, onDelete }: Props = $props();

	let confirmingDelete = $state(false);

	let liked = $state(untrack(() => item.isLikedByMe));
	let likeCount = $state(untrack(() => item.likeCount));
	let likeLoading = $state(false);

	// Sync local state when parent passes a new item
	$effect(() => {
		liked = item.isLikedByMe;
		likeCount = item.likeCount;
	});

	const mediaPath = $derived(item.mediaExternalId ? `/media/external/${item.mediaExternalId}` : `/media/${item.mediaId}`);
	const reviewPath = $derived(`${mediaPath}?review=${item.reviewId}#review-${item.reviewId}`);
	const avatarUrl = $derived(getAvatarUrl(item.username, item.profilePicUrl));

	let expanded = $state(false);
	const PREVIEW_LIMIT = 240;
	const needsExpand = $derived((item.content?.length ?? 0) > PREVIEW_LIMIT);
	const visibleContent = $derived.by(() => {
		if (!item.content) return '';
		if (expanded || !needsExpand) return item.content;
		return item.content.slice(0, PREVIEW_LIMIT) + '…';
	});

	async function toggleLike() {
		if (!token || likeLoading) return;
		likeLoading = true;
		const prevLiked = liked;
		const prevCount = likeCount;
		liked = !liked;
		likeCount += liked ? 1 : -1;
		try {
			const result = await api.toggleReviewLike(item.mediaId, item.reviewId, token);
			liked = result.isLiked;
			likeCount = result.likeCount;
		} catch {
			liked = prevLiked;
			likeCount = prevCount;
		} finally {
			likeLoading = false;
		}
	}
</script>

<article class="relative bg-bkg-header rounded-lg border border-gray-700/50 overflow-hidden">
	<!-- Whole-card click → review. Inner anchors/buttons use relative z-10 to keep their own targets. -->
	<a href={resolve(reviewPath as '/')} class="absolute inset-0 z-0" aria-label="Перейти до рецензії"></a>

	<div class="flex gap-0 items-start">
		<!-- Media poster → media page (no review anchor). -->
		{#if item.mediaPosterUrl}
			<a href={resolve(mediaPath as '/')} class="relative z-10 shrink-0 self-start block" style="width:128px;height:192px">
				<img
					src={item.mediaPosterUrl}
					alt={item.mediaTitle ?? ''}
					class="w-full h-full object-cover block"
				/>
			</a>
		{/if}

		<!-- Text body -->
		<div class="flex-1 min-w-0 p-4">

			<!-- Media title (keep as visible link too) -->
			<a
				href={resolve(mediaPath as '/')}
				class="relative z-10 text-xs font-semibold text-brand-accent hover:underline mb-2 block truncate w-fit"
			>
				{item.mediaTitle ?? 'Медіа'}
			</a>

			<!-- Author row — pointer-events-none on wrapper so non-interactive gaps
			     pass clicks through to the full-card overlay anchor. -->
			<div class="relative z-10 flex items-center justify-between gap-2 mb-2 pointer-events-none">
				<div class="flex items-center gap-2 min-w-0">
					<a
						href={resolve(`/profile/${item.username}`)}
						class="pointer-events-auto flex items-center gap-2 min-w-0 group"
					>
						<img src={avatarUrl} alt={item.username} class="w-7 h-7 rounded-full object-cover shrink-0" />
						<span class="text-white/90 text-sm font-medium group-hover:text-brand-accent transition-colors truncate">
							{item.username}
						</span>
					</a>
					<StarRating value={item.rating} size="sm" />
					<span class="text-text-muted text-xs shrink-0">{item.rating}/10</span>
				</div>
				<span class="text-text-muted text-xs shrink-0">
					{formatDate(item.createdAt)}
				</span>
			</div>

			<!-- Review content (collapsible inline) -->
			{#if item.content}
				<SafeHtml
					content={renderMarkdown(visibleContent)}
					class="tl-feed-prose text-white/75 text-sm leading-relaxed mb-2 prose prose-invert max-w-none"
				/>
				{#if needsExpand}
					<button
						type="button"
						onclick={(e) => { e.preventDefault(); e.stopPropagation(); expanded = !expanded; }}
						class="relative z-10 mb-3 text-xs text-brand-accent hover:underline"
					>
						{expanded ? 'Згорнути' : 'Розгорнути повністю'}
					</button>
				{/if}
			{/if}

			<!-- Top comment preview (US-304) -->
			{#if item.topComment}
				<div class="bg-black/20 rounded-md px-3 py-2 mb-3 text-xs">
					<span class="text-brand-accent font-medium">{item.topComment.username}</span>
					<span class="text-text-muted mx-1">·</span>
					<span class="text-white/60 line-clamp-1">{item.topComment.content}</span>
					{#if item.topComment.likeCount > 0}
						<span class="text-text-muted ml-1">♥ {item.topComment.likeCount}</span>
					{/if}
				</div>
			{/if}

			<!-- Footer: like + comments link + owner actions.
			     pointer-events-none on wrapper so empty space between children stays
			     clickable for the underlying full-card overlay anchor; children opt back in. -->
			<div class="relative z-10 flex items-center gap-4 pointer-events-none">
				<button
					onclick={token ? toggleLike : () => goto(resolve('/auth/login'))}
					disabled={likeLoading}
					class="pointer-events-auto flex items-center gap-1.5 text-sm transition-colors
					       {liked ? 'text-red-400' : 'text-text-muted hover:text-red-400'}
					       disabled:opacity-40"
				>
					<span>{liked ? '♥' : '♡'}</span>
					<span class="text-xs">{likeCount}</span>
				</button>

				<a
					href={resolve(reviewPath as '/')}
					class="pointer-events-auto flex items-center gap-1.5 text-sm text-text-muted hover:text-white/90 transition-colors"
				>
					<span>💬</span>
					<span class="text-xs">
						{#if item.commentCount > 0}
							Переглянути всі {item.commentCount} коментарів...
						{:else}
							Коментарі
						{/if}
					</span>
				</a>

				{#if onEdit || onDelete}
					<div class="pointer-events-auto ml-auto flex items-center gap-3">
						{#if onEdit}
							<button
								onclick={() => onEdit!(item)}
								class="text-xs text-text-muted hover:text-white/90 transition-colors"
							>
								Редагувати
							</button>
						{/if}
						{#if onDelete}
							{#if confirmingDelete}
								<span class="text-xs text-text-muted">Видалити?</span>
								<button
									onclick={() => { onDelete!(item.reviewId); confirmingDelete = false; }}
									class="text-xs text-red-400 hover:text-red-300 transition-colors"
								>Так</button>
								<button
									onclick={() => (confirmingDelete = false)}
									class="text-xs text-text-muted hover:text-white/90 transition-colors"
								>Ні</button>
							{:else}
								<button
									onclick={() => (confirmingDelete = true)}
									class="text-xs text-text-muted hover:text-red-400 transition-colors"
								>
									Видалити
								</button>
							{/if}
						{/if}
					</div>
				{/if}
			</div>
		</div>
	</div>
</article>

<style>
	/* Class lives on SafeHtml's child element, so the whole selector is :global. */
	:global(.tl-feed-prose p) { margin-bottom: 0.5em; }
	:global(.tl-feed-prose p:last-child) { margin-bottom: 0; }
	:global(.tl-feed-prose ul),
	:global(.tl-feed-prose ol) { margin: 0.5em 0; }
</style>
