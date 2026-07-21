<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { SvelteSet } from 'svelte/reactivity';
	import { Chart, ArcElement, DoughnutController, Tooltip, Legend } from 'chart.js';
	import { api } from '$lib/api';
	import { env } from '$env/dynamic/public';
	import type { AdminUserItem, AdminMediaItem, PlatformStatsDto } from '$lib/types/adminTypes';
	import type { PageData } from './$types';

	Chart.register(ArcElement, DoughnutController, Tooltip, Legend);

	const ROLE_LABELS: Record<string, string> = {
		admin: 'Адмін',
		moderator: 'Модератор',
		user: 'Користувач',
	};

	const ROLE_COLORS: Record<string, string> = {
		admin: 'bg-red-500/20 text-red-400',
		moderator: 'bg-blue-500/20 text-blue-400',
		user: 'bg-gray-700/60 text-text-muted',
	};

	const MEDIA_TYPE_LABELS: Record<string, string> = {
		Movie: 'Фільм',
		Series: 'Серіал',
		Game: 'Гра',
		Book: 'Книга',
		Other: 'Інше',
	};

	const STAT_CARDS = [
		{ key: 'totalUsers',          label: 'Користувачів',        icon: '👤' },
		{ key: 'totalMedia',          label: 'Медіа',               icon: '🎬' },
		{ key: 'totalReviews',        label: 'Рецензій',            icon: '✍️' },
		{ key: 'totalComments',       label: 'Коментарів',          icon: '💬' },
		{ key: 'totalCollections',    label: 'Колекцій',            icon: '📚' },
		{ key: 'totalReports',        label: 'Скарг (всього)',      icon: '🚨' },
		{ key: 'pendingReports',      label: 'Скарг в черзі',       icon: '⏳' },
		{ key: 'pendingTranslations', label: 'Перекладів в черзі',  icon: '🌐' },
	] as const;

	let { data } = $props<{ data: PageData }>();

	let activeTab = $state<'users' | 'statistics' | 'media'>('users');

	// ── Users tab ────────────────────────────────────────────────────────────────
	let users = $state<AdminUserItem[]>(untrack(() => data.initialUsers ?? []));
	let totalCount = $state(untrack(() => data.initialTotalCount ?? 0));
	let searchInput = $state('');
	let currentPage = $state(1);
	const PAGE_SIZE = 20;
	let loadingUsers = $state(false);
	let usersError = $state<string | null>(null);
	let sortBy = $state<'username' | 'role' | 'createdAt'>('createdAt');
	let sortDir = $state<'asc' | 'desc'>('desc');
	let pendingRoles = $state<Record<string, string>>({});
	let busyUsers = $state<Record<string, boolean>>({});

	// ── Statistics tab ────────────────────────────────────────────────────────────
	function toInputDate(d: Date) { return d.toISOString().slice(0, 10); }
	let statsFromStr = $state(toInputDate(new Date(Date.now() - 30 * 24 * 60 * 60 * 1000)));
	let statsToStr   = $state(toInputDate(new Date()));
	let stats = $state<PlatformStatsDto | null>(untrack(() => data.initialStats ?? null));
	let loadingStats = $state(false);
	let statsError = $state<string | null>(null);
	let exportBusy = $state(false);

	let trackingCanvas = $state<HTMLCanvasElement | null>(null);
	let trackingChart: Chart<'doughnut'> | null = null;

	$effect(() => {
		const canvas = trackingCanvas;
		const dist = stats?.trackingDistribution;
		if (!canvas || !dist) return;

		const values = [dist.planned, dist.watching, dist.completed, dist.dropped];

		if (trackingChart) {
			trackingChart.data.datasets[0].data = values;
			trackingChart.update('none');
			return () => {};
		}

		trackingChart = new Chart(canvas, {
			type: 'doughnut',
			data: {
				labels: ['Планую', 'Дивлюсь', 'Завершено', 'Кинув'],
				datasets: [{
					data: values,
					backgroundColor: ['#f97316', '#3b82f6', '#22c55e', '#ef4444'],
					borderWidth: 0,
				}],
			},
			options: {
				responsive: true,
				maintainAspectRatio: false,
				plugins: {
					legend: {
						position: 'bottom',
						labels: { color: '#9ca3af', font: { size: 11 }, padding: 12 },
					},
				},
			},
		});

		return () => { trackingChart?.destroy(); trackingChart = null; };
	});

	// ── Media tab ─────────────────────────────────────────────────────────────────
	const MEDIA_PAGE_SIZE = 20;
	let mediaItems = $state<AdminMediaItem[]>([]);
	let mediaTotalCount = $state(0);
	let mediaPage = $state(1);
	let mediaSearch = $state('');
	let loadingMedia = $state(false);
	let mediaError = $state<string | null>(null);
	let expandedMedia = new SvelteSet<string>();
	let pendingTranslations = $state<Record<string, { title: string; description: string }>>({});
	let busyTranslations = $state<Record<string, boolean>>({});
	let busyMediaItems = $state<Record<string, boolean>>({});

	// ── Lifecycle ─────────────────────────────────────────────────────────────────
	onMount(() => {
		if (users.length === 0) loadUsers(1, '');
		if (!stats) refreshStats();
	});

	// ── Users functions ───────────────────────────────────────────────────────────
	async function loadUsers(
		page: number,
		search: string,
		by: 'username' | 'role' | 'createdAt' = sortBy,
		dir: 'asc' | 'desc' = sortDir,
	) {
		loadingUsers = true;
		usersError = null;
		try {
			const result = await api.getAdminUsers(
				data.token, page, PAGE_SIZE,
				search || undefined,
				by,
				dir === 'desc',
			);
			users = result.items ?? [];
			totalCount = result.totalCount ?? 0;
			currentPage = page;
			pendingRoles = {};
		} catch (e: unknown) {
			usersError = (e as { message?: string })?.message ?? String(e);
		} finally { loadingUsers = false; }
	}

	function toggleSort(col: 'username' | 'role' | 'createdAt') {
		if (sortBy === col) {
			sortDir = sortDir === 'asc' ? 'desc' : 'asc';
		} else {
			sortBy = col;
			sortDir = col === 'createdAt' ? 'desc' : 'asc';
		}
		loadUsers(1, searchInput, sortBy, sortDir);
	}

	function sortIcon(col: string) {
		if (sortBy !== col) return '↕';
		return sortDir === 'asc' ? '↑' : '↓';
	}

	let searchTimer: ReturnType<typeof setTimeout>;
	function onSearchInput() {
		clearTimeout(searchTimer);
		searchTimer = setTimeout(() => loadUsers(1, searchInput, sortBy, sortDir), 350);
	}

	function setPendingRole(userId: string, role: string) {
		pendingRoles = { ...pendingRoles, [userId]: role };
	}

	function clearPendingRole(userId: string) {
		const next = { ...pendingRoles };
		delete next[userId];
		pendingRoles = next;
	}

	async function saveRole(user: AdminUserItem) {
		const newRole = pendingRoles[user.id];
		if (newRole === undefined || busyUsers[user.id]) return;
		busyUsers = { ...busyUsers, [user.id]: true };
		try {
			await api.updateUserRole(user.username, newRole, data.token);
			users = users.map((u) => u.id === user.id ? { ...u, role: newRole } : u);
			clearPendingRole(user.id);
		} catch { /* leave pending on error */ }
		finally { busyUsers = { ...busyUsers, [user.id]: false }; }
	}

	async function deleteUser(user: AdminUserItem) {
		if (busyUsers[user.id]) return;
		if (!confirm(`Видалити акаунт "${user.username}"? Цю дію не можна скасувати.`)) return;
		busyUsers = { ...busyUsers, [user.id]: true };
		try {
			await api.deleteAdminUser(user.username, data.token);
			users = users.filter((u) => u.id !== user.id);
			totalCount = Math.max(0, totalCount - 1);
		} catch { /* keep on error */ }
		finally { busyUsers = { ...busyUsers, [user.id]: false }; }
	}

	// ── Stats functions ───────────────────────────────────────────────────────────
	async function refreshStats() {
		loadingStats = true;
		statsError = null;
		try {
			stats = await api.getAdminStats(
				data.token,
				new Date(statsFromStr),
				new Date(statsToStr + 'T23:59:59Z'),
			);
		} catch (e: unknown) {
			statsError = (e as { message?: string })?.message ?? String(e);
		} finally { loadingStats = false; }
	}

	async function downloadCsv() {
		if (exportBusy) return;
		exportBusy = true;
		try {
			const base = (env.PUBLIC_API_URL || '/api').replace(/\/$/, '');
			const res = await fetch(`${base}/admin/export/users.csv`, {
				headers: { Authorization: `Bearer ${data.token}` },
			});
			if (!res.ok) return;
			const blob = await res.blob();
			const url = URL.createObjectURL(blob);
			const a = document.createElement('a');
			a.href = url;
			a.download = 'users_export.csv';
			a.click();
			URL.revokeObjectURL(url);
		} catch { /* ignore */ }
		finally { exportBusy = false; }
	}

	// ── Media functions ───────────────────────────────────────────────────────────
	let mediaSearchTimer: ReturnType<typeof setTimeout>;

	async function loadMedia(page: number, search: string) {
		loadingMedia = true;
		mediaError = null;
		try {
			const result = await api.getAdminMedia(data.token, page, MEDIA_PAGE_SIZE, search || undefined);
			mediaItems = result.items ?? [];
			mediaTotalCount = result.totalCount ?? 0;
			mediaPage = page;
		} catch (e: unknown) {
			mediaError = (e as { message?: string })?.message ?? String(e);
		} finally { loadingMedia = false; }
	}

	function onMediaSearchInput() {
		clearTimeout(mediaSearchTimer);
		mediaSearchTimer = setTimeout(() => loadMedia(1, mediaSearch), 350);
	}

	function toggleExpand(id: string) {
		if (expandedMedia.has(id)) {
			expandedMedia.delete(id);
		} else {
			expandedMedia.add(id);
			const item = mediaItems.find((m) => m.id === id);
			item?.translations.forEach((t) => {
				if (!(t.id in pendingTranslations)) {
					pendingTranslations = {
						...pendingTranslations,
						[t.id]: { title: t.title ?? '', description: t.description ?? '' },
					};
				}
			});
		}
	}

	async function saveTranslation(translationId: string) {
		const pending = pendingTranslations[translationId];
		if (!pending || busyTranslations[translationId]) return;
		busyTranslations = { ...busyTranslations, [translationId]: true };
		try {
			await api.updateAdminTranslation(translationId, { title: pending.title, description: pending.description }, data.token);
			mediaItems = mediaItems.map((m) => ({
				...m,
				translations: m.translations.map((t) =>
					t.id === translationId ? { ...t, title: pending.title, description: pending.description } : t,
				),
			}));
		} catch { /* keep pending */ }
		finally { busyTranslations = { ...busyTranslations, [translationId]: false }; }
	}

	async function deleteMediaItem(m: AdminMediaItem) {
		if (busyMediaItems[m.id]) return;
		const label = m.translations.find((t) => t.title)?.title ?? m.externalApiId ?? m.id;
		if (!confirm(`Видалити медіа "${label}"? Цю дію не можна скасувати.`)) return;
		busyMediaItems = { ...busyMediaItems, [m.id]: true };
		try {
			await api.deleteAdminMedia(m.id, data.token);
			mediaItems = mediaItems.filter((item) => item.id !== m.id);
			mediaTotalCount = Math.max(0, mediaTotalCount - 1);
		} catch { /* keep on error */ }
		finally { busyMediaItems = { ...busyMediaItems, [m.id]: false }; }
	}

	// ── Derived ───────────────────────────────────────────────────────────────────
	const totalPages = $derived(Math.ceil(totalCount / PAGE_SIZE));
	const mediaTotalPages = $derived(Math.ceil(mediaTotalCount / MEDIA_PAGE_SIZE));
