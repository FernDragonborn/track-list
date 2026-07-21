<script lang="ts">
	import '../app.css';
	import favicon from '$lib/assets/favicon.svg';

	import Header from '$lib/components/Header.svelte';
	import Breadcrumbs from '$lib/components/Breadcrumbs.svelte';
	import { invalidate, beforeNavigate, afterNavigate } from '$app/navigation';
	import { onMount } from 'svelte';
	import { previousPath } from '$lib/stores/breadcrumb';

	let { children, data } = $props();

	// Set previousPath BEFORE the new route mounts, so the new page's Breadcrumbs
	// derive sees the correct context on first paint.
	beforeNavigate(({ from }) => {
		const p = from?.url.pathname ?? null;
		previousPath.set(p ? p.replace(/\/$/, '') || '/' : null);
	});
	// Also handle initial page load (beforeNavigate doesn't fire on cold boot).
	afterNavigate(({ from }) => {
		if (!from) return;
		const p = from.url.pathname.replace(/\/$/, '') || '/';
		previousPath.set(p);
	});

	onMount(() => {
		const revalidate = () => invalidate('app:user');
		const onVisibility = () => { if (!document.hidden) revalidate(); };
		document.addEventListener('visibilitychange', onVisibility);
		const interval = setInterval(revalidate, 60_000);
		return () => {
			document.removeEventListener('visibilitychange', onVisibility);
			clearInterval(interval);
		};
	});
</script>

<svelte:head>
	<link rel="icon" href={favicon} />
	<title>TrackList TrackList</title>
</svelte:head>

<div class="min-h-screen flex flex-col font-sans text-black">
	<Header user={data.user} />
	<Breadcrumbs />

	<main class="flex-1 w-full max-w-7xl mx-auto p-6">
		{@render children()}
	</main>
</div>

<!-- {@render children()} -->
