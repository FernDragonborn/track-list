<script lang="ts">
	import { resolve } from '$app/paths';
	import { api } from '$lib/api';
	import { getAvatarUrl } from '$lib/utils/avatar';
	import type { ProfileDto } from '$lib/types/profileTypes';

	let {
		open = $bindable(false),
		type,
		username,
		token,
	}: {
		open: boolean;
		type: 'followers' | 'following';
		username: string;
		token?: string;
	} = $props();

	let users: ProfileDto[] = $state([]);
	let loading = $state(false);
	let error = $state('');

	$effect(() => {
		if (!open) return;
		let cancelled = false;
		loading = true;
		error = '';
		users = [];
		const req = type === 'followers'
			? api.getFollowers(username, token)
			: api.getFollowing(username, token);
		req
			.then((list) => { if (!cancelled) users = list; })
			.catch(() => { if (!cancelled) error = 'Не вдалося завантажити список'; })
			.finally(() => { if (!cancelled) loading = false; });
		return () => { cancelled = true; };
	});

	function close() {
		open = false;
	}

	function onKeydown(e: KeyboardEvent) {
		if (open && e.key === 'Escape') close();
	}
</script>

<svelte:window onkeydown={onKeydown} />

{#if open}
	<!-- backdrop — click outside the panel closes; Escape via svelte:window above -->
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
		role="dialog"
		aria-modal="true"
		tabindex={-1}
		onclick={(e) => { if (e.target === e.currentTarget) close(); }}
		onkeydown={(e) => { if (e.key === 'Escape') close(); }}
	>
		<!-- panel -->
		<div
			class="relative w-full max-w-sm bg-bkg-header border border-gray-800 rounded-2xl shadow-2xl overflow-hidden"
		>
			<!-- header -->
			<div class="flex items-center justify-between px-5 py-4 border-b border-gray-800">
				<h2 class="text-base font-bold text-white/95">
					{type === 'followers' ? 'Підписники' : 'Підписки'}
				</h2>
				<button
					onclick={close}
					class="text-text-muted hover:text-white transition-colors leading-none text-xl"
					aria-label="Закрити"
				>
					×
				</button>
			</div>

			<!-- body -->
			<div class="max-h-80 overflow-y-auto">
				{#if loading}
					<div class="flex justify-center items-center py-10">
						<span class="w-6 h-6 border-2 border-brand-accent border-t-transparent rounded-full animate-spin"></span>
					</div>
				{:else if error}
					<p class="text-center text-red-400 text-sm py-8">{error}</p>
				{:else if users.length === 0}
					<p class="text-center text-text-muted text-sm py-8">Список порожній</p>
				{:else}
					<ul>
						{#each users as user (user.username)}
							<li>
								<a
									href={resolve(`/profile/${user.username}`)}
									onclick={close}
									class="flex items-center gap-3 px-5 py-3 hover:bg-white/5 transition-colors"
								>
									<img
										src={getAvatarUrl(user.displayName ?? user.username, user.profilePicUrl, 40)}
										alt={user.username}
										width="40"
										height="40"
										class="w-10 h-10 rounded-full object-cover shrink-0 border border-gray-700"
									/>
									<div class="min-w-0">
										{#if user.displayName}
											<p class="text-sm font-semibold text-white/95 truncate">{user.displayName}</p>
											<p class="text-xs text-text-muted truncate">@{user.username}</p>
										{:else}
											<p class="text-sm font-semibold text-white/95 truncate">@{user.username}</p>
										{/if}
									</div>
								</a>
							</li>
						{/each}
					</ul>
				{/if}
			</div>
		</div>
	</div>
{/if}
