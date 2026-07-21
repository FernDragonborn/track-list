<script lang="ts">
	import MediaPageView from '$lib/components/MediaPageView.svelte';
	import type { PageData } from './$types';

	let { data }: { data: PageData } = $props();
</script>

<svelte:head>
	<title>{data.media.translations.find((t) => t.title)?.title ?? 'Медіа'} · TrackList</title>
</svelte:head>

<MediaPageView
	media={data.media}
	reviews={data.reviews}
	trackingStats={data.trackingStats}
	inCollections={data.inCollections}
	token={data.token}
	username={data.username}
	userRole={data.userRole}
	currentUserId={data.currentUserId}
/>
