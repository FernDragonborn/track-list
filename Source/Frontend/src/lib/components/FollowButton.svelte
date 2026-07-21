<script lang="ts">
	import { invalidateAll } from '$app/navigation';
	import { api } from '$lib/api';

	interface Props {
		username: string;
		initialIsFollowing: boolean;
		token?: string;
	}

	let { username, initialIsFollowing, token }: Props = $props();

	// Writable $derived: starts from the prop, accepts local optimistic writes
	// (toggleFollow), then resyncs when the parent passes a new prop value
	// (same-route navigation /profile/X → /profile/Y).
	let isFollowing = $derived(initialIsFollowing);
	let isLoading = $state(false);
	let isHoveringUnfollow = $state(false);

	async function toggleFollow() {
		if (isLoading) return;

		const wasFollowing = isFollowing;
		isFollowing = !wasFollowing;
		isLoading = true;

		try {
			if (wasFollowing) {
				await api.unfollowUser(username, token);
			} else {
				await api.followUser(username, token);
			}
			await invalidateAll();
		} catch (e) {
			isFollowing = wasFollowing;
			console.error('follow/unfollow failed', e);
		} finally {
			isLoading = false;
		}
	}
</script>

<button
	onclick={toggleFollow}
	disabled={isLoading}
	onmouseenter={() => (isHoveringUnfollow = isFollowing)}
	onmouseleave={() => (isHoveringUnfollow = false)}
	class="min-w-[140px] px-5 py-2 rounded-lg text-sm font-semibold transition-all duration-200
	       cursor-pointer disabled:opacity-60 disabled:cursor-not-allowed
	       {isFollowing
		? isHoveringUnfollow
			? 'bg-red-500/20 border border-red-500 text-red-400 scale-95'
			: 'bg-bkg-header border border-gray-600 text-white/80 hover:border-gray-400'
		: 'border border-brand-accent text-brand-accent hover:bg-brand-accent hover:text-white hover:scale-105 shadow-md shadow-brand-accent/10'}"
>
	{#if isLoading}
		<span class="inline-flex items-center gap-2 justify-center w-full">
			<span class="w-3.5 h-3.5 border-2 border-current border-t-transparent rounded-full animate-spin"></span>
			{isFollowing ? 'Відписуюсь...' : 'Підписуюсь...'}
		</span>
	{:else if isFollowing}
		{isHoveringUnfollow ? 'Відписатися' : 'Підписаний'}
	{:else}
		Підписатися
	{/if}
</button>
