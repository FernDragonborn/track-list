<script lang="ts">
	import { resolve } from '$app/paths';
	import { api } from '$lib/api';
	import { TRACKING_STATUS_LABELS } from '$lib/constants';
	import type { ProfileTrackingItem, TrackingStatusCode } from '$lib/types/trackingTypes';

	interface Props {
		items: ProfileTrackingItem[];
		isOwnProfile: boolean;
		token: string | null;
		onDelete?: (mediaId: string) => void;
		onUpdate?: (mediaId: string, status: TrackingStatusCode, progress: number) => void;
	}

	let { items, isOwnProfile, token, onDelete, onUpdate }: Props = $props();

	const GROUPS: { code: TrackingStatusCode; label: string }[] = [
		{ code: 'watching', label: TRACKING_STATUS_LABELS.watching },
		{ code: 'planned', label: TRACKING_STATUS_LABELS.planned },
		{ code: 'completed', label: TRACKING_STATUS_LABELS.completed },
		{ code: 'dropped', label: TRACKING_STATUS_LABELS.dropped },
	];

	let progressMap = $derived<Record<string, number | null>>(
		Object.fromEntries(items.map((i) => [i.mediaId, i.progress ?? null])),
	);
	let savingMap = $state<Record<string, boolean>>({});
	let deletingMap = $state<Record<string, boolean>>({});
	let debounceTimers: Record<string, ReturnType<typeof setTimeout>> = {};
	$effect(() => {
		return () => { Object.values(debounceTimers).forEach(clearTimeout); };
	});

	function debounceSave(item: ProfileTrackingItem) {
		const id = item.mediaId;
		if (debounceTimers[id]) clearTimeout(debounceTimers[id]);
		debounceTimers[id] = setTimeout(() => saveProgress(item), 800);
	}

	function grouped(code: TrackingStatusCode) {
		return items.filter((i) => i.status === code);
	}

	async function deleteItem(item: ProfileTrackingItem) {
		if (!token) return;
		deletingMap = { ...deletingMap, [item.mediaId]: true };
		try {
			await api.deleteTrackingStatus(item.mediaId, token);
			onDelete?.(item.mediaId);
		} catch {
			// silent
		} finally {
			deletingMap = { ...deletingMap, [item.mediaId]: false };
		}
	}

	async function saveProgress(item: ProfileTrackingItem) {
		if (!token) return;
		const progress = progressMap[item.mediaId] ?? 0;
		const autoComplete = item.mediaEpisodeCount != null && progress >= item.mediaEpisodeCount;
		const autoStart = item.status === 'planned' && progress > 0;
		const statusToSave: TrackingStatusCode = autoComplete ? 'completed' : autoStart ? 'watching' : item.status;
		savingMap = { ...savingMap, [item.mediaId]: true };
		try {
			await api.upsertTrackingStatus(
				{ mediaId: item.mediaId, status: statusToSave, progress: progress || undefined },
				token,
			);
			if (autoComplete || autoStart) onUpdate?.(item.mediaId, statusToSave, progress);
		} catch {
			// silent
		} finally {
			savingMap = { ...savingMap, [item.mediaId]: false };
		}
	}
</script>

{#if items.length === 0}
	<div class="py-12 text-center text-text-muted">
		<p>Список порожній</p>
	</div>
{:else}
	<div class="flex flex-col gap-8">
		{#each GROUPS as group (group.code)}
			{@const groupItems = grouped(group.code)}
			{#if groupItems.length > 0}
				<section id="group-{group.code}">
					<h3
						class="text-xs font-semibold uppercase tracking-wider text-text-muted mb-3 flex items-center gap-2"
					>
						{group.label}
						<span class="text-brand-accent font-bold">{groupItems.length}</span>
					</h3>
					<div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
						{#each groupItems as item (item.mediaId)}
							<div
								class="bg-bkg-header/60 rounded-xl border border-gray-800 overflow-hidden
								       hover:border-gray-600 transition-colors"
							>
								<a href={resolve(`/media/${item.mediaId}`)} class="block">
									<div class="aspect-[2/3] bg-gray-800 relative">
										{#if item.mediaPosterUrl}
											<img
												src={item.mediaPosterUrl}
												alt={item.mediaTitle ?? ''}
												class="w-full h-full object-cover"
												loading="lazy"
											/>
										{:else}
											<div class="w-full h-full flex items-center justify-center">
												<svg
													class="w-8 h-8 text-gray-600"
													fill="currentColor"
													viewBox="0 0 20 20"
												>
													<path
														fill-rule="evenodd"
														d="M4 3a2 2 0 00-2 2v10a2 2 0 002 2h12a2 2 0 002-2V5a2 2 0 00-2-2H4zm12 12H4l4-8 3 6 2-4 3 6z"
														clip-rule="evenodd"
													/>
												</svg>
											</div>
										{/if}
										{#if item.mediaType}
											<span
												class="absolute top-1.5 left-1.5 text-[9px] font-bold uppercase
												       px-1.5 py-0.5 rounded bg-black/70 text-white/80"
											>
												{item.mediaType?.toLowerCase() === 'series' ? 'Серіал' : 'Фільм'}
											</span>
										{/if}
									</div>
									<div class="p-2">
										<p class="text-xs font-semibold text-white/90 line-clamp-2 leading-tight min-h-[2rem]">
											{item.mediaTitle ?? 'Без назви'}
										</p>
									</div>
								</a>

								{#if isOwnProfile}
									<div class="px-2 pb-2 flex justify-end">
										<button
											onclick={() => deleteItem(item)}
											disabled={deletingMap[item.mediaId]}
											class="text-[10px] text-red-400 hover:text-red-300 disabled:opacity-40 transition-colors"
											title="Видалити"
										>
											{deletingMap[item.mediaId] ? '...' : '× Видалити'}
										</button>
									</div>
								{/if}

								{#if isOwnProfile && item.mediaEpisodeCount}
									{#if group.code === 'completed'}
										<div class="px-2 pb-2 flex items-center gap-1 text-xs text-text-muted">
											<span class="text-white/60 font-semibold">{item.mediaEpisodeCount}</span>
											<span>/ {item.mediaEpisodeCount} серій</span>
										</div>
									{:else}
										<div class="px-2 pb-2 flex items-center gap-1 {savingMap[item.mediaId] ? 'opacity-60' : ''}">
											<input
												type="number"
												min="0"
												max={item.mediaEpisodeCount}
												bind:value={progressMap[item.mediaId]}
												oninput={() => debounceSave(item)}
												class="w-full px-1.5 py-1 text-xs rounded bg-gray-800 border border-gray-700
												       text-white/80 focus:outline-none focus:border-brand-accent"
												placeholder="Серія"
											/>
											<span class="text-text-muted text-xs shrink-0">/{item.mediaEpisodeCount}</span>
										</div>
									{/if}
								{/if}
							</div>
						{/each}
					</div>
				</section>
			{/if}
		{/each}
	</div>
{/if}
