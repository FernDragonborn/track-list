<script lang="ts">
	import { resolve } from '$app/paths';
	import { tick, untrack } from 'svelte';
	import { getAvatarUrl } from '$lib/utils/avatar';
	import type { ProfileDto } from '$lib/types/profileTypes';
	import type { ProfileTrackingItem } from '$lib/types/trackingTypes';
	import type { CollectionResponseDto } from '$lib/types/collectionTypes';
	import type { ProfileReviewItem, FeedItemDto } from '$lib/types/reviewTypes';
	import { api } from '$lib/api';
	import FollowButton from '$lib/components/FollowButton.svelte';
	import FollowListModal from '$lib/components/FollowListModal.svelte';
	import TrackingTab from '$lib/components/TrackingTab.svelte';
	import ReportModal from '$lib/components/ReportModal.svelte';
	import InfiniteScrollSentinel from '$lib/components/InfiniteScrollSentinel.svelte';
	import FeedCard from '$lib/components/FeedCard.svelte';

	function toFeedItem(r: ProfileReviewItem): FeedItemDto {
		return {
			reviewId: r.id,
			mediaId: r.mediaId,
			mediaExternalId: r.mediaExternalApiId,
			mediaTitle: r.mediaTitle,
			mediaPosterUrl: r.mediaPosterUrl,
			userId: r.userId,
			username: r.username,
			profilePicUrl: r.profilePicUrl,
			rating: r.rating,
			content: r.content,
			createdAt: r.createdAt,
			likeCount: r.likeCount,
			commentCount: r.commentCount,
			isLikedByMe: r.isLikedByMe,
		};
	}

	let { data } = $props<{
		data: {
			profile: ProfileDto;
			isOwnProfile: boolean;
			token?: string;
			userId?: string;
			tracking?: ProfileTrackingItem[];
			collections?: CollectionResponseDto[];
		};
	}>();

	type Tab = 'profile' | 'tracking' | 'reviews' | 'collections';

	let collections = $state<CollectionResponseDto[]>(untrack(() => data.collections ?? []));
	let activeTab = $state<Tab>('profile');

	// Profile reviews state
	let reviews = $state<ProfileReviewItem[]>([]);
	let reviewsTotal = $state(0);
	let reviewsPage = $state(1);
	let reviewsLoading = $state(false);
	let reviewsLoaded = $state(false);
	const reviewsHasMore = $derived(reviews.length < reviewsTotal);

	async function loadReviews(page = 1) {
		if (reviewsLoading) return;
		reviewsLoading = true;
		try {
			const result = await api.getProfileReviews(data.profile.username, page, 10, data.token);
			if (page === 1) {
				reviews = result.items;
			} else {
				reviews = [...reviews, ...result.items];
			}
			reviewsTotal = result.totalCount;
			reviewsPage = page;
			reviewsLoaded = true;
		} catch {
			// keep current
		} finally {
			reviewsLoading = false;
		}
	}

	$effect(() => {
		if (activeTab === 'reviews' && !reviewsLoaded) {
			loadReviews(1);
		}
	});

	async function goToTrackingGroup(statusKey: string) {
		activeTab = 'tracking';
		await tick();
		document.getElementById(`group-${statusKey}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
	}

	const avatarUrl = $derived(getAvatarUrl(data.profile.username, data.profile.profilePicUrl, 128));

	let modalOpen = $state(false);
	let modalType: 'followers' | 'following' = $state('followers');
	let showReportModal = $state(false);

	function openModal(type: 'followers' | 'following') {
		modalType = type;
		modalOpen = true;
	}

	let usernameCopied = $state(false);

	function copyUsername() {
		navigator.clipboard.writeText(`@${data.profile.username}`).then(() => {
			usernameCopied = true;
			setTimeout(() => (usernameCopied = false), 1500);
		});
	}

	// Local tracking state — initialized from SSR, refreshed via effect to stay current.
	let tracking = $state<ProfileTrackingItem[]>(untrack(() => data.tracking ?? []));

	async function refreshTracking(username: string, token: string | null) {
		try {
			tracking = await api.getProfileTracking(username, token ?? undefined);
		} catch {
			/* keep SSR data */
		}
	}

	// Reset local state when navigating between profiles (same /profile/[username] route).
	$effect(() => {
		const username = data.profile.username;
		untrack(() => {
			collections = data.collections ?? [];
			tracking = data.tracking ?? [];
			reviews = [];
			reviewsTotal = 0;
			reviewsPage = 1;
			reviewsLoaded = false;
			activeTab = 'profile';
			refreshTracking(username, data.token);
		});
	});

	const currentYear = new Date().getFullYear();

	const MEDIA_ROW_DEFS = [
		{ type: 'movie', label: 'Фільми', countLabel: 'фільмів переглянуто', yearLabel: 'фільмів у' },
		{ type: 'series', label: 'Серіали', countLabel: 'серіалів переглянуто', yearLabel: 'серіалів у' },
	] as const;

	const STATUS_BARS = [
		{ key: 'completed' as const, label: 'Переглянуто', color: 'bg-brand-accent' },
		{ key: 'watching' as const, label: 'Дивлюся', color: 'bg-blue-500' },
		{ key: 'planned' as const, label: 'У планах', color: 'bg-gray-500' },
		{ key: 'dropped' as const, label: 'Кинуто', color: 'bg-red-500' },
	];

	// Reactive stats — recomputes when `tracking` state updates (e.g. after onMount fetch)
	const mediaStats = $derived(
		MEDIA_ROW_DEFS.map((row) => {
			const items = tracking.filter(
				(i: ProfileTrackingItem) => i.mediaType?.toLowerCase() === row.type,
			);
			const completed = items.filter((i) => i.status === 'completed');
			return {
				...row,
				total: completed.length,
				thisYear: completed.filter(
					(i) => new Date(i.updatedAt).getFullYear() === currentYear,
				).length,
				counts: {
					planned: items.filter((i) => i.status === 'planned').length,
					watching: items.filter((i) => i.status === 'watching').length,
					completed: completed.length,
					dropped: items.filter((i) => i.status === 'dropped').length,
					total: items.length,
				},
				recent: completed.slice(0, 3),
			};
		}),
	);
</script>

<div class="min-h-[80vh] flex items-start justify-center pt-10 px-4">
	<div class="w-full max-w-3xl">
		<div class="bg-bkg-header/80 backdrop-blur-sm border border-gray-800 rounded-2xl shadow-2xl p-8">

			<!-- Header -->
			<div class="flex flex-col sm:flex-row items-center sm:items-start gap-6 mb-6">
				<img
					src={avatarUrl}
					alt={data.profile.username}
					width="96"
					height="96"
					class="w-24 h-24 rounded-full object-cover border-2 border-brand-accent shadow-lg shadow-brand-accent/20 shrink-0
					       transition-transform duration-300 hover:scale-105"
				/>

				<div class="flex-1 text-center sm:text-left">
					<h1 class="text-2xl font-black text-white/95 tracking-tight leading-tight">
						{data.profile.displayName ?? data.profile.username}
					</h1>
					<button
						onclick={copyUsername}
						class="inline-flex items-center gap-1 text-text-muted text-sm mt-0.5 hover:text-white/80 transition-colors group/copy"
						title={usernameCopied ? 'Скопійовано!' : 'Копіювати username'}
					>
						@{data.profile.username}
						<svg class="w-3 h-3 opacity-0 group-hover/copy:opacity-100 transition-opacity shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
							{#if usernameCopied}
								<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
							{:else}
								<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"/>
							{/if}
						</svg>
					</button>

					<div class="flex gap-6 mt-3 justify-center sm:justify-start">
						<button
							onclick={() => openModal('followers')}
							class="text-center hover:opacity-75 transition-opacity cursor-pointer"
						>
							<span class="block text-lg font-bold text-white/95">{data.profile.followersCount ?? 0}</span>
							<span class="text-xs text-text-muted uppercase tracking-wider">Підписників</span>
						</button>
						<div class="w-px bg-gray-700 self-stretch"></div>
						<button
							onclick={() => openModal('following')}
							class="text-center hover:opacity-75 transition-opacity cursor-pointer"
						>
							<span class="block text-lg font-bold text-white/95">{data.profile.followingCount}</span>
							<span class="text-xs text-text-muted uppercase tracking-wider">Підписок</span>
						</button>
					</div>

					{#if data.profile.bio}
						<p class="text-white/70 text-sm mt-3 leading-relaxed line-clamp-2">
							{data.profile.bio}
						</p>
					{:else}
						<p class="text-text-muted text-sm mt-3 italic">Немає інформації</p>
					{/if}
				</div>

				<div class="shrink-0">
					{#if data.isOwnProfile}
						<a
							href={resolve(`/profile/${data.profile.username}/edit`)}
							class="inline-block px-5 py-2 rounded-lg border border-brand-accent text-brand-accent text-sm font-semibold
							       hover:bg-brand-accent hover:text-white transition-all duration-200"
						>
							Редагувати профіль
						</a>
					{:else}
						<div class="flex flex-col items-end gap-2">
							<FollowButton
								username={data.profile.username}
								initialIsFollowing={data.profile.isFollowing}
								token={data.token}
							/>
							{#if data.token && data.profile.id}
								<button
									onclick={() => (showReportModal = true)}
									class="text-xs text-text-muted hover:text-yellow-400 transition-colors"
									title="Поскаржитись на профіль"
								>Поскаржитись</button>
							{/if}
						</div>
					{/if}
				</div>
			</div>

			<!-- Tabs -->
			<div class="border-t border-gray-800">
				<nav class="flex border-b border-gray-800 -mb-px">
					{#each [['profile', 'Профіль'], ['tracking', 'Трекінг'], ['reviews', 'Рецензії'], ['collections', 'Добірки']] as tab (tab[0])}
						<button
							onclick={() => (activeTab = tab[0] as Tab)}
							class="px-4 py-3 text-sm font-semibold transition-colors border-b-2
							       {activeTab === tab[0]
								       ? 'border-brand-accent text-brand-accent'
								       : 'border-transparent text-text-muted hover:text-white/80'}"
						>
							{tab[1]}
						</button>
					{/each}
				</nav>

				<div class="pt-6">
					{#if activeTab === 'profile'}
						<!-- Stats rows per media type -->
						{#each mediaStats as row (row.type)}
							<div class="flex flex-col sm:flex-row gap-4 items-start py-5 border-b border-gray-800 last:border-0">

								<!-- Status distribution bars -->
								<div class="w-36 shrink-0">
									<p class="text-[10px] font-semibold text-text-muted uppercase tracking-wider mb-2">
										{row.label}
									</p>
									{#if row.counts.total === 0}
										<p class="text-[11px] text-text-muted italic">Немає даних</p>
									{:else}
										<div class="flex flex-col gap-1.5">
											{#each STATUS_BARS as bar (bar.key)}
												{#if row.counts[bar.key] > 0}
													<button
														onclick={() => goToTrackingGroup(bar.key)}
														class="flex items-center gap-1.5 w-full text-left hover:opacity-75 transition-opacity"
													>
														<span class="text-[9px] text-text-muted w-16 truncate leading-none">{bar.label}</span>
														<div class="flex-1 h-1.5 bg-gray-800 rounded-full overflow-hidden">
															<div
																class="{bar.color} h-full rounded-full transition-all"
																style="width: {Math.round((row.counts[bar.key] / row.counts.total) * 100)}%"
															></div>
														</div>
														<span class="text-[9px] text-white/50 w-4 text-right">{row.counts[bar.key]}</span>
													</button>
												{/if}
											{/each}
										</div>
									{/if}
								</div>

								<!-- Counts -->
								<div class="flex gap-8 flex-1 justify-center items-center">
									<div class="text-center">
										<span class="block text-4xl font-black text-white/95 tabular-nums">
											{String(row.total).padStart(3, '0')}
										</span>
										<span class="text-[10px] text-text-muted">Всього {row.countLabel}</span>
									</div>
									<div class="text-center">
										<span class="block text-4xl font-black text-white/95 tabular-nums">
											{String(row.thisYear).padStart(3, '0')}
										</span>
										<span class="text-[10px] text-text-muted">{row.yearLabel} {currentYear}</span>
									</div>
								</div>

								<!-- Recent posters -->
								<div class="shrink-0">
									<p class="text-[10px] font-semibold text-text-muted uppercase tracking-wider mb-2">
										Останні переглянуті
									</p>
									<div class="flex gap-1.5">
										{#each [0, 1, 2] as i (i)}
											{#if row.recent[i]}
												<a
													href={resolve(`/media/${row.recent[i].mediaId}`)}
													class="block w-12 aspect-[2/3] rounded overflow-hidden bg-gray-800 hover:opacity-80 transition-opacity"
												>
													{#if row.recent[i].mediaPosterUrl}
														<img
															src={row.recent[i].mediaPosterUrl}
															alt={row.recent[i].mediaTitle ?? ''}
															class="w-full h-full object-cover"
															loading="lazy"
														/>
													{/if}
												</a>
											{:else}
												<div class="w-12 aspect-[2/3] rounded bg-gray-800/40 border border-gray-800"></div>
											{/if}
										{/each}
									</div>
								</div>
							</div>
						{/each}

					{:else if activeTab === 'tracking'}
						<TrackingTab
							items={tracking}
							isOwnProfile={data.isOwnProfile}
							token={data.token ?? null}
							onDelete={(mediaId) => { tracking = tracking.filter((i) => i.mediaId !== mediaId); }}
							onUpdate={(mediaId, status, progress) => {
								tracking = tracking.map((i) => i.mediaId === mediaId ? { ...i, status, progress } : i);
							}}
						/>

					{:else if activeTab === 'reviews'}
						{#if reviewsLoading && reviews.length === 0}
							<div class="flex justify-center py-12">
								<span class="w-6 h-6 border-2 border-brand-accent border-t-transparent rounded-full animate-spin"></span>
							</div>
						{:else if reviews.length === 0}
							<div class="py-12 text-center text-text-muted">
								<p class="text-sm">Ще немає рецензій</p>
							</div>
						{:else}
							<div class="flex flex-col gap-4">
								{#each reviews as r (r.id)}
									<FeedCard item={toFeedItem(r)} token={data.token ?? null} />
								{/each}
							</div>
							<InfiniteScrollSentinel
								hasMore={reviewsHasMore}
								loading={reviewsLoading}
								onLoadMore={() => loadReviews(reviewsPage + 1)}
							/>
						{/if}

					{:else if activeTab === 'collections'}
						{#if data.isOwnProfile}
							<div class="flex justify-end mb-3">
								<a
									href={resolve('/collections')}
									class="inline-flex items-center gap-1 bg-brand-accent hover:bg-brand-hover text-white px-4 py-1.5 rounded-full font-bold text-xs transition-all hover:scale-105 shadow-lg shadow-brand-accent/20"
								>
									+ Нова добірка
								</a>
							</div>
						{/if}
						{#if collections.length === 0}
							<div class="py-12 text-center text-text-muted">
								<p class="text-sm">Немає добірок</p>
							</div>
						{:else}
							<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
								{#each collections as c (c.id)}
									<a
										href={resolve(`/collections/${c.id}`)}
										class="flex flex-col gap-2 bg-gray-800/60 border border-gray-700 rounded-xl p-4 hover:border-gray-500 transition-colors"
									>
										<div class="flex items-center justify-between gap-2">
											<span class="text-sm font-bold text-white/95 leading-tight">{c.name}</span>
											<span
												class="shrink-0 text-[10px] px-2 py-0.5 rounded-full border
												       {c.privacyLevel === 'private'
													       ? 'border-gray-600 text-gray-400'
													       : 'border-blue-700 text-blue-400'}"
											>
												{c.privacyLevel === 'private' ? '🔒' : '🌐'}
											</span>
										</div>
										{#if c.description}
											<p class="text-[11px] text-text-muted line-clamp-1">{c.description}</p>
										{/if}
										<p class="text-[10px] text-text-muted mt-auto">{c.itemCount} медіа</p>
									</a>
								{/each}
							</div>
						{/if}
					{/if}
				</div>
			</div>
		</div>
	</div>
</div>

<FollowListModal
	bind:open={modalOpen}
	type={modalType}
	username={data.profile.username}
	token={data.token}
/>

{#if showReportModal && data.token && data.profile.id}
	<ReportModal
		bind:open={showReportModal}
		targetId={data.profile.id}
		targetType="Profile"
		token={data.token}
		userId={data.userId ?? ''}
		onClose={() => (showReportModal = false)}
	/>
{/if}
