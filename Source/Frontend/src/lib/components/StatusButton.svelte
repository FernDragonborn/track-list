<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { api } from '$lib/api';
	import { TRACKING_STATUS_LABELS } from '$lib/constants';
	import type { TrackingStatusCode } from '$lib/types/trackingTypes';

	interface Props {
		mediaId: string;
		token: string | null;
		mediaType?: string;
		episodeCount?: number;
		seasonCount?: number;
		onStatusChange?: (next: TrackingStatusCode | null, prev: TrackingStatusCode | null) => void;
	}

	let { mediaId, token, mediaType, episodeCount, seasonCount, onStatusChange }: Props = $props();

	const STATUS_LABELS = TRACKING_STATUS_LABELS;

	const isSeries = $derived(mediaType?.toLowerCase() === 'series');

	const STATUS_OPTIONS = $derived<{ code: TrackingStatusCode; label: string }[]>([
		...(isSeries ? [{ code: 'watching' as TrackingStatusCode, label: 'Дивлюся' }] : []),
		{ code: 'planned', label: 'У планах' },
		{ code: 'completed', label: 'Переглянуто' },
		{ code: 'dropped', label: 'Кинуто' },
	]);

	let currentStatus = $state<TrackingStatusCode | null>(null);
	let currentProgress = $state<number>(0);
	let isMenuOpen = $state(false);
	let loading = $state(false);
	let episodeInput = $derived(String(currentProgress));
	let saveTimer: ReturnType<typeof setTimeout> | null = null;

	$effect(() => {
		const id = mediaId;
		const tok = token;
		currentStatus = null;
		currentProgress = 0;
		isMenuOpen = false;
		if (!tok) return;
		let cancelled = false;
		api.getMyTrackingStatus(id, tok)
			.then((result) => {
				if (!cancelled && result) {
					currentStatus = result.status;
					currentProgress = result.progress ?? 0;
				}
			})
			.catch(() => {});
		return () => {
			cancelled = true;
		};
	});

	// Average episodes per season (approximate, for derived season display)
	const avgEpsPerSeason = $derived(
		episodeCount && seasonCount && seasonCount > 1
			? Math.round(episodeCount / seasonCount)
			: null,
	);

	const displaySeason = $derived(
		avgEpsPerSeason && currentProgress > 0
			? Math.floor((currentProgress - 1) / avgEpsPerSeason) + 1
			: avgEpsPerSeason
			  ? 1
			  : null,
	);

	const showProgress = $derived(isSeries && (currentStatus === 'watching' || currentStatus === 'dropped') && !!token);

	const buttonLabel = $derived(currentStatus ? STATUS_LABELS[currentStatus] : 'Відмітити статус');

	function clickOutside(node: HTMLElement) {
		function handle(e: MouseEvent) {
			if (!node.contains(e.target as Node)) isMenuOpen = false;
		}
		document.addEventListener('mousedown', handle);
		return {
			destroy() {
				document.removeEventListener('mousedown', handle);
			},
		};
	}

	function handleButtonClick() {
		if (!token) {
			goto(resolve('/auth/login'));
			return;
		}
		isMenuOpen = !isMenuOpen;
	}

	async function selectStatus(code: TrackingStatusCode) {
		if (code === currentStatus) {
			isMenuOpen = false;
			return;
		}
		if (!token) {
			goto(resolve('/auth/login'));
			return;
		}
		loading = true;
		try {
			await api.upsertTrackingStatus(
				{ mediaId, status: code, progress: currentProgress || undefined },
				token,
			);
			const prev = currentStatus;
			currentStatus = code;
			onStatusChange?.(code, prev);
		} catch {
			// silent — user can retry
		} finally {
			loading = false;
			isMenuOpen = false;
		}
	}

	async function removeStatus() {
		if (!token || !currentStatus) {
			isMenuOpen = false;
			return;
		}
		loading = true;
		try {
			await api.deleteTrackingStatus(mediaId, token);
			const prev = currentStatus;
			currentStatus = null;
			currentProgress = 0;
			onStatusChange?.(null, prev);
		} catch {
			// silent
		} finally {
			loading = false;
			isMenuOpen = false;
		}
	}

	async function saveProgress(newProgress: number, autoStatus?: TrackingStatusCode) {
		if (!token) return;
		const statusToSave = autoStatus ?? currentStatus ?? 'watching';
		// Optimistic update — no UI dim/flash
		currentProgress = newProgress;
		if (autoStatus && autoStatus !== currentStatus) {
			const prev = currentStatus;
			currentStatus = autoStatus;
			onStatusChange?.(autoStatus, prev);
		}
		try {
			await api.upsertTrackingStatus({ mediaId, status: statusToSave, progress: newProgress }, token);
		} catch {
			// silent — keep optimistic value
		}
	}

	function scheduleSave(value: number) {
		if (saveTimer) clearTimeout(saveTimer);
		saveTimer = setTimeout(() => {
			const max = episodeCount ?? Infinity;
			const clamped = Math.min(Math.max(0, value), max);
			if (episodeCount && clamped >= episodeCount && currentStatus !== 'dropped') {
				saveProgress(episodeCount, 'completed');
			} else {
				saveProgress(clamped);
			}
		}, 400);
	}

	function onEpisodeInput(e: Event) {
		const v = (e.target as HTMLInputElement).value;
		episodeInput = v;
		const parsed = parseInt(v, 10);
		if (!isNaN(parsed)) scheduleSave(parsed);
	}

	function onEpisodeBlur() {
		const parsed = parseInt(episodeInput, 10);
		if (isNaN(parsed)) {
			episodeInput = String(currentProgress);
		}
	}

	async function adjustEpisode(delta: number) {
		const max = episodeCount ?? Infinity;
		const next = Math.min(Math.max(0, currentProgress + delta), max);
		if (next === currentProgress) return;
		if (episodeCount && next >= episodeCount && currentStatus !== 'dropped') {
			await saveProgress(episodeCount, 'completed');
		} else {
			await saveProgress(next);
		}
	}

	async function adjustSeason(delta: number) {
		if (!avgEpsPerSeason || !seasonCount) return;
		const curSeason = displaySeason ?? 1;
		const nextSeason = Math.min(Math.max(1, curSeason + delta), seasonCount);
		if (nextSeason === curSeason) return;
		const newProgress = Math.min((nextSeason - 1) * avgEpsPerSeason + 1, episodeCount ?? Infinity);
		if (episodeCount && newProgress >= episodeCount && currentStatus !== 'dropped') {
			await saveProgress(episodeCount, 'completed');
		} else {
			await saveProgress(newProgress);
		}
	}
