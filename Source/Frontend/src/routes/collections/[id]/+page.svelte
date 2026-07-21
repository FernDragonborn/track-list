<script lang="ts">
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import { setCrumbLabel, clearCrumbLabel } from '$lib/stores/breadcrumb';
	import type { CollectionDetailResponseDto, CollectionItemDto } from '$lib/types/collectionTypes';

	let { data } = $props<{
		data: {
			collection: CollectionDetailResponseDto;
			token: string | null;
			currentUserId: string | null;
		};
	}>();

	let items = $state<CollectionItemDto[]>(untrack(() => data.collection.items));
	let removingId = $state<string | null>(null);

	// Reset items when navigating between collections (same /collections/[id] route).
	$effect(() => {
		const _id = data.collection.id;
		untrack(() => {
			items = data.collection.items;
			removingId = null;
		});
	});

	const isOwner = $derived(
		data.currentUserId !== null && data.currentUserId === data.collection.ownerId
	);

	$effect(() => {
		const href = `/collections/${data.collection.id}`;
		setCrumbLabel(href, data.collection.name);
		return () => clearCrumbLabel(href);
	});

	async function removeItem(item: CollectionItemDto) {
		if (!confirm(`Видалити «${item.mediaTitle ?? 'медіа'}» з добірки?`)) return;
		removingId = item.id;
		try {
			await api.removeCollectionItem(data.collection.id, item.id, data.token ?? undefined);
			items = items.filter((i) => i.id !== item.id);
		} catch {
			// silent
		} finally {
			removingId = null;
		}
	}
</script>

<div class="max-w-4xl mx-auto">
	<!-- Header -->
	<div class="bg-bkg-header/80 border border-gray-800 rounded-2xl p-6 mb-6">
		<div class="flex flex-col sm:flex-row sm:items-start gap-4">
			<div class="flex-1">
				<div class="flex items-center gap-3 flex-wrap mb-2">
					<h1 class="text-2xl font-black text-white/95">{data.collection.name}</h1>
					<span
						class="text-xs px-2 py-0.5 rounded-full border
						       {data.collection.privacyLevel === 'private'
							       ? 'border-gray-600 text-gray-400'
							       : 'border-blue-700 text-blue-400'}"
					>
						{data.collection.privacyLevel === 'private' ? '🔒 Приватна' : '🌐 Публічна'}
					</span>
				</div>

				{#if data.collection.description}
					<p class="text-white/70 text-sm leading-relaxed mb-3">{data.collection.description}</p>
				{/if}

				<p class="text-xs text-text-muted">
					Власник:
					<a
						href={resolve(`/profile/${data.collection.ownerUsername}`)}
						class="text-brand-accent hover:underline"
					>
						@{data.collection.ownerUsername}
					</a>
					· {items.length} медіа
				</p>
			</div>

			{#if isOwner}
				<a
					href={resolve(`/collections/${data.collection.id}/settings`)}
					class="shrink-0 px-4 py-2 rounded-lg border border-gray-700 text-gray-300 text-sm font-semibold hover:bg-white/10 hover:text-white/95 transition-colors"
				>
					Налаштування
				</a>
			{/if}
		</div>

		{#if isOwner && data.collection.sharedWith.length > 0}
			<div class="mt-4 pt-4 border-t border-gray-800">
				<p class="text-xs text-text-muted mb-1">Спільний доступ:</p>
				<div class="flex flex-wrap gap-2">
					{#each data.collection.sharedWith as access (access.id)}
						<a
							href={resolve(`/profile/${access.username}`)}
							class="text-xs bg-gray-800 px-2 py-1 rounded-full text-gray-300 hover:text-white/95 transition-colors"
						>
							@{access.username}
						</a>
					{/each}
				</div>
			</div>
		{/if}
	</div>

	<!-- Items -->
	{#if items.length === 0}
		<div class="bg-bkg-header/80 border border-gray-800 rounded-2xl p-12 text-center">
			<p class="text-text-muted text-sm">Добірка порожня</p>
			{#if isOwner}
				<p class="text-xs text-text-muted mt-2">
					Додавайте медіа через кнопку «Додати до добірки» на сторінці медіа
				</p>
			{/if}
		</div>
	{:else}
		<div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
			{#each items as item (item.id)}
				<div class="flex flex-col gap-2">
					<a
						href={resolve(`/media/${item.mediaId}`)}
						class="block aspect-[2/3] rounded-lg overflow-hidden bg-gray-800 hover:opacity-80 transition-opacity"
					>
						{#if item.mediaPosterUrl}
							<img
								src={item.mediaPosterUrl}
								alt={item.mediaTitle ?? ''}
								class="w-full h-full object-cover"
								loading="lazy"
							/>
						{:else}
							<div class="w-full h-full flex items-center justify-center">
								<span class="text-3xl text-gray-600">🎬</span>
							</div>
						{/if}
					</a>

					<div class="flex flex-col gap-1">
						<a
							href={resolve(`/media/${item.mediaId}`)}
							class="text-xs font-semibold text-white/90 hover:text-brand-accent transition-colors line-clamp-2 leading-tight"
						>
							{item.mediaTitle ?? 'Без назви'}
						</a>
						{#if isOwner}
							<button
								onclick={() => removeItem(item)}
								disabled={removingId === item.id}
								class="text-[10px] text-red-500 hover:text-red-400 transition-colors text-left disabled:opacity-50"
							>
								{removingId === item.id ? '...' : '× Видалити'}
							</button>
						{/if}
					</div>
				</div>
			{/each}
		</div>
	{/if}
</div>
