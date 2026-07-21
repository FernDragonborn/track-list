<script lang="ts">
	import { resolve } from '$app/paths';
	import { api } from '$lib/api';
	import { extractError } from '$lib/utils/errors';
	import type { CollectionResponseDto } from '$lib/types/collectionTypes';

	interface Props {
		open: boolean;
		mediaId: string;
		token: string;
		currentUserId: string;
		onClose: () => void;
		onAdd?: (collection: CollectionResponseDto) => void;
		onRemove?: (collectionId: string) => void;
	}

	let { open = $bindable(false), mediaId, token, currentUserId, onClose, onAdd, onRemove }: Props = $props();

	let collections = $state<CollectionResponseDto[]>([]);
	let loading = $state(false);
	let actionMap = $state<Record<string, boolean>>({});
	let error = $state('');
	// collectionId → itemId for collections already containing this media
	let membershipMap = $state<Record<string, string>>({});

	// inline creation
	let showCreateForm = $state(false);
	let newName = $state('');
	let creating = $state(false);

	$effect(() => {
		if (!open) {
			collections = [];
			error = '';
			membershipMap = {};
			actionMap = {};
			showCreateForm = false;
			newName = '';
			return;
		}
		loading = true;
		error = '';
		Promise.all([
			api.getUserCollections(currentUserId, token),
			api.getUserMembershipsForMedia(mediaId, token),
		])
			.then(([paged, memberships]) => {
				// filter out default "Вподобане" collection
				collections = paged?.items ?? [];
				const map: Record<string, string> = {};
				for (const m of memberships) map[m.collectionId] = m.itemId;
				membershipMap = map;
			})
			.catch(() => { error = 'Не вдалося завантажити добірки'; })
			.finally(() => { loading = false; });
	});

	async function addToCollection(collectionId: string) {
		actionMap = { ...actionMap, [collectionId]: true };
		error = '';
		try {
			const item = await api.addCollectionItem(collectionId, mediaId, token);
			membershipMap = { ...membershipMap, [collectionId]: item.id };
			const col = collections.find((c) => c.id === collectionId);
			if (col) {
				col.itemCount += 1;
				collections = collections;
				onAdd?.(col);
			}
		} catch (e: unknown) {
			error = extractError(e, 'Помилка додавання');
		} finally {
			actionMap = { ...actionMap, [collectionId]: false };
		}
	}

	async function removeFromCollection(collectionId: string) {
		const itemId = membershipMap[collectionId];
		if (!itemId) return;
		actionMap = { ...actionMap, [collectionId]: true };
		error = '';
		try {
			await api.removeCollectionItem(collectionId, itemId, token);
			const next = { ...membershipMap };
			delete next[collectionId];
			membershipMap = next;
			const col = collections.find((c) => c.id === collectionId);
			if (col) {
				col.itemCount = Math.max(0, col.itemCount - 1);
				collections = collections;
			}
			onRemove?.(collectionId);
		} catch {
			error = 'Помилка видалення';
		} finally {
			actionMap = { ...actionMap, [collectionId]: false };
		}
	}

	async function createAndAdd() {
		if (!newName.trim()) return;
		creating = true;
		error = '';
		try {
			const created = await api.createCollection({ name: newName.trim() }, token);
			const item = await api.addCollectionItem(created.id, mediaId, token);
			membershipMap = { ...membershipMap, [created.id]: item.id };
			collections = [...collections, { ...created, itemCount: 1 }];
			onAdd?.(created);
			newName = '';
			showCreateForm = false;
		} catch {
			error = 'Не вдалося створити добірку';
		} finally {
			creating = false;
		}
	}

	function onKeydown(e: KeyboardEvent) {
		if (open && e.key === 'Escape') onClose();
	}
</script>

<svelte:window onkeydown={onKeydown} />

{#if open}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
		role="dialog"
		aria-modal="true"
		tabindex={-1}
		onclick={(e) => { if (e.target === e.currentTarget) onClose(); }}
		onkeydown={(e) => { if (e.key === 'Escape') onClose(); }}
	>
		<div
			class="w-full max-w-sm bg-bkg-header border border-gray-700 rounded-2xl shadow-2xl overflow-hidden"
		>
			<div class="flex items-center justify-between px-6 py-5 border-b border-gray-800">
				<h2 class="text-lg font-bold text-white/95">Додати до добірки</h2>
				<button onclick={onClose} class="text-2xl text-gray-400 hover:text-white/95 transition-colors">×</button>
			</div>

			<div class="max-h-80 overflow-y-auto">
				{#if loading}
					<div class="py-10 text-center text-text-muted text-base">Завантаження...</div>
				{:else if error}
					<div class="py-8 px-6 text-red-400 text-base">{error}</div>
				{:else if collections.length === 0}
					<div class="py-10 text-center text-text-muted text-base">
						<p>У вас немає добірок</p>
						<a href={resolve('/collections')} class="text-brand-accent hover:underline text-sm mt-2 block">
							Створити добірку
						</a>
					</div>
				{:else}
					{#each collections as c, i (c.id)}
						{#if i > 0}
							<hr class="mx-5 border-gray-600" />
						{/if}
						{@const inCollection = c.id in membershipMap}
						{@const busy = !!actionMap[c.id]}
						<div class="w-full flex items-center justify-between px-6 py-4">
							<div class="flex flex-col gap-0.5 flex-1 min-w-0">
								<span class="text-base font-semibold text-white/90 truncate">{c.name}</span>
								<span class="text-xs text-text-muted">{c.itemCount} медіа · {c.privacyLevel === 'private' ? '🔒' : '🌐'}</span>
							</div>
							<button
								onclick={() => inCollection ? removeFromCollection(c.id) : addToCollection(c.id)}
								disabled={busy}
								class="shrink-0 ml-4 text-sm font-semibold transition-colors disabled:opacity-50
								       {inCollection ? 'text-red-400 hover:text-red-300' : 'text-brand-accent hover:text-brand-hover'}"
							>
								{#if busy}
									...
								{:else if inCollection}
									Видалити
								{:else}
									Додати
								{/if}
							</button>
						</div>
					{/each}
				{/if}
			</div>

				{#if !loading}
					<div class="px-6 py-4 border-t border-gray-800">
						{#if showCreateForm}
							<div class="flex items-center gap-2">
								<input
									type="text"
									bind:value={newName}
									placeholder="Назва добірки"
									class="flex-1 bg-gray-800 text-white/90 text-base rounded-lg px-4 py-2.5
									       border border-gray-700 focus:border-brand-accent focus:outline-none"
									onkeydown={(e) => { if (e.key === 'Enter') createAndAdd(); }}
								/>
								<button
									onclick={createAndAdd}
									disabled={creating || !newName.trim()}
									class="shrink-0 text-sm font-semibold text-brand-accent hover:text-brand-hover
									       disabled:opacity-50 transition-colors"
								>
									{creating ? '...' : 'Створити'}
								</button>
								<button
									onclick={() => { showCreateForm = false; newName = ''; }}
									class="shrink-0 text-base text-gray-400 hover:text-white/90 transition-colors"
								>
									×
								</button>
							</div>
						{:else}
							<button
								onclick={() => { showCreateForm = true; }}
								class="w-full text-base text-brand-accent hover:text-brand-hover transition-colors text-left"
							>
								＋ Нова добірка
							</button>
						{/if}
					</div>
				{/if}
		</div>
	</div>
{/if}
