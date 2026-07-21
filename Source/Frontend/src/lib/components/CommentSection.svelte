<script lang="ts">
	import { resolve } from '$app/paths';
	import { api } from '$lib/api';
	import { extractError } from '$lib/utils/errors';
	import type { CommentItem } from '$lib/types/reviewTypes';
	import CommentItemComponent from './CommentItem.svelte';

	interface Props {
		mediaId: string;
		reviewId: string;
		token: string | null;
		username: string | null;
		userRole: string | null;
		userId: string | null;
		highlightCommentId?: string;
	}

	let { mediaId, reviewId, token, username, userRole, userId, highlightCommentId }: Props = $props();

	let comments = $state<CommentItem[]>([]);
	let loading = $state(true);
	let newText = $state('');
	let replyTo = $state<string | null>(null);
	let replyText = $state('');
	let submitting = $state(false);
	let errorMsg = $state<string | null>(null);

	$effect(() => {
		// track deps explicitly
		const _mid = mediaId;
		const _rid = reviewId;
		let cancelled = false;
		loadComments(_mid, _rid, cancelled);
		return () => { cancelled = true; };
	});

	async function loadComments(_mediaId: string, _reviewId: string, cancelled: boolean) {
		loading = true;
		try {
			const result = await api.getComments(_mediaId, _reviewId, token ?? undefined);
			if (cancelled) return;
			comments = result;
			if (highlightCommentId) {
				// scroll and briefly highlight the reported comment after DOM updates
				setTimeout(() => {
					const el = document.getElementById(`comment-${highlightCommentId}`);
					if (el) {
						el.scrollIntoView({ behavior: 'smooth', block: 'center' });
						el.classList.add('ring-2', 'ring-yellow-400', 'rounded-xl');
						setTimeout(() => el.classList.remove('ring-2', 'ring-yellow-400', 'rounded-xl'), 3000);
					}
				}, 100);
			}
		} catch {
			// show empty on error
		} finally {
			if (!cancelled) loading = false;
		}
	}

	async function addComment() {
		if (!token || !newText.trim() || submitting) return;
		submitting = true;
		errorMsg = null;
		try {
			const created = await api.createComment(mediaId, reviewId, { content: newText.trim() }, token);
			comments = [created, ...comments];
			newText = '';
		} catch (e: unknown) {
			errorMsg = extractError(e);
		} finally {
			submitting = false;
		}
	}

	async function addReply(parentId: string) {
		if (!token || !replyText.trim() || submitting) return;
		submitting = true;
		errorMsg = null;
		try {
			const created = await api.createComment(
				mediaId,
				reviewId,
				{ content: replyText.trim(), parentCommentId: parentId },
				token,
			);
			comments = comments.map((c) =>
				c.id === parentId ? { ...c, replies: [...(c.replies ?? []), created] } : c,
			);
			replyText = '';
			replyTo = null;
		} catch (e: unknown) {
			errorMsg = extractError(e);
		} finally {
			submitting = false;
		}
	}

	function handleDelete(commentId: string) {
		comments = comments.filter((c) => c.id !== commentId);
		comments = comments.map((c) => ({
			...c,
			replies: (c.replies ?? []).filter((r) => r.id !== commentId),
		}));
	}

	function handleStartReply(parentId: string) {
		replyTo = replyTo === parentId ? null : parentId;
		replyText = '';
		errorMsg = null;
	}
</script>

<div class="mt-3 border-t border-gray-700/50 pt-3">
	{#if loading}
		<p class="text-text-muted text-sm py-2">Завантаження...</p>
	{:else if comments.length === 0}
		<p class="text-text-muted text-sm py-2">Коментарів ще немає</p>
	{:else}
		<div class="flex flex-col gap-3">
			{#each comments as comment (comment.id)}
				<div>
					<CommentItemComponent
						{mediaId}
						{reviewId}
						{comment}
						{token}
						{username}
						{userRole}
						{userId}
						onDelete={handleDelete}
						onStartReply={handleStartReply}
					/>

					<!-- Reply form for this comment -->
					{#if replyTo === comment.id}
						<div class="ml-10 mt-2 flex gap-2">
							<input
								type="text"
								bind:value={replyText}
								placeholder="Відповісти..."
								maxlength={10240}
								class="flex-1 bg-black/30 border border-gray-700 rounded-lg px-3 py-1.5 text-sm
								       text-white/90 placeholder:text-text-muted focus:outline-none
								       focus:border-brand-accent transition-colors"
							/>
							<button
								onclick={() => addReply(comment.id)}
								disabled={submitting || !replyText.trim()}
								class="px-3 py-1.5 rounded-lg bg-brand-accent text-white text-sm font-semibold
								       hover:bg-brand-hover transition-all active:scale-95 disabled:opacity-50"
							>
								{submitting ? '...' : '↩'}
							</button>
							<button
								onclick={() => {
									replyTo = null;
									replyText = '';
								}}
								class="px-2 py-1.5 text-text-muted hover:text-white/90 transition-colors text-sm"
							>✕</button>
						</div>
					{/if}
				</div>
			{/each}
		</div>
	{/if}

	{#if errorMsg}
		<p class="text-red-400 text-xs mt-2">{errorMsg}</p>
	{/if}

	<!-- New top-level comment form -->
	{#if token}
		<div class="mt-3 flex gap-2">
			<input
				type="text"
				bind:value={newText}
				placeholder="Написати коментар..."
				maxlength={10240}
				onkeydown={(e) => {
					if (e.key === 'Enter' && !e.shiftKey) {
						e.preventDefault();
						addComment();
					}
				}}
				class="flex-1 bg-black/30 border border-gray-700 rounded-lg px-3 py-1.5 text-sm
				       text-white/90 placeholder:text-text-muted focus:outline-none
				       focus:border-brand-accent transition-colors"
			/>
			<button
				onclick={addComment}
				disabled={submitting || !newText.trim()}
				class="px-3 py-1.5 rounded-lg bg-brand-accent text-white text-sm font-semibold
				       hover:bg-brand-hover transition-all active:scale-95 disabled:opacity-50"
			>
				{submitting ? '...' : '↵'}
			</button>
		</div>
	{:else}
		<p class="text-text-muted text-xs mt-3">
			<a href={resolve('/auth/login')} class="text-brand-accent hover:underline">Увійдіть</a>, щоб коментувати
		</p>
	{/if}
</div>