</script>

<div class="relative w-full" use:clickOutside>
	<button
		onclick={handleButtonClick}
		disabled={loading}
		class="w-full py-2 rounded-lg bg-bkg-header border text-xs font-bold transition-all
		       active:scale-95 disabled:opacity-50
		       {currentStatus
			       ? 'border-brand-accent/60 text-brand-accent hover:bg-brand-accent/10'
			       : 'border-gray-700 text-text-muted hover:bg-white/10 hover:text-white/95'}"
	>
		{buttonLabel}
	</button>

	{#if isMenuOpen}
		<div
			class="absolute top-full left-0 right-0 mt-1 bg-bkg-header border border-gray-700
			       rounded-lg overflow-hidden z-50 shadow-xl"
		>
			{#each STATUS_OPTIONS as opt (opt.code)}
				<button
					onclick={() => selectStatus(opt.code)}
					class="w-full px-3 py-2 text-left text-xs transition-colors
					       {currentStatus === opt.code
						       ? 'bg-brand-accent/20 text-brand-accent font-bold'
						       : 'text-white/80 hover:bg-white/10'}"
				>
					{opt.label}
				</button>
			{/each}
			{#if currentStatus}
				<hr class="border-gray-700/60" />
				<button
					onclick={removeStatus}
					class="w-full px-3 py-2 text-left text-xs text-red-400 hover:bg-red-500/10 transition-colors"
				>
					Видалити
				</button>
			{/if}
		</div>
	{/if}

	<!-- Episode / season progress panel (series + watching) -->
	{#if showProgress && (episodeCount || seasonCount)}
		<div class="mt-2 rounded-lg border border-gray-700 bg-bkg-main p-2.5">
			<div class="flex items-center justify-center gap-4">
				<!-- Season picker (only when multiple seasons) -->
				{#if avgEpsPerSeason && seasonCount && seasonCount > 1}
					<div class="flex flex-col items-center gap-1">
						<span class="text-[9px] uppercase tracking-wider text-text-muted">Сезон</span>
						<div class="flex items-center gap-1">
							<button
								onclick={() => adjustSeason(-1)}
								disabled={!displaySeason || displaySeason <= 1}
								class="w-6 h-6 rounded bg-gray-800 hover:bg-gray-700 text-white/80 font-bold
								       disabled:opacity-30 disabled:cursor-not-allowed transition-colors leading-none"
							>−</button>
							<span class="w-6 text-center text-sm font-black text-white/95 tabular-nums">
								{displaySeason ?? 1}
							</span>
							<button
								onclick={() => adjustSeason(1)}
								disabled={!!displaySeason && displaySeason >= (seasonCount ?? 1)}
								class="w-6 h-6 rounded bg-gray-800 hover:bg-gray-700 text-white/80 font-bold
								       disabled:opacity-30 disabled:cursor-not-allowed transition-colors leading-none"
							>+</button>
						</div>
						<span class="text-[9px] text-text-muted">/ {seasonCount}</span>
					</div>

					<div class="w-px h-10 bg-gray-700"></div>
				{/if}

				<!-- Episode picker -->
				{#if episodeCount}
					<div class="flex flex-col items-center gap-1">
						<span class="text-[9px] uppercase tracking-wider text-text-muted">Серія</span>
						<div class="flex items-center gap-1">
							<button
								onclick={() => adjustEpisode(-1)}
								disabled={currentProgress <= 0}
								class="w-6 h-6 rounded bg-gray-800 hover:bg-gray-700 text-white/80 font-bold
								       disabled:opacity-30 disabled:cursor-not-allowed transition-colors leading-none"
							>−</button>
							<input
								type="number"
								min="0"
								max={episodeCount}
								value={episodeInput}
								oninput={onEpisodeInput}
								onblur={onEpisodeBlur}
								class="w-12 text-center text-sm font-black text-white/95 tabular-nums bg-transparent border-0 outline-none focus:bg-gray-800 rounded px-1 [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none [appearance:textfield]"
							/>
							<button
								onclick={() => adjustEpisode(1)}
								disabled={currentProgress >= episodeCount}
								class="w-6 h-6 rounded bg-gray-800 hover:bg-gray-700 text-white/80 font-bold
								       disabled:opacity-30 disabled:cursor-not-allowed transition-colors leading-none"
							>+</button>
						</div>
						<span class="text-[9px] text-text-muted">/ {episodeCount}</span>
					</div>
				{/if}
			</div>
		</div>
	{/if}
</div>
