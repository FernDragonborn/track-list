<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import { selectedLang } from '$lib/stores/language';
	import { renderMarkdown } from '$lib/utils/markdown';
	import { truncate } from '$lib/utils/text';
	import { getAvatarUrl } from '$lib/utils/avatar';
	import { formatDate } from '$lib/utils/format';
	import { isOwner, canModerate } from '$lib/utils/permissions';
	import type { ReviewItem } from '$lib/types/searchTypes';
	import CommentSection from './CommentSection.svelte';
	import ReviewForm from './ReviewForm.svelte';
	import ReportModal from './ReportModal.svelte';
	import SafeHtml from './SafeHtml.svelte';
	import StarRating from './StarRating.svelte';

	interface Props {
		mediaId: string;
		review: ReviewItem;
		token: string | null;
		username: string | null;
		userRole: string | null;
		userId: string | null;
		onDelete: (reviewId: string) => void;
		onUpdate: (updated: ReviewItem) => void;
		autoOpenComments?: boolean;
		highlightCommentId?: string;
	}

	let { mediaId, review, token, username, userRole, userId, onDelete, onUpdate, autoOpenComments = false, highlightCommentId }: Props = $props();

	let liked = $state(untrack(() => review.isLikedByMe));
	let likeCount = $state(untrack(() => review.likeCount));
	let likeLoading = $state(false);
	let showComments = $state(false);

	$effect(() => {
		if (autoOpenComments) showComments = true;
	});

	// Sync if parent mutates the same review instance (defensive — usually keyed by id).
	$effect(() => {
		liked = review.isLikedByMe;
		likeCount = review.likeCount;
	});
	let showEditForm = $state(false);
	let deleteLoading = $state(false);
	let confirmingDelete = $state(false);
	let showReportModal = $state(false);

	const isOwn = $derived(isOwner(review.username, username));
	const canDelete = $derived(isOwn || canModerate(userRole));
	const canReport = $derived(!!token && !isOwn);
	const compact = $derived(!review.content || review.content.trim() === '');

	let translations = $state<Record<string, string>>({});
	let translating = $state(false);
	let showOriginal = $state(false);
	let expandedLong = $state(false);

	const LONG_THRESHOLD = 1000;
	const PREVIEW_LIMIT = 500;
	const hasTranslation = $derived(!!translations[$selectedLang]);
	const displayedContent = $derived.by(() => {
		const lang = $selectedLang;
		if (lang === 'en' || !review.content || showOriginal) return { text: review.content ?? '', translated: false };
		const cached = translations[lang];
		if (cached) return { text: cached, translated: true };
		return { text: review.content, translated: false };
	});
	const isLong = $derived(displayedContent.text.length > LONG_THRESHOLD);
	const visibleContent = $derived(
		!isLong || expandedLong ? displayedContent.text : truncate(displayedContent.text, PREVIEW_LIMIT),
	);
	const canToggleTranslation = $derived(!!review.content && $selectedLang !== 'en');
	async function toggleTranslation() {
		if (!canToggleTranslation) return;
		if (hasTranslation) {
			showOriginal = !showOriginal;
			return;
		}
		if (translating) return;
		translating = true;
		try {
			const r = await api.translateReview(mediaId, review.id, $selectedLang);
			translations = { ...translations, [r.lang]: r.translation };
			showOriginal = false;
		} catch {
			// noop
		} finally {
			translating = false;
		}
	}
	const translateButtonLabel = $derived.by(() => {
		if (translating) return 'Перекладаю…';
		if (!hasTranslation) return `Перекласти на ${$selectedLang.toUpperCase()}`;
		return showOriginal ? `Показати переклад (${$selectedLang.toUpperCase()})` : 'Показати оригінал';
	});

	const avatarUrl = $derived(getAvatarUrl(review.username, review.profilePicUrl));

	async function toggleLike() {
		if (!token || likeLoading) return;
		likeLoading = true;
		const prevLiked = liked;
		const prevCount = likeCount;
		liked = !liked;
		likeCount += liked ? 1 : -1;
		try {
			const result = await api.toggleReviewLike(mediaId, review.id, token);
			liked = result.isLiked;
			likeCount = result.likeCount;
		} catch {
			liked = prevLiked;
			likeCount = prevCount;
		} finally {
			likeLoading = false;
		}
	}

	async function deleteReview() {
		if (!token || deleteLoading) return;
		deleteLoading = true;
		try {
			await api.deleteReview(mediaId, review.id, token);
			onDelete(review.id);
		} catch {
			// leave in place on error
		} finally {
			deleteLoading = false;
		}
	}

	function handleSave(updated: ReviewItem) {
		onUpdate(updated);
		showEditForm = false;
	}
