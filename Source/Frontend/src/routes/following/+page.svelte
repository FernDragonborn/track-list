<script lang="ts">
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import { getAvatarUrl } from '$lib/utils/avatar';
	import type { ProfileDto } from '$lib/types/profileTypes';

	let { data } = $props<{
		data: { following: ProfileDto[]; token: string | null };
	}>();

	let users = $state<ProfileDto[]>(untrack(() => data.following));
	let loadingMap = $state<Record<string, boolean>>({});

	function avatarUrl(u: ProfileDto) {
		return getAvatarUrl(u.username, u.profilePicUrl, 80);
	}

	async function toggleFollow(u: ProfileDto) {
		if (!data.token) return;
		loadingMap = { ...loadingMap, [u.username]: true };
		try {
			if (u.isFollowing) {
				await api.unfollowUser(u.username, data.token);
				users = users.map((x) =>
					x.username === u.username
						? { ...x, isFollowing: false, followersCount: x.followersCount - 1 }
						: x,
				);
			} else {
				await api.followUser(u.username, data.token);
				users = users.map((x) =>
					x.username === u.username
						? { ...x, isFollowing: true, followersCount: x.followersCount + 1 }
						: x,
				);
			}
		} catch {
			// silent
		} finally {
			loadingMap = { ...loadingMap, [u.username]: false };
		}
	}
</script>

<svelte:head>
	<title>Підписки · TrackList</title>
</svelte:head>

<div class="max-w-2xl mx-auto pt-10 px-4">
	<h1 class="text-2xl font-black text-white/95 mb-6">Підписки</h1>

	{#if users.length === 0}
		<div class="py-16 text-center border border-dashed border-gray-700 rounded-xl text-text-muted">
			<p class="text-base">Ви ні на кого не підписані</p>
			<a
				href={resolve('/')}
				class="inline-block mt-4 px-5 py-2 rounded-lg bg-brand-accent hover:bg-brand-hover
				       text-white text-sm font-semibold transition-all"
			>
				Переглянути каталог
			</a>
		</div>
	{:else}
		<div class="flex flex-col gap-3">
			{#each users as u (u.username)}
				<div
					class="flex items-center gap-4 bg-bkg-header border border-gray-800 rounded-xl p-4
					       hover:border-gray-700 transition-colors"
				>
					<!-- Avatar -->
					<a href={resolve(`/profile/${u.username}`)} class="shrink-0">
						<img
							src={avatarUrl(u)}
							alt={u.username}
							width="56"
							height="56"
							class="w-14 h-14 rounded-full object-cover border-2 border-gray-700
							       hover:border-brand-accent transition-colors"
						/>
					</a>

					<!-- Info -->
					<div class="flex-1 min-w-0">
						<a
							href={resolve(`/profile/${u.username}`)}
							class="hover:text-brand-accent transition-colors"
						>
							{#if u.displayName}
								<p class="text-white/95 font-bold text-sm leading-tight truncate">{u.displayName}</p>
								<p class="text-text-muted text-xs">@{u.username}</p>
							{:else}
								<p class="text-white/95 font-bold text-sm truncate">@{u.username}</p>
							{/if}
						</a>

						<!-- Stats row -->
						<div class="flex items-center gap-3 mt-1.5 flex-wrap">
							{#if u.memberSinceYear}
								<span class="text-[11px] text-text-muted">з {u.memberSinceYear}</span>
								<span class="text-gray-700">·</span>
							{/if}
							<span class="text-[11px] text-text-muted">
								<span class="text-white/70 font-semibold">{u.followersCount}</span> підписників
							</span>
							{#if u.reviewsCount !== undefined}
								<span class="text-gray-700">·</span>
								<span class="text-[11px] text-text-muted">
									<span class="text-white/70 font-semibold">{u.reviewsCount}</span>
									{u.reviewsCount === 1 ? 'рецензія' : u.reviewsCount >= 2 && u.reviewsCount <= 4 ? 'рецензії' : 'рецензій'}
								</span>
							{/if}
							{#if u.listsCount !== undefined && u.listsCount > 0}
								<span class="text-gray-700">·</span>
								<span class="text-[11px] text-text-muted">
									<span class="text-white/70 font-semibold">{u.listsCount}</span>
									{u.listsCount === 1 ? 'список' : 'списків'}
								</span>
							{/if}
						</div>
					</div>

					<!-- Follow / Unfollow button -->
					<div class="shrink-0">
						<button
							onclick={() => toggleFollow(u)}
							disabled={loadingMap[u.username]}
							class="px-4 py-1.5 rounded-lg text-xs font-bold transition-all active:scale-95 disabled:opacity-50
							       {u.isFollowing
								       ? 'border border-gray-600 text-text-muted hover:border-red-500 hover:text-red-400'
								       : 'bg-brand-accent hover:bg-brand-hover text-white shadow-sm shadow-brand-accent/20'}"
						>
							{u.isFollowing ? 'Відписатися' : 'Підписатися'}
						</button>
					</div>
				</div>
			{/each}
		</div>
	{/if}
</div>