</script>

<div class="max-w-4xl mx-auto">
	<h1 class="text-2xl font-black text-white/95 mb-6">Панель адміністратора</h1>

	<!-- Tabs -->
	<div class="flex gap-1 mb-6 border-b border-gray-800">
		<button
			onclick={() => (activeTab = 'users')}
			class="px-5 py-2.5 text-sm font-semibold transition-colors border-b-2 -mb-px
			       {activeTab === 'users'
				       ? 'border-brand-accent text-white'
				       : 'border-transparent text-text-muted hover:text-white/90'}"
		>
			Користувачі
			{#if totalCount > 0}
				<span class="ml-1.5 text-xs bg-gray-700/60 text-text-muted px-1.5 py-0.5 rounded-full">{totalCount}</span>
			{/if}
		</button>
		<button
			onclick={() => { activeTab = 'media'; if (mediaItems.length === 0 && !loadingMedia) loadMedia(1, ''); }}
			class="px-5 py-2.5 text-sm font-semibold transition-colors border-b-2 -mb-px
			       {activeTab === 'media'
				       ? 'border-brand-accent text-white'
				       : 'border-transparent text-text-muted hover:text-white/90'}"
		>
			Медіа
			{#if mediaTotalCount > 0}
				<span class="ml-1.5 text-xs bg-gray-700/60 text-text-muted px-1.5 py-0.5 rounded-full">{mediaTotalCount}</span>
			{/if}
		</button>
		<button
			onclick={() => (activeTab = 'statistics')}
			class="px-5 py-2.5 text-sm font-semibold transition-colors border-b-2 -mb-px
			       {activeTab === 'statistics'
				       ? 'border-brand-accent text-white'
				       : 'border-transparent text-text-muted hover:text-white/90'}"
		>
			Статистика
		</button>
	</div>

	<!-- ── Users tab ──────────────────────────────────────────────────────────────── -->
	{#if activeTab === 'users'}
		<div class="mb-4">
			<input
				type="text"
				placeholder="Пошук за ім'ям або email..."
				bind:value={searchInput}
				oninput={onSearchInput}
				class="w-full bg-bkg-header border border-gray-700 rounded-lg px-4 py-2.5 text-sm text-white/90
				       placeholder:text-text-muted focus:outline-none focus:border-brand-accent/60 transition-colors"
			/>
		</div>

		{#if loadingUsers}
			<div class="text-center py-16 text-text-muted text-sm">Завантаження...</div>
		{:else if usersError}
			<div class="text-center py-16 text-red-400 text-sm font-mono">Помилка: {usersError}</div>
		{:else if users.length === 0}
			<div class="text-center py-16 text-text-muted text-sm">Користувачів не знайдено</div>
		{:else}
			<div class="overflow-x-auto rounded-xl border border-gray-800">
				<table class="w-full text-sm">
					<thead>
						<tr class="border-b border-gray-800 bg-gray-900/60">
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide w-8">#</th>
							<th class="px-4 py-3 text-left">
								<button
									onclick={() => toggleSort('username')}
									class="text-xs font-semibold text-text-muted uppercase tracking-wide hover:text-white/80 transition-colors flex items-center gap-1"
								>Користувач <span class="opacity-60">{sortIcon('username')}</span></button>
							</th>
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide">Email</th>
							<th class="px-4 py-3 text-left">
								<button
									onclick={() => toggleSort('role')}
									class="text-xs font-semibold text-text-muted uppercase tracking-wide hover:text-white/80 transition-colors flex items-center gap-1"
								>Роль <span class="opacity-60">{sortIcon('role')}</span></button>
							</th>
							<th class="px-4 py-3 text-left">
								<button
									onclick={() => toggleSort('createdAt')}
									class="text-xs font-semibold text-text-muted uppercase tracking-wide hover:text-white/80 transition-colors flex items-center gap-1"
								>Рік <span class="opacity-60">{sortIcon('createdAt')}</span></button>
							</th>
							<th class="text-right px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide">Дії</th>
						</tr>
					</thead>
					<tbody>
						{#each users as user, i (user.id)}
							{@const busy = !!busyUsers[user.id]}
							{@const pendingRole = pendingRoles[user.id]}
							{@const hasPending = pendingRole !== undefined && pendingRole !== user.role}
							<tr class="border-b border-gray-800/60 last:border-0 hover:bg-white/[0.02] transition-colors">
								<td class="px-4 py-3 text-xs text-text-muted">
									{(currentPage - 1) * PAGE_SIZE + i + 1}
								</td>
								<td class="px-4 py-3">
									<div class="font-semibold text-white/90">{user.username}</div>
									{#if user.displayName && user.displayName !== user.username}
										<div class="text-xs text-text-muted mt-0.5">{user.displayName}</div>
									{/if}
								</td>
								<td class="px-4 py-3 text-xs text-text-muted">{user.email}</td>
								<td class="px-4 py-3">
									<span class="text-xs font-semibold px-2 py-0.5 rounded-full {ROLE_COLORS[user.role] ?? ROLE_COLORS['user']}">
										{ROLE_LABELS[user.role] ?? user.role}
									</span>
								</td>
								<td class="px-4 py-3 text-xs text-text-muted">{user.memberSinceYear ?? '—'}</td>
								<td class="px-4 py-3">
									<div class="flex items-center justify-end gap-2">
										<select
											value={pendingRole ?? user.role}
											onchange={(e) => setPendingRole(user.id, (e.target as HTMLSelectElement).value)}
											disabled={busy}
											class="text-xs bg-gray-800 border border-gray-700 text-white/80 rounded-lg px-2 py-1.5
											       focus:outline-none focus:border-brand-accent/60 disabled:opacity-40 cursor-pointer"
										>
											<option value="admin">Адмін</option>
											<option value="moderator">Модератор</option>
											<option value="user">Користувач</option>
										</select>

										{#if hasPending}
											<button
												onclick={() => saveRole(user)}
												disabled={busy}
												class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-brand-accent/20 text-brand-accent
												       hover:bg-brand-accent/30 transition-colors disabled:opacity-40"
											>{busy ? '...' : 'Зберегти'}</button>
											<button
												onclick={() => clearPendingRole(user.id)}
												disabled={busy}
												class="text-xs px-2 py-1.5 rounded-lg bg-gray-700/60 text-text-muted
												       hover:bg-gray-700 transition-colors disabled:opacity-40"
											>✕</button>
										{/if}

										<button
											onclick={() => deleteUser(user)}
											disabled={busy}
											class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-red-500/15 text-red-400
											       hover:bg-red-500/25 transition-colors disabled:opacity-40"
										>{busy ? '...' : 'Видалити'}</button>
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>

			{#if totalPages > 1}
				<div class="flex items-center justify-between mt-4">
					<button
						onclick={() => loadUsers(currentPage - 1, searchInput)}
						disabled={currentPage <= 1 || loadingUsers}
						class="text-xs font-semibold px-4 py-2 rounded-lg bg-gray-800 text-text-muted
						       hover:bg-gray-700 disabled:opacity-30 transition-colors"
					>← Попередня</button>
					<span class="text-xs text-text-muted">Сторінка {currentPage} з {totalPages}</span>
					<button
						onclick={() => loadUsers(currentPage + 1, searchInput)}
						disabled={currentPage >= totalPages || loadingUsers}
						class="text-xs font-semibold px-4 py-2 rounded-lg bg-gray-800 text-text-muted
						       hover:bg-gray-700 disabled:opacity-30 transition-colors"
					>Наступна →</button>
				</div>
			{/if}
		{/if}
	{/if}

	<!-- ── Media tab ──────────────────────────────────────────────────────────────── -->
	{#if activeTab === 'media'}
		<div class="mb-4">
			<input
				type="text"
				placeholder="Пошук за ID або назвою перекладу..."
				bind:value={mediaSearch}
				oninput={onMediaSearchInput}
				class="w-full bg-bkg-header border border-gray-700 rounded-lg px-4 py-2.5 text-sm text-white/90
				       placeholder:text-text-muted focus:outline-none focus:border-brand-accent/60 transition-colors"
			/>
		</div>

		{#if loadingMedia}
			<div class="text-center py-16 text-text-muted text-sm">Завантаження...</div>
		{:else if mediaError}
			<div class="text-center py-16 text-red-400 text-sm font-mono">Помилка: {mediaError}</div>
		{:else if mediaItems.length === 0}
			<div class="text-center py-16 text-text-muted text-sm">Медіа не знайдено</div>
		{:else}
			<div class="rounded-xl border border-gray-800 overflow-hidden">
				<table class="w-full text-sm">
					<thead>
						<tr class="border-b border-gray-800 bg-gray-900/60">
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide">Тип</th>
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide">Назва</th>
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide">Рік</th>
							<th class="text-left px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide">Переклади</th>
							<th class="text-right px-4 py-3 text-xs font-semibold text-text-muted uppercase tracking-wide">Дії</th>
						</tr>
					</thead>
					<tbody>
						{#each mediaItems as m (m.id)}
							{@const expanded = expandedMedia.has(m.id)}
							{@const busyItem = !!busyMediaItems[m.id]}
							<tr class="border-b border-gray-800/60 hover:bg-white/[0.02] transition-colors {expanded ? 'bg-white/[0.01]' : ''}">
								<td class="px-4 py-3">
									<span class="text-xs font-semibold px-2 py-0.5 rounded-full bg-gray-700/60 text-text-muted">
										{MEDIA_TYPE_LABELS[m.type] ?? m.type}
									</span>
								</td>
								<td class="px-4 py-3">
									<div class="flex flex-col gap-0.5">
										<span class="text-sm font-medium text-white/90">
											{m.translations.find((t) => t.languageCode === 'uk')?.title
												?? m.translations.find((t) => t.languageCode === 'en')?.title
												?? m.translations.find((t) => t.title)?.title
												?? '—'}
										</span>
										<span class="text-[10px] font-mono text-text-muted">{m.externalApiId ?? m.id}</span>
									</div>
								</td>
								<td class="px-4 py-3 text-xs text-text-muted">{m.releaseYear ?? '—'}</td>
								<td class="px-4 py-3 text-xs text-text-muted">{m.translationCount}</td>
								<td class="px-4 py-3">
									<div class="flex items-center justify-end gap-2">
										<button
											onclick={() => toggleExpand(m.id)}
											class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-gray-700/60 text-text-muted
											       hover:bg-gray-700 transition-colors"
										>{expanded ? 'Сховати' : 'Переклади'}</button>
										<button
											onclick={() => deleteMediaItem(m)}
											disabled={busyItem}
											class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-red-500/15 text-red-400
											       hover:bg-red-500/25 transition-colors disabled:opacity-40"
										>{busyItem ? '...' : 'Видалити'}</button>
									</div>
								</td>
							</tr>

							{#if expanded}
								<tr class="border-b border-gray-800/60 bg-gray-900/40">
									<td colspan="5" class="px-4 py-4">
										{#if m.translations.length === 0}
											<p class="text-xs text-text-muted">Переклади відсутні</p>
										{:else}
											<div class="flex flex-col gap-4">
												{#each m.translations as t (t.id)}
													{@const pending = pendingTranslations[t.id] ?? { title: t.title ?? '', description: t.description ?? '' }}
													{@const busyT = !!busyTranslations[t.id]}
													<div class="bg-gray-800/50 rounded-lg p-3 flex flex-col gap-2">
														<div class="flex items-center gap-2 mb-1">
															<span class="text-xs font-mono font-bold text-brand-accent/80 uppercase">{t.languageCode ?? '??'}</span>
															<span class="text-xs px-1.5 py-0.5 rounded bg-gray-700/60 text-text-muted">{t.status}</span>
														</div>
														<input
															type="text"
															value={pending.title}
															oninput={(e) => {
																pendingTranslations = {
																	...pendingTranslations,
																	[t.id]: { ...pending, title: (e.target as HTMLInputElement).value },
																};
															}}
															placeholder="Назва"
															class="w-full bg-gray-900/60 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white/90
															       placeholder:text-text-muted focus:outline-none focus:border-brand-accent/60 transition-colors"
														/>
														<textarea
															value={pending.description}
															oninput={(e) => {
																pendingTranslations = {
																	...pendingTranslations,
																	[t.id]: { ...pending, description: (e.target as HTMLTextAreaElement).value },
																};
															}}
															rows={3}
															placeholder="Опис"
															class="w-full bg-gray-900/60 border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white/90
															       placeholder:text-text-muted focus:outline-none focus:border-brand-accent/60 transition-colors resize-none"
														></textarea>
														<div class="flex justify-end">
															<button
																onclick={() => saveTranslation(t.id)}
																disabled={busyT}
																class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-brand-accent/20 text-brand-accent
																       hover:bg-brand-accent/30 transition-colors disabled:opacity-40"
															>{busyT ? '...' : 'Зберегти'}</button>
														</div>
													</div>
												{/each}
											</div>
										{/if}
									</td>
								</tr>
							{/if}
						{/each}
					</tbody>
				</table>
			</div>

			{#if mediaTotalPages > 1}
				<div class="flex items-center justify-between mt-4">
					<button
						onclick={() => loadMedia(mediaPage - 1, mediaSearch)}
						disabled={mediaPage <= 1 || loadingMedia}
						class="text-xs font-semibold px-4 py-2 rounded-lg bg-gray-800 text-text-muted
						       hover:bg-gray-700 disabled:opacity-30 transition-colors"
					>← Попередня</button>
					<span class="text-xs text-text-muted">Сторінка {mediaPage} з {mediaTotalPages}</span>
					<button
						onclick={() => loadMedia(mediaPage + 1, mediaSearch)}
						disabled={mediaPage >= mediaTotalPages || loadingMedia}
						class="text-xs font-semibold px-4 py-2 rounded-lg bg-gray-800 text-text-muted
						       hover:bg-gray-700 disabled:opacity-30 transition-colors"
					>Наступна →</button>
				</div>
			{/if}
		{/if}
	{/if}

	<!-- ── Statistics tab ─────────────────────────────────────────────────────────── -->
	{#if activeTab === 'statistics'}
		<!-- Controls -->
		<div class="flex flex-wrap items-end gap-3 mb-5">
			<div class="flex items-center gap-2">
				<label class="text-xs text-text-muted whitespace-nowrap" for="stats-from">З:</label>
				<input
					id="stats-from"
					type="date"
					bind:value={statsFromStr}
					class="bg-bkg-header border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white/90
					       focus:outline-none focus:border-brand-accent/60 transition-colors"
				/>
			</div>
			<div class="flex items-center gap-2">
				<label class="text-xs text-text-muted whitespace-nowrap" for="stats-to">По:</label>
				<input
					id="stats-to"
					type="date"
					bind:value={statsToStr}
					class="bg-bkg-header border border-gray-700 rounded-lg px-3 py-1.5 text-sm text-white/90
					       focus:outline-none focus:border-brand-accent/60 transition-colors"
				/>
			</div>
			<button
				onclick={refreshStats}
				disabled={loadingStats}
				class="text-xs font-semibold px-4 py-1.5 rounded-lg bg-brand-accent/20 text-brand-accent
				       hover:bg-brand-accent/30 transition-colors disabled:opacity-40"
			>{loadingStats ? '...' : 'Оновити'}</button>
			<div class="ml-auto flex items-center gap-2">
				{#if stats?.generatedAt}
					<span class="text-xs text-text-muted">
						Оновлено: {new Date(stats.generatedAt).toLocaleString('uk-UA')}
					</span>
				{/if}
				<button
					onclick={downloadCsv}
					disabled={exportBusy}
					class="text-xs font-semibold px-3 py-1.5 rounded-lg bg-gray-700/60 text-text-muted
					       hover:bg-gray-700 transition-colors disabled:opacity-40"
				>{exportBusy ? '...' : 'Експорт CSV'}</button>
			</div>
		</div>

		{#if loadingStats}
			<div class="text-center py-16 text-text-muted text-sm">Завантаження...</div>
		{:else if statsError}
			<div class="text-center py-16 text-red-400 text-sm font-mono">Помилка: {statsError}</div>
		{:else if !stats}
			<div class="text-center py-16 text-text-muted text-sm">Не вдалося завантажити статистику</div>
		{:else}
			<!-- Period highlight cards -->
			<div class="grid grid-cols-2 gap-3 mb-5">
				<div class="bg-indigo-500/10 border border-indigo-500/20 rounded-xl p-4 flex flex-col gap-1">
					<span class="text-lg leading-none">📈</span>
					<span class="text-2xl font-black text-indigo-400 mt-1">{stats.newUsersInPeriod}</span>
					<span class="text-xs text-text-muted">Нових користувачів за період</span>
				</div>
				<div class="bg-emerald-500/10 border border-emerald-500/20 rounded-xl p-4 flex flex-col gap-1">
					<span class="text-lg leading-none">📝</span>
					<span class="text-2xl font-black text-emerald-400 mt-1">{stats.newReviewsInPeriod}</span>
					<span class="text-xs text-text-muted">Нових рецензій за період</span>
				</div>
			</div>

			<!-- Total counters -->
			<div class="grid grid-cols-2 sm:grid-cols-4 gap-3 mb-5">
				{#each STAT_CARDS as card (card.key)}
					{@const val = stats[card.key]}
					{#if card.key === 'totalUsers'}
						<button
							onclick={() => (activeTab = 'users')}
							class="bg-bkg-header border border-gray-800 rounded-xl p-4 flex flex-col gap-1 text-left
							       hover:border-brand-accent/40 transition-colors group"
						>
							<span class="text-xl leading-none">{card.icon}</span>
							<span class="text-2xl font-black text-white/95 mt-1 group-hover:text-brand-accent transition-colors">{val ?? '—'}</span>
							<span class="text-xs text-text-muted">{card.label} <span class="text-brand-accent/50">→</span></span>
						</button>
					{:else if card.key === 'totalMedia'}
						<button
							onclick={() => { activeTab = 'media'; if (mediaItems.length === 0 && !loadingMedia) loadMedia(1, ''); }}
							class="bg-bkg-header border border-gray-800 rounded-xl p-4 flex flex-col gap-1 text-left
							       hover:border-brand-accent/40 transition-colors group"
						>
							<span class="text-xl leading-none">{card.icon}</span>
							<span class="text-2xl font-black text-white/95 mt-1 group-hover:text-brand-accent transition-colors">{val ?? '—'}</span>
							<span class="text-xs text-text-muted">{card.label} <span class="text-brand-accent/50">→</span></span>
						</button>
					{:else}
						<div class="bg-bkg-header border border-gray-800 rounded-xl p-4 flex flex-col gap-1">
							<span class="text-xl leading-none">{card.icon}</span>
							<span class="text-2xl font-black text-white/95 mt-1">{val ?? '—'}</span>
							<span class="text-xs text-text-muted">{card.label}</span>
						</div>
					{/if}
				{/each}
			</div>

			<!-- Tracking distribution chart -->
			{@const dist = stats.trackingDistribution}
			{@const distTotal = dist.planned + dist.watching + dist.completed + dist.dropped}
			{#if distTotal > 0}
				<div class="bg-bkg-header border border-gray-800 rounded-xl p-5">
					<h3 class="text-sm font-semibold text-white/80 mb-4">Розподіл за статусом відстеження</h3>
					<div class="flex flex-col sm:flex-row gap-6 items-center">
						<div class="w-48 h-48 shrink-0">
							<canvas bind:this={trackingCanvas}></canvas>
						</div>
						<div class="grid grid-cols-2 gap-3 flex-1 w-full">
							{#each [
								{ label: 'Планую',    count: dist.planned,   color: 'text-orange-400',  bg: 'bg-orange-500/10'  },
								{ label: 'Дивлюсь',   count: dist.watching,  color: 'text-blue-400',    bg: 'bg-blue-500/10'    },
								{ label: 'Завершено', count: dist.completed, color: 'text-emerald-400', bg: 'bg-emerald-500/10' },
								{ label: 'Кинув',     count: dist.dropped,   color: 'text-red-400',     bg: 'bg-red-500/10'     },
							] as s (s.label)}
								<div class="rounded-lg p-3 {s.bg} flex flex-col gap-0.5">
									<span class="text-xl font-black {s.color}">{s.count}</span>
									<span class="text-xs text-text-muted">{s.label}</span>
									<span class="text-xs text-text-muted opacity-60">{distTotal > 0 ? Math.round(s.count / distTotal * 100) : 0}%</span>
								</div>
							{/each}
						</div>
					</div>
				</div>
			{/if}
		{/if}
	{/if}
</div>
