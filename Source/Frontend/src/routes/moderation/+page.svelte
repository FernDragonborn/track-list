<script lang="ts">
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import { REPORT_REASON_LABELS, TARGET_TYPE_LABELS } from '$lib/constants';
	import { formatDate } from '$lib/utils/format';
	import type { ReportItem, PendingTranslationItem } from '$lib/types/moderationTypes';

	function deleteLabel(report: ReportItem): string {
		const t = (report.targetType as string).toLowerCase();
		if (t === 'profile') return 'Видалити профіль';
		return 'Видалити контент';
	}

	function targetUrl(report: ReportItem): string | null {
		const nav = report.targetNavigation;
		if (!nav) return null;
		const t = (report.targetType as string).toLowerCase();
		if (t === 'profile' && nav.username) return `/profile/${nav.username}`;
		if (t === 'review' && nav.mediaId && nav.reviewId)
			return `/media/${nav.mediaId}#review-${nav.reviewId}`;
		if (t === 'comment' && nav.mediaId && nav.reviewId)
			return `/media/${nav.mediaId}?review=${nav.reviewId}${nav.commentId ? `&comment=${nav.commentId}` : ''}`;
		return null;
	}

	let { data } = $props<{
		data: { reports: ReportItem[]; translations: PendingTranslationItem[]; token: string; isAdmin: boolean };
	}>();

	let activeTab = $state<'reports' | 'translations'>('reports');
	let reports = $state<ReportItem[]>(untrack(() => data.reports));
	let translations = $state<PendingTranslationItem[]>(untrack(() => data.translations));
	let busyReports = $state<Record<string, boolean>>({});
	let busyTranslations = $state<Record<string, boolean>>({});
	let translationErrors = $state<Record<string, string>>({});

	const REASON_LABELS = REPORT_REASON_LABELS;
	const TARGET_LABELS = TARGET_TYPE_LABELS;

	async function resolveReport(id: string, resolution: 'ResolvedDeleted' | 'ResolvedDismissed') {
		if (busyReports[id]) return;
		busyReports = { ...busyReports, [id]: true };
		try {
			await api.resolveReport(id, resolution, data.token);
			reports = reports.filter((r) => r.id !== id);
		} catch {
			// leave in list on error
		} finally {
			busyReports = { ...busyReports, [id]: false };
		}
	}

	async function updateTranslation(id: string, status: 'Approved' | 'Rejected') {
		if (busyTranslations[id]) return;
		busyTranslations = { ...busyTranslations, [id]: true };
		translationErrors = { ...translationErrors, [id]: '' };
		try {
			await api.updateTranslationStatus(id, status, data.token);
			translations = translations.filter((t) => t.id !== id);
		} catch (e: unknown) {
			const msg = e instanceof Error ? e.message : String(e);
			translationErrors = { ...translationErrors, [id]: msg || 'Помилка' };
		} finally {
			busyTranslations = { ...busyTranslations, [id]: false };
		}
	}
</script>