</script>

{#if showEditForm && token}
	<ReviewForm
		{mediaId}
		{token}
		existing={review}
		onSave={handleSave}
		onClose={() => (showEditForm = false)}
	/>
{/if}

<article id="review-{review.id}" class="scroll-mt-28 bg-bkg-header rounded-xl border border-gray-700/50 target:ring-2 target:ring-yellow-400/60 transition-shadow {compact ? 'px-3 py-2' : 'p-5'}">
	<!-- Header -->
	<div class="flex items-center justify-between gap-3 {compact ? '' : 'items-start mb-3'}">
		<div class="flex items-center gap-2.5 min-w-0">
			<a href={resolve(`/profile/${review.username}`)} class="shrink-0">
				<img
					src={avatarUrl}
					alt={review.username}
					class="{compact ? 'w-7 h-7' : 'w-9 h-9'} rounded-full object-cover"
				/>
			</a>
			{#if compact}
				<a
					href={resolve(`/profile/${review.username}`)}
					class="text-white/90 font-semibold text-sm hover:text-brand-accent transition-colors truncate"
				>
					{review.username}
				</a>
				{#if review.isFromFollowing}
					<span class="text-[9px] uppercase tracking-wide px-1.5 py-0.5 rounded-md font-bold bg-brand-accent/15 text-brand-accent border border-brand-accent/40 shrink-0" title="Підписка">★</span>
				{/if}
				<div class="flex items-center gap-1.5 shrink-0">
					<StarRating value={review.rating} size="sm" />
					<span class="text-text-muted text-xs tabular-nums">{review.rating}/10</span>
				</div>
			{:else}
				<div>
					<div class="flex items-center gap-1.5 flex-wrap">
						<a
							href={resolve(`/profile/${review.username}`)}
							class="text-white/90 font-semibold text-sm hover:text-brand-accent transition-colors"
						>
							{review.username}
						</a>
						{#if review.isFromFollowing}
							<span class="text-[9px] uppercase tracking-wide px-1.5 py-0.5 rounded-md font-bold bg-brand-accent/15 text-brand-accent border border-brand-accent/40" title="Підписка">★ Підписка</span>
						{/if}
					</div>
					<div class="mt-0.5 flex items-center gap-1.5">
						<StarRating value={review.rating} size="sm" />
						<span class="text-text-muted text-xs">{review.rating}/10</span>
					</div>
				</div>
			{/if}
		</div>

		<div class="flex items-center gap-2 shrink-0">
			{#if canToggleTranslation}
				<button
					type="button"
					onclick={toggleTranslation}
					disabled={translating}
					class="ml-3 text-xs text-brand-accent hover:underline disabled:opacity-50 whitespace-nowrap"
				>{translateButtonLabel}</button>
			{/if}
			{#if compact}
				<!-- Like + comment inline for rating-only cards -->
				<button
					onclick={token ? toggleLike : () => goto(resolve('/auth/login'))}
					disabled={likeLoading}
					class="flex items-center gap-1 text-xs transition-colors
					       {liked ? 'text-red-400' : 'text-text-muted hover:text-red-400'}
					       disabled:opacity-40"
					title="Подобається"
				>
					<span>{liked ? '♥' : '♡'}</span>
					<span class="tabular-nums">{likeCount}</span>
				</button>
				<button
					onclick={() => (showComments = !showComments)}
					class="flex items-center gap-1 text-xs text-text-muted hover:text-white/90 transition-colors"
					title="Коментарі"
				>
					<span>💬</span>
					<span class="tabular-nums">{review.commentCount}</span>
				</button>
			{/if}
			<span class="text-text-muted text-xs whitespace-nowrap">
				{formatDate(review.createdAt)}
			</span>
			{#if isOwn && token}
				<button
					onclick={() => (showEditForm = true)}
					class="text-xs text-text-muted hover:text-white/90 transition-colors px-1"
					title="Редагувати"
				>✎</button>
			{/if}
			{#if canReport}
				<button
					onclick={() => (showReportModal = true)}
					class="text-xs text-text-muted hover:text-yellow-400 transition-colors px-1"
					title="Поскаржитись"
				>⚑</button>
			{/if}
			{#if canDelete && token}
				{#if confirmingDelete}
					<span class="flex items-center gap-1">
						<button
							onclick={deleteReview}
							disabled={deleteLoading}
							class="text-xs text-red-400 hover:text-red-300 transition-colors disabled:opacity-40 px-1 font-semibold"
						>{deleteLoading ? '...' : 'Так'}</button>
						<button
							onclick={() => (confirmingDelete = false)}
							class="text-xs text-text-muted hover:text-white/90 transition-colors px-1"
						>Ні</button>
					</span>
				{:else}
					<button
						onclick={() => (confirmingDelete = true)}
						class="text-xs text-text-muted hover:text-red-400 transition-colors px-1"
						title="Видалити"
					>✕</button>
				{/if}
			{/if}
		</div>
	</div>

	{#if !compact}
		<!-- Content (marked → DOMPurify; preserves newlines + bold/italic/lists/links) -->
		{#if review.content}
			{#if displayedContent.translated}
				<p class="text-[10px] text-text-muted mb-2 italic">Перекладено DeepL</p>
			{/if}
			<SafeHtml
				content={renderMarkdown(visibleContent)}
				class="tl-review-prose text-white/80 text-sm leading-relaxed prose prose-invert max-w-none mb-2"
			/>
			{#if isLong}
				<button
					type="button"
					onclick={() => (expandedLong = !expandedLong)}
					class="mb-3 text-xs text-brand-accent hover:underline"
				>{expandedLong ? 'Згорнути' : 'Розгорнути повністю'}</button>
			{/if}
		{/if}

		<!-- Footer: like + comments toggle -->
		<div class="flex items-center gap-4 mt-2">
			<button
				onclick={token ? toggleLike : () => goto(resolve('/auth/login'))}
				disabled={likeLoading}
				class="flex items-center gap-1.5 text-sm transition-colors
				       {liked ? 'text-red-400' : 'text-text-muted hover:text-red-400'}
				       disabled:opacity-40"
			>
				<span class="text-base">{liked ? '♥' : '♡'}</span>
				<span class="text-xs">{likeCount}</span>
			</button>

			<button
				onclick={() => (showComments = !showComments)}
				class="flex items-center gap-1.5 text-sm text-text-muted hover:text-white/90 transition-colors"
			>
				<span class="text-base">💬</span>
				<span class="text-xs">{review.commentCount}</span>
			</button>
		</div>
	{/if}

	<!-- Comments -->
	{#if showComments}
		<CommentSection
			{mediaId}
			reviewId={review.id}
			{token}
			{username}
			{userRole}
			{userId}
			{highlightCommentId}
		/>
	{/if}
</article>

{#if showReportModal && token}
	<ReportModal
		bind:open={showReportModal}
		targetId={review.id}
		targetType="Review"
		{token}
		userId={userId ?? ''}
		onClose={() => (showReportModal = false)}
	/>
{/if}

<style>
	/* Wider paragraph + heading spacing — better readability for multi-paragraph reviews.
	   Class lives on SafeHtml's child element, so the whole selector is :global. */
	:global(.tl-review-prose p) { margin-bottom: 1em; }
	:global(.tl-review-prose p:last-child) { margin-bottom: 0; }
	:global(.tl-review-prose ul),
	:global(.tl-review-prose ol) { margin: 0.75em 0; }
</style>
