<script lang="ts">
	import { api } from '$lib/api';
	import { REPORT_REASON_LABELS } from '$lib/constants';
	import type { ReportableEntityType, ReportReason } from '$lib/types/moderationTypes';

	interface Props {
		open: boolean;
		targetId: string;
		targetType: ReportableEntityType;
		token: string;
		userId: string;
		onClose: () => void;
	}

	let { open = $bindable(false), targetId, targetType, token, userId, onClose }: Props = $props();

	const REASON_LABELS = REPORT_REASON_LABELS;

	let reason = $state<ReportReason>('Spam');
	let comment = $state('');
	let loading = $state(false);
	let success = $state(false);
	let errorMsg = $state('');

	$effect(() => {
		if (!open) {
			reason = 'Spam';
			comment = '';
			loading = false;
			success = false;
			errorMsg = '';
		}
	});

	async function submit() {
		if (loading) return;
		loading = true;
		errorMsg = '';
		try {
			await api.createReport({
				targetId,
				targetType,
				reason,
				comment: comment.trim() || undefined,
				reporterId: userId || undefined,
			}, token);
			success = true;
		} catch {
			errorMsg = 'Не вдалося надіслати скаргу';
		} finally {
			loading = false;
		}
	}

	function onKeydown(e: KeyboardEvent) {
		if (open && e.key === 'Escape') onClose();
	}
</script>

<svelte:window onkeydown={onKeydown} />

{#if open}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
		role="dialog"
		aria-modal="true"
		tabindex={-1}
		onclick={(e) => { if (e.target === e.currentTarget) onClose(); }}
		onkeydown={(e) => { if (e.key === 'Escape') onClose(); }}
	>
		<div
			class="w-full max-w-sm bg-bkg-header border border-gray-700 rounded-2xl shadow-2xl overflow-hidden"
		>
			<div class="flex items-center justify-between px-5 py-4 border-b border-gray-800">
				<h2 class="text-base font-bold text-white/95">Поскаржитись</h2>
				<button
					onclick={onClose}
					class="text-text-muted hover:text-white/95 transition-colors leading-none text-xl"
					aria-label="Закрити"
				>×</button>
			</div>

			<div class="px-5 py-4">
				{#if success}
					<p class="text-center text-green-400 text-sm py-4">Скаргу надіслано</p>
					<button
						onclick={onClose}
						class="w-full mt-2 py-2 rounded-lg bg-gray-700 hover:bg-gray-600 text-white/90 text-sm font-semibold transition-colors"
					>Закрити</button>
				{:else}
					<label class="block mb-3">
						<span class="text-xs text-text-muted mb-1 block">Причина</span>
						<select
							bind:value={reason}
							class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white/90 focus:outline-none focus:border-brand-accent"
						>
							{#each Object.entries(REASON_LABELS) as [val, label] (val)}
								<option value={val}>{label}</option>
							{/each}
						</select>
					</label>

					<label class="block mb-4">
						<span class="text-xs text-text-muted mb-1 block">Деталі (необов'язково)</span>
						<textarea
							bind:value={comment}
							rows="3"
							maxlength="500"
							placeholder="Опишіть проблему..."
							class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white/90 placeholder-text-muted resize-none focus:outline-none focus:border-brand-accent"
						></textarea>
					</label>

					{#if errorMsg}
						<p class="text-red-400 text-xs mb-3">{errorMsg}</p>
					{/if}

					<button
						onclick={submit}
						disabled={loading}
						class="w-full py-2 rounded-lg bg-brand-accent hover:bg-brand-hover text-white font-semibold text-sm transition-colors disabled:opacity-50"
					>{loading ? '...' : 'Надіслати скаргу'}</button>
				{/if}
			</div>
		</div>
	</div>
{/if}