<div class="max-w-4xl mx-auto">
	<h1 class="text-2xl font-black text-white/95 mb-6">Панель модератора</h1>

	<!-- Tabs -->
	<div class="flex gap-1 mb-6 border-b border-gray-800">
		<button
			onclick={() => (activeTab = 'reports')}
			class="px-5 py-2.5 text-sm font-semibold transition-colors border-b-2 -mb-px
			       {activeTab === 'reports'
				       ? 'border-brand-accent text-white'
				       : 'border-transparent text-text-muted hover:text-white/90'}"
		>
			Скарги
			{#if reports.length > 0}
				<span class="ml-1.5 text-xs bg-red-500/20 text-red-400 px-1.5 py-0.5 rounded-full">{reports.length}</span>
			{/if}
		</button>
		<button
			onclick={() => (activeTab = 'translations')}
			class="px-5 py-2.5 text-sm font-semibold transition-colors border-b-2 -mb-px
			       {activeTab === 'translations'
				       ? 'border-brand-accent text-white'
				       : 'border-transparent text-text-muted hover:text-white/90'}"
		>
			Переклади
			{#if translations.length > 0}
				<span class="ml-1.5 text-xs bg-yellow-500/20 text-yellow-400 px-1.5 py-0.5 rounded-full">{translations.length}</span>
			{/if}
		</button>
	</div>

	<!-- Reports tab -->
	{#if activeTab === 'reports'}
		{#if reports.length === 0}
			<div class="text-center py-16 text-text-muted text-sm">Нових скарг немає</div>
		{:else}
			<div class="flex flex-col gap-3">
				{#each reports as report (report.id)}
					{@const busy = !!busyReports[report.id]}
					{@const url = targetUrl(report)}
					<div class="bg-bkg-header border border-gray-800 rounded-xl p-5 flex flex-col gap-3">
						<!-- Header row: metadata + actions -->
						<div class="flex items-start justify-between gap-4">
							<div class="flex flex-col gap-1 min-w-0">
								<div class="flex items-center gap-2 flex-wrap">
									<span class="text-xs font-semibold text-white/90 bg-gray-700/60 px-2 py-0.5 rounded-full">
										{TARGET_LABELS[report.targetType] ?? report.targetType}
									</span>
									<span class="text-xs text-yellow-400 font-semibold">
										{REASON_LABELS[report.reason] ?? report.reason}
									</span>
									<span class="text-xs text-text-muted">
										{formatDate(report.createdAt)}
									</span>
									{#if report.targetNavigation?.authorUsername}
										<span class="text-xs text-text-muted">
											автор: <span class="text-white/70 font-medium">{report.targetNavigation.authorUsername}</span>
											{#if report.targetNavigation.isDeleted}
												<span class="text-red-400/70 text-[10px] font-normal ml-0.5">(видалено)</span>
											{/if}
										</span>
									{/if}
									{#if report.targetNavigation?.rating != null}
										<span class="text-xs text-text-muted">
											оцінка: <span class="text-white/70 font-medium">{report.targetNavigation.rating}/10</span>
										</span>
									{/if}
								</div>
								{#if report.comment}
									<p class="text-sm text-white/70 leading-relaxed mt-1 break-words">
										<span class="text-text-muted text-xs">Скарга:</span> {report.comment}
									</p>
								{/if}
							</div>

							<div class="flex gap-2 shrink-0">
								{#if (report.targetType as string).toLowerCase() !== 'profile' || data.isAdmin}
									<button
										onclick={() => resolveReport(report.id, 'ResolvedDeleted')}
										disabled={busy}
										class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-red-500/15 text-red-400
										       hover:bg-red-500/25 transition-colors disabled:opacity-40"
									>{busy ? '...' : deleteLabel(report)}</button>
								{/if}
								<button
									onclick={() => resolveReport(report.id, 'ResolvedDismissed')}
									disabled={busy}
									class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-gray-700/60 text-text-muted
									       hover:bg-gray-700 transition-colors disabled:opacity-40"
								>{busy ? '...' : 'Відхилити'}</button>
							</div>
						</div>

						<!-- Reported content preview -->
						{#if report.targetNavigation?.contentExcerpt || report.targetNavigation?.bio || report.targetNavigation?.displayName}
							<div class="bg-black/25 border border-gray-700/50 rounded-lg p-3 text-sm text-white/70 leading-relaxed break-words">
								{#if report.targetNavigation.displayName}
									<span class="font-semibold text-white/85">{report.targetNavigation.displayName}</span>
									{#if report.targetNavigation.bio}
										<span class="mx-1 text-text-muted">·</span>
									{/if}
								{/if}
								{report.targetNavigation.contentExcerpt ?? report.targetNavigation.bio ?? ''}
							</div>
						{/if}

						<!-- Link to view in context -->
						{#if url}
							<a
								href={resolve(url as '/')}
								target="_blank"
								rel="noopener noreferrer"
								class="self-start inline-flex items-center gap-1.5 text-sm font-semibold text-brand-accent
								       bg-brand-accent/10 hover:bg-brand-accent/20 transition-colors
								       px-3 py-1.5 rounded-lg border border-brand-accent/30"
							>
								Перейти до місця публікації →
							</a>
						{:else if report.targetNavigation?.isDeleted}
							<span class="text-xs text-red-400/70 font-semibold">Контент видалено</span>
						{:else}
							<p class="text-[11px] text-text-muted font-mono truncate">ID: {report.targetId}</p>
						{/if}
					</div>
				{/each}
			</div>
		{/if}
	{/if}

	<!-- Translations tab -->
	{#if activeTab === 'translations'}
		{#if translations.length === 0}
			<div class="text-center py-16 text-text-muted text-sm">Нових перекладів на розгляді немає</div>
		{:else}
			<div class="overflow-x-auto rounded-xl border border-gray-800">
				<table class="w-full text-sm">
					<thead>
						<tr class="border-b border-gray-800 bg-gray-900/60">
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide w-16">Мова</th>
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide">Оригінал</th>
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide">Пропозиція</th>
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide w-24">Дата</th>
							<th class="text-right px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide w-36">Дії</th>
						</tr>
					</thead>
					<tbody>
						{#each translations as t (t.id)}
							{@const busy = !!busyTranslations[t.id]}
							{@const rowError = translationErrors[t.id] ?? ''}
							<tr class="border-b border-gray-800/60 last:border-0 hover:bg-white/[0.02] transition-colors align-top">
								<!-- Language -->
								<td class="px-4 py-4">
									<span class="text-xs font-bold bg-blue-500/15 text-blue-400 px-2 py-1 rounded-full uppercase">
										{t.languageCode}
									</span>
								</td>

								<!-- Original -->
								<td class="px-4 py-4 max-w-[220px]">
									{#if t.originalTitle}
										<p class="text-sm font-semibold text-white/80 leading-snug line-clamp-2">{t.originalTitle}</p>
										{#if t.originalDescription}
											<p class="text-xs text-text-muted leading-relaxed line-clamp-3 mt-1">{t.originalDescription}</p>
										{/if}
									{:else}
										<span class="text-xs text-text-muted italic">Оригіналу немає</span>
									{/if}
									<a
										href={resolve(`/media/${t.mediaId}`)}
										class="inline-block mt-1.5 text-[11px] text-brand-accent hover:underline"
									>Відкрити медіа →</a>
								</td>

								<!-- Proposal -->
								<td class="px-4 py-4 max-w-[220px]">
									<p class="text-sm font-semibold text-white/95 leading-snug line-clamp-2">{t.title}</p>
									{#if t.description}
										<p class="text-xs text-white/60 leading-relaxed line-clamp-3 mt-1">{t.description}</p>
									{/if}
								</td>

								<!-- Date -->
								<td class="px-4 py-4 text-xs text-text-muted whitespace-nowrap">
									{formatDate(t.createdAt)}
								</td>

								<!-- Actions -->
								<td class="px-4 py-4">
									<div class="flex flex-col items-end gap-1.5">
										<div class="flex gap-2">
											<button
												onclick={() => updateTranslation(t.id, 'Approved')}
												disabled={busy}
												class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-green-500/15 text-green-400
												       hover:bg-green-500/25 transition-colors disabled:opacity-40"
											>{busy ? '...' : 'Схвалити'}</button>
											<button
												onclick={() => updateTranslation(t.id, 'Rejected')}
												disabled={busy}
												class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-gray-700/60 text-text-muted
												       hover:bg-gray-700 transition-colors disabled:opacity-40"
											>{busy ? '...' : 'Відхилити'}</button>
										</div>
										{#if rowError}
											<p class="text-[11px] text-red-400 text-right max-w-[140px] leading-tight">{rowError}</p>
										{/if}
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		{/if}
	{/if}
</div>
