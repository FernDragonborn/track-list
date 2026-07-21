<script lang="ts">
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import { extractError } from '$lib/utils/errors';
	import type { ReviewItem } from '$lib/types/searchTypes';
	import StarRating from './StarRating.svelte';

	interface Props {
		mediaId: string;
		token: string;
		existing?: ReviewItem | null;
		initialRating?: number;
		onSave: (review: ReviewItem) => void;
		onClose: () => void;
	}

	let { mediaId, token, existing = null, initialRating = 0, onSave, onClose }: Props = $props();

	// initialRating (from sidebar click) takes priority so user's hover intent is preserved
	let rating = $state(untrack(() => (initialRating > 0 ? initialRating : (existing?.rating ?? 0))));
	let content = $state(untrack(() => existing?.content ?? ''));
	let loading = $state(false);
	let errorMsg = $state<string | null>(null);

	const isEditing = $derived(existing !== null);

	async function submit() {
		if (rating === 0) {
			errorMsg = 'Оберіть рейтинг';
			return;
		}
		loading = true;
		errorMsg = null;
		try {
			const body = { rating, content: content.trim() || undefined };
			if (isEditing && existing) {
				await api.updateReview(mediaId, existing.id, body, token);
				onSave({ ...existing, rating, content: content.trim() || undefined });
			} else {
				const review = await api.createReview(mediaId, body, token);
				onSave(review);
			}
			onClose();
		} catch (e: unknown) {
			errorMsg = extractError(e, 'Помилка збереження');
		} finally {
			loading = false;
		}
	}

	function handleBackdropClick(e: MouseEvent) {
		if (e.target === e.currentTarget) onClose();
	}

	function handleKeydown(e: KeyboardEvent) {
		if (e.key === 'Escape') onClose();
	}
</script>

<svelte:window onkeydown={handleKeydown} />

<div
	class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm px-4"
	role="dialog"
	aria-modal="true"
	tabindex={-1}
	onclick={handleBackdropClick}
	onkeydown={handleKeydown}
>
	<div class="w-full max-w-lg bg-bkg-header border border-gray-700 rounded-2xl shadow-2xl p-6">
		<h2 class="text-lg font-bold text-white/95 mb-4">
			{isEditing ? 'Редагувати рецензію' : 'Написати рецензію'}
		</h2>

		<!-- Star rating selector -->
		<div class="mb-4">
			<p class="text-xs font-semibold uppercase tracking-wider text-text-muted mb-2">
				Рейтинг {rating > 0 ? `${rating}/10` : ''}
			</p>
			<StarRating
				value={rating}
				interactive
				size="lg"
				onchange={(v) => (rating = v)}
			/>
		</div>

		<!-- Content textarea -->
		<div class="mb-4">
			<label
				for="review-content"
				class="text-xs font-semibold uppercase tracking-wider text-text-muted mb-2 block"
			>
				Текст рецензії <span class="normal-case opacity-60">(необов'язково)</span>
			</label>
			<textarea
				id="review-content"
				bind:value={content}
				rows={5}
				maxlength={10000}
				placeholder="Поділіться своїми враженнями..."
				class="w-full bg-black/30 border border-gray-700 rounded-lg px-3 py-2 text-white/90
				       placeholder:text-text-muted text-sm resize-none focus:outline-none
				       focus:border-brand-accent transition-colors"
			></textarea>
			<p class="text-right text-xs text-text-muted mt-1">{content.length}/10000</p>
		</div>

		{#if errorMsg}
			<p class="text-red-400 text-sm mb-4">{errorMsg}</p>
		{/if}

		<div class="flex gap-3 justify-end">
			<button
				type="button"
				onclick={onClose}
				disabled={loading}
				class="px-4 py-2 rounded-lg text-sm text-text-muted hover:text-white/90
				       hover:bg-white/5 transition-colors disabled:opacity-50"
			>
				Скасувати
			</button>
			<button
				type="button"
				onclick={submit}
				disabled={loading || rating === 0}
				class="px-5 py-2 rounded-lg bg-brand-accent hover:bg-brand-hover text-white text-sm
				       font-semibold transition-all active:scale-95 disabled:opacity-50
				       disabled:cursor-not-allowed shadow-lg shadow-brand-accent/20"
			>
				{#if loading}
					<span class="inline-flex items-center gap-2">
						<span
							class="w-3.5 h-3.5 border-2 border-white/60 border-t-transparent rounded-full animate-spin"
						></span>
						Збереження...
					</span>
				{:else}
					{isEditing ? 'Зберегти' : 'Опублікувати'}
				{/if}
			</button>
		</div>
	</div>
</div>
