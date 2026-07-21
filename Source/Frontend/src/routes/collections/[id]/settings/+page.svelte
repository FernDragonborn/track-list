<script lang="ts">
	import { resolve } from '$app/paths';
	import { enhance } from '$app/forms';
	import { untrack } from 'svelte';
	import { DEFAULT_COLLECTION_NAME } from '$lib/constants';
	import { setCrumbLabel, clearCrumbLabel } from '$lib/stores/breadcrumb';
	import SearchableSelect from '$lib/components/SearchableSelect.svelte';
	import type { CollectionDetailResponseDto } from '$lib/types/collectionTypes';

	let { data, form } = $props<{
		data: { collection: CollectionDetailResponseDto; token: string | null };
		form: { message?: string; accessMessage?: string; success?: boolean } | null;
	}>();

	let name = $state(untrack(() => data.collection.name));
	let description = $state(untrack(() => data.collection.description ?? ''));
	let privacyLevel = $state<'public' | 'private'>(untrack(() => data.collection.privacyLevel));
	let saving = $state(false);
	let showDeleteConfirm = $state(false);
	let newUsername = $state('');

	// Reset form when navigating between collection settings (same route, different [id]).
	$effect(() => {
		const _id = data.collection.id;
		untrack(() => {
			name = data.collection.name;
			description = data.collection.description ?? '';
			privacyLevel = data.collection.privacyLevel;
			showDeleteConfirm = false;
			newUsername = '';
		});
	});

	$effect(() => {
		const href = `/collections/${data.collection.id}`;
		setCrumbLabel(href, data.collection.name);
		return () => clearCrumbLabel(href);
	});
</script>

<div class="max-w-2xl mx-auto">

	<!-- Edit form -->
	<div class="bg-bkg-header/80 border border-gray-800 rounded-2xl p-6 mb-6">
		<h2 class="text-lg font-bold text-white/95 mb-4">Редагувати добірку</h2>

		{#if form?.success}
			<p class="text-green-400 text-sm mb-4">Збережено</p>
		{/if}
		{#if form?.message}
			<p class="text-red-400 text-sm mb-4">{form.message}</p>
		{/if}

		<form
			method="POST"
			action="?/save"
			use:enhance={() => {
				saving = true;
				return async ({ update }) => {
					saving = false;
					await update({ reset: false });
				};
			}}
			class="flex flex-col gap-4"
		>
			{#if data.collection.name !== DEFAULT_COLLECTION_NAME}
				<div class="flex flex-col gap-1">
					<label class="text-xs text-text-muted font-semibold uppercase tracking-wider" for="name">Назва</label>
					<input
						id="name"
						name="name"
						bind:value={name}
						maxlength="200"
						required
						class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white/95 focus:outline-none focus:border-brand-accent"
					/>
				</div>
			{:else}
				<input type="hidden" name="name" value={name} />
			{/if}

			<div class="flex flex-col gap-1">
				<label class="text-xs text-text-muted font-semibold uppercase tracking-wider" for="description">Опис</label>
				<textarea
					id="description"
					name="description"
					bind:value={description}
					maxlength="1000"
					rows="3"
					class="bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white/95 focus:outline-none focus:border-brand-accent resize-none"
				></textarea>
			</div>

			<div class="flex flex-col gap-1">
				<label class="text-xs text-text-muted font-semibold uppercase tracking-wider" for="privacyLevel">Приватність</label>
				<SearchableSelect
					id="privacyLevel"
					name="privacyLevel"
					bind:value={privacyLevel}
					placeholder=""
					options={[
						{ value: 'public', label: '🌐 Публічна' },
						{ value: 'private', label: '🔒 Приватна' },
					]}
				/>
			</div>

			<div class="flex justify-end">
				<button
					type="submit"
					disabled={saving}
					class="bg-brand-accent hover:bg-brand-hover text-white px-5 py-2 rounded-lg font-bold text-sm transition-colors disabled:opacity-50"
				>
					{saving ? 'Збереження...' : 'Зберегти'}
				</button>
			</div>
		</form>
	</div>

	<!-- Access management (shown for private collections) -->
	{#if privacyLevel === 'private'}
		<div class="bg-bkg-header/80 border border-gray-800 rounded-2xl p-6 mb-6">
			<h2 class="text-lg font-bold text-white/95 mb-4">Доступ</h2>

			{#if form?.accessMessage}
				<p class="text-red-400 text-sm mb-3">{form.accessMessage}</p>
			{/if}

			{#if data.collection.sharedWith.length > 0}
				<ul class="flex flex-col gap-2 mb-4">
					{#each data.collection.sharedWith as access (access.id)}
						<li class="flex items-center justify-between gap-2">
							<a
								href={resolve(`/profile/${access.username}`)}
								class="text-sm text-white/80 hover:text-brand-accent transition-colors"
							>
								@{access.username}
							</a>
							<form method="POST" action="?/revokeAccess" use:enhance>
								<input type="hidden" name="targetUserId" value={access.userId} />
								<button
									type="submit"
									class="text-xs text-red-500 hover:text-red-400 transition-colors"
								>
									Відкликати
								</button>
							</form>
						</li>
					{/each}
				</ul>
			{:else}
				<p class="text-text-muted text-sm mb-4">Ніхто не має доступу</p>
			{/if}

			<form method="POST" action="?/grantAccess" use:enhance class="flex gap-2">
				<input
					name="username"
					bind:value={newUsername}
					placeholder="username"
					class="flex-1 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white/95 focus:outline-none focus:border-brand-accent"
				/>
				<button
					type="submit"
					class="bg-gray-700 hover:bg-gray-600 text-white/95 px-4 py-2 rounded-lg text-sm font-semibold transition-colors"
				>
					Надати
				</button>
			</form>
		</div>
	{/if}

	<!-- Danger zone -->
	{#if data.collection.name !== DEFAULT_COLLECTION_NAME}
	<div class="bg-bkg-header/80 border border-red-900/50 rounded-2xl p-6">
		<h2 class="text-lg font-bold text-red-400 mb-4">Небезпечна зона</h2>

		{#if !showDeleteConfirm}
			<button
				onclick={() => (showDeleteConfirm = true)}
				class="px-4 py-2 rounded-lg border border-red-700 text-red-400 text-sm font-semibold hover:bg-red-900/30 transition-colors"
			>
				Видалити добірку
			</button>
		{:else}
			<p class="text-sm text-white/70 mb-3">Добірку буде видалено назавжди. Продовжити?</p>
			<div class="flex gap-3">
				<form method="POST" action="?/delete" use:enhance>
					<button
						type="submit"
						class="px-4 py-2 rounded-lg bg-red-700 hover:bg-red-600 text-white text-sm font-bold transition-colors"
					>
						Так, видалити
					</button>
				</form>
				<button
					onclick={() => (showDeleteConfirm = false)}
					class="px-4 py-2 text-sm text-gray-400 hover:text-white/95 transition-colors"
				>
					Скасувати
				</button>
			</div>
		{/if}
	</div>
	{/if}
</div>
