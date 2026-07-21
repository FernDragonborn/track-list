<script lang="ts">
	import { resolve } from '$app/paths';
	import { untrack } from 'svelte';
	import { api } from '$lib/api';
	import { DEFAULT_COLLECTION_NAME } from '$lib/constants';
	import { extractError } from '$lib/utils/errors';
	import SearchableSelect from '$lib/components/SearchableSelect.svelte';
	import type { CollectionResponseDto } from '$lib/types/collectionTypes';

	let { data } = $props<{
		data: { collections: CollectionResponseDto[]; token: string | null };
	}>();

	let collections = $state<CollectionResponseDto[]>(untrack(() => data.collections));
	let showCreateModal = $state(false);
	let createName = $state('');
	let createDesc = $state('');
	let createPrivacy = $state<'public' | 'private'>('public');
	let creating = $state(false);
	let createError = $state('');
	let deletingId = $state<string | null>(null);

	async function createCollection() {
		if (!createName.trim()) {
			createError = "Назва не може бути порожньою";
			return;
		}
		creating = true;
		createError = '';
		try {
			const c = await api.createCollection(
				{ name: createName.trim(), description: createDesc.trim() || undefined, privacyLevel: createPrivacy },
				data.token ?? undefined
			);
			collections = [c, ...collections];
			showCreateModal = false;
			createName = '';
			createDesc = '';
			createPrivacy = 'public';
		} catch (e: unknown) {
			createError = extractError(e, 'Помилка створення');
		} finally {
			creating = false;
		}
	}

	async function deleteCollection(id: string) {
		if (!confirm('Видалити добірку?')) return;
		deletingId = id;
		try {
			await api.deleteCollection(id, data.token ?? undefined);
			collections = collections.filter((c) => c.id !== id);
		} catch {
			// silent
		} finally {
			deletingId = null;
		}
	}

	function closeModal() {
		showCreateModal = false;
		createName = '';
		createDesc = '';
		createPrivacy = 'public';
		createError = '';
	}

	function onKeydown(e: KeyboardEvent) {
		if (e.key === 'Escape') closeModal();
	}
</script>

<svelte:window onkeydown={onKeydown} />

<div class="max-w-4xl mx-auto">
	<div class="flex items-center justify-between mb-8">
		<h1 class="text-2xl font-black text-white/95">Мої добірки</h1>
		<button
			onclick={() => (showCreateModal = true)}
			class="bg-brand-accent hover:bg-brand-hover text-white px-5 py-2 rounded-full font-bold text-sm transition-all hover:scale-105 shadow-lg shadow-brand-accent/20"
		>
			+ Нова добірка
		</button>
	</div>

	{#if collections.length === 0}
		<div class="bg-bkg-header/80 border border-gray-800 rounded-2xl p-12 text-center">
			<p class="text-text-muted text-sm mb-4">У вас ще немає добірок</p>
			<button
				onclick={() => (showCreateModal = true)}
				class="text-brand-accent hover:underline text-sm font-semibold"
			>
				Створити першу добірку
			</button>
		</div>
	{:else}
		<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
			{#each collections as c (c.id)}
				<a
						href={resolve(`/collections/${c.id}`)}
						class="bg-bkg-header/80 border border-gray-800 rounded-xl p-5 flex flex-col gap-3 hover:border-gray-600 transition-colors block no-underline"
					>
					<div class="flex items-start justify-between gap-2">
						<span class="text-base font-bold text-white/95 group-hover:text-brand-accent transition-colors leading-tight flex-1">
							{c.name}
						</span>
						<span
							class="shrink-0 text-xs px-2 py-0.5 rounded-full border
							       {c.privacyLevel === 'private'
								       ? 'border-gray-600 text-gray-400'
								       : 'border-blue-700 text-blue-400'}"
						>
							{c.privacyLevel === 'private' ? '🔒 Приватна' : '🌐 Публічна'}
						</span>
					</div>

					{#if c.description}
						<p class="text-text-muted text-xs line-clamp-2">{c.description}</p>
					{/if}

					<div class="flex items-center justify-between mt-auto pt-2 border-t border-gray-800">
						<span class="text-xs text-text-muted">{c.itemCount} {c.itemCount === 1 ? 'медіа' : 'медіа'}</span>
						{#if c.name !== DEFAULT_COLLECTION_NAME}
							<button
								onclick={(e) => { e.preventDefault(); e.stopPropagation(); deleteCollection(c.id); }}
								disabled={deletingId === c.id}
								class="text-xs text-red-500 hover:text-red-400 transition-colors disabled:opacity-50"
							>
								{deletingId === c.id ? '...' : 'Видалити'}
							</button>
						{/if}
					</div>
				</a>
			{/each}
		</div>
	{/if}
</div>

{#if showCreateModal}
	<div
		class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
		role="dialog"
		aria-modal="true"
		tabindex={-1}
		onclick={(e) => { if (e.target === e.currentTarget) closeModal(); }}
		onkeydown={(e) => { if (e.key === 'Escape') closeModal(); }}
	>
		<div
			class="w-full max-w-md bg-bkg-header border border-gray-700 rounded-2xl shadow-2xl p-6 flex flex-col gap-4"
		>
			<h2 class="text-lg font-bold text-white/95">Нова добірка</h2>

			{#if createError}
				<p class="text-red-400 text-sm">{createError}</p>
			{/if}

			<div class="flex flex-col gap-1">
				<label class="text-xs text-text-muted font-semibold uppercase tracking-wider" for="cname">Назва</label>
				<input
					id="cname"
					bind:value={createName}
					maxlength="200"
					placeholder="Моя добірка"
					class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white/95 focus:outline-none focus:border-brand-accent"
				/>
			</div>

			<div class="flex flex-col gap-1">
				<label class="text-xs text-text-muted font-semibold uppercase tracking-wider" for="cdesc">Опис (необов'язково)</label>
				<textarea
					id="cdesc"
					bind:value={createDesc}
					maxlength="1000"
					rows="3"
					placeholder="Опишіть добірку..."
					class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white/95 focus:outline-none focus:border-brand-accent resize-none"
				></textarea>
			</div>

			<div class="flex flex-col gap-1">
				<label class="text-xs text-text-muted font-semibold uppercase tracking-wider" for="cprivacy">Приватність</label>
				<SearchableSelect
					id="cprivacy"
					bind:value={createPrivacy}
					placeholder=""
					options={[
						{ value: 'public', label: '🌐 Публічна' },
						{ value: 'private', label: '🔒 Приватна' },
					]}
				/>
			</div>

			<div class="flex gap-3 justify-end pt-2">
				<button
					onclick={closeModal}
					class="px-4 py-2 text-sm text-gray-400 hover:text-white/95 transition-colors"
				>
					Скасувати
				</button>
				<button
					onclick={createCollection}
					disabled={creating}
					class="bg-brand-accent hover:bg-brand-hover text-white px-5 py-2 rounded-lg font-bold text-sm transition-colors disabled:opacity-50"
				>
					{creating ? 'Створення...' : 'Створити'}
				</button>
			</div>
		</div>
	</div>
{/if}
