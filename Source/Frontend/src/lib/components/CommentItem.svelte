<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import { getAvatarUrl } from '$lib/utils/avatar';
	import { formatDate } from '$lib/utils/format';
	import { isOwner, canModerate } from '$lib/utils/permissions';
	import { renderMarkdown } from '$lib/utils/markdown';
	import { selectedLang } from '$lib/stores/language';
	import type { CommentItem } from '$lib/types/reviewTypes';
	import ReportModal from './ReportModal.svelte';
	import SafeHtml from './SafeHtml.svelte';
	import Self from './CommentItem.svelte';

	interface Props {
		mediaId: string;
		reviewId: string;
		comment: CommentItem;
		token: string | null;
		username: string | null;
		userRole: string | null;
		userId: string | null;
		isReply?: boolean;
		onDelete: (commentId: string) => void;
		onStartReply?: (parentId: string) => void;
	}

	let {
		mediaId,
		reviewId,
		comment,
		token,
		username,
		userRole,
		userId,
		isReply = false,
		onDelete,
		onStartReply,
	}: Props = $props();

	let liked = $state(untrack(() => comment.isLikedByMe));
	let likeCount = $state(untrack(() => comment.likeCount));
	let likeLoading = $state(false);
	let deleteLoading = $state(false);
	let showReportModal = $state(false);

	// Sync if parent mutates the same comment instance (defensive — usually keyed by id).
	$effect(() => {
		liked = comment.isLikedByMe;
		likeCount = comment.likeCount;
	});

	const isOwn = $derived(isOwner(comment.username, username));
	const canDelete = $derived(isOwn || canModerate(userRole));
	const canReport = $derived(!!token && !isOwn);

	const avatarUrl = $derived(getAvatarUrl(comment.username, comment.profilePicUrl));

	let translations = $state<Record<string, string>>({});
	let translating = $state(false);
	let showOriginal = $state(false);
	const hasTranslation = $derived(!!translations[$selectedLang]);
	const displayedText = $derived.by(() => {
		if (!comment.content) return '';
		if ($selectedLang === 'en' || showOriginal) return comment.content;
		return translations[$selectedLang] ?? comment.content;
	});
	const isTranslated = $derived(
		$selectedLang !== 'en' && !showOriginal && !!translations[$selectedLang]
	);
	const canToggleTranslation = $derived(!!comment.content && $selectedLang !== 'en');
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
			const r = await api.translateComment(mediaId, reviewId, comment.id, $selectedLang);
			translations = { ...translations, [r.lang]: r.translation };
			showOriginal = false;
		} catch {
			// noop
		} finally {
			translating = false;
		}
	}

	async function toggleLike() {
		if (!token || likeLoading) return;
		likeLoading = true;
		const prevLiked = liked;
		const prevCount = likeCount;
		liked = !liked;
		likeCount += liked ? 1 : -1;
		try {
			const result = await api.toggleCommentLike(mediaId, reviewId, comment.id, token);
			liked = result.isLiked;
			likeCount = result.likeCount;
		} catch {
			liked = prevLiked;
			likeCount = prevCount;
		} finally {
			likeLoading = false;
		}
	}

	async function deleteComment() {
		if (!token || deleteLoading) return;
		deleteLoading = true;
		try {
			await api.deleteComment(mediaId, reviewId, comment.id, token);
			onDelete(comment.id);
		} catch {
			// leave in place on error
		} finally {
			deleteLoading = false;
		}
	}
</script>

<div id="comment-{comment.id}" class="flex gap-2.5 {isReply ? 'ml-8 mt-2' : ''}">
	<a href={resolve(`/profile/${comment.username}`)} class="shrink-0 mt-0.5">
		<img src={avatarUrl} alt={comment.username} class="w-7 h-7 rounded-full object-cover" />
	</a>

	<div class="flex-1 min-w-0">
		<div class="bg-black/20 rounded-xl px-3 py-2">
			<div class="flex items-center gap-2 mb-0.5 flex-wrap">
				<a
					href={resolve(`/profile/${comment.username}`)}
					class="text-white/90 text-xs font-semibold hover:text-brand-accent transition-colors"
				>
					{comment.username}
				</a>
				<span class="text-text-muted text-[11px]">
					{formatDate(comment.createdAt)}
				</span>
				{#if canToggleTranslation}
					<button
						type="button"
						onclick={toggleTranslation}
						disabled={translating}
						class="ml-3 text-[11px] text-brand-accent hover:underline disabled:opacity-50 whitespace-nowrap"
					>{translateButtonLabel}</button>
				{/if}
			</div>
			{#if comment.content}
				{#if isTranslated}
					<p class="text-[10px] text-text-muted mb-1 italic">Перекладено DeepL</p>
				{/if}
				<!-- eslint-disable-next-line svelte/no-at-html-tags -- marked → DOMPurify sanitized -->
				<SafeHtml content={renderMarkdown(displayedText)} class="text-white/80 text-sm leading-relaxed break-words prose prose-invert prose-sm max-w-none" />
			{:else}
				<p class="text-text-muted text-sm italic">[видалено]</p>
			{/if}
		</div>

		<!-- Actions -->
		<div class="flex items-center gap-3 mt-1 ml-1">
			<button
				onclick={token ? toggleLike : () => goto(resolve('/auth/login'))}
				disabled={likeLoading}
				class="flex items-center gap-1 text-xs transition-colors
				       {liked ? 'text-red-400' : 'text-text-muted hover:text-red-400'}
				       disabled:opacity-40"
				aria-label="Вподобати"
			>
				{liked ? '♥' : '♡'}
				{#if likeCount > 0}<span>{likeCount}</span>{/if}
			</button>

			{#if !isReply && onStartReply}
				<button
					onclick={token ? () => onStartReply(comment.id) : () => goto(resolve('/auth/login'))}
					class="text-xs text-text-muted hover:text-white/90 transition-colors"
				>
					Відповісти
				</button>
			{/if}

			{#if canReport}
				<button
					onclick={() => (showReportModal = true)}
					class="text-xs text-text-muted hover:text-yellow-400 transition-colors"
					title="Поскаржитись"
				>⚑</button>
			{/if}
			{#if canDelete && token}
				<button
					onclick={deleteComment}
					disabled={deleteLoading}
					class="text-xs text-text-muted hover:text-red-400 transition-colors disabled:opacity-40 ml-auto"
				>
					{deleteLoading ? '...' : 'Видалити'}
				</button>
			{/if}
		</div>

		<!-- Nested replies (level 1 only) -->
		{#if comment.replies?.length > 0}
			<div class="mt-1 flex flex-col gap-1">
				{#each comment.replies as reply (reply.id)}
					<Self
						{mediaId}
						{reviewId}
						comment={reply}
						{token}
						{username}
						{userRole}
						{userId}
						isReply={true}
						{onDelete}
					/>
				{/each}
			</div>
		{/if}
	</div>
</div>

{#if showReportModal && token}
	<ReportModal
		bind:open={showReportModal}
		targetId={comment.id}
		targetType="Comment"
		{token}
		userId={userId ?? ''}
		onClose={() => (showReportModal = false)}
	/>
{/if}
