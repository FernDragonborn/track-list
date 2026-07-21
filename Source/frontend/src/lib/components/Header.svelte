<script lang="ts">
	import { resolve } from '$app/paths';
	import { enhance } from '$app/forms';
	import type { UserState } from '$lib/stores/user.svelte';
	import { getAvatarUrl } from '$lib/utils/avatar';
	import { selectedLang, SUPPORTED_LANGS } from '$lib/stores/language';
	import Searchbar from './Searchbar.svelte';

	import type { SubmitFunction } from '@sveltejs/kit';

	let { user }: { user: UserState | null } = $props();

	const handleLogout: SubmitFunction = () => {
		return async ({ update }) => {
			await update();
		};
	};


</script>

<header
	class="w-full bg-bkg-header text-text-main py-3 px-6 shadow-md flex items-center justify-between sticky top-0 z-50"
>
	<div class="flex items-center gap-8">
		<a
			href={resolve('/')}
			class="text-2xl font-black tracking-wide hover:text-brand-accent transition-colors uppercase"
		>
			TrackList
		</a>

		<Searchbar />
	</div>

	<nav class="flex items-center gap-6 font-medium text-sm">
		<a
			href={resolve('/catalog')}
			class="text-text-muted hover:text-white/95 transition-colors text-base"
		>
			Каталог
		</a>

		<!-- Language selector — picks UI translation language for external content (Wiki, descriptions, reviews). -->
		<label class="flex items-center gap-1 text-text-muted text-sm" title="Мова перекладу зовнішнього вмісту">
			<svg class="w-4 h-4 opacity-70" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5h12M9 3v2m1.048 9.5A18.022 18.022 0 016.412 9m6.088 9h7M11 21l5-10 5 10M12.751 5C11.783 10.77 8.07 15.61 3 18.129"/></svg>
			<select
				bind:value={$selectedLang}
				class="bg-bkg-main border border-transparent hover:border-gray-600 rounded-md px-2 py-1 text-xs cursor-pointer focus:outline-none focus:border-brand-accent transition-colors"
			>
				{#each SUPPORTED_LANGS as l (l.code)}
					<option value={l.code}>{l.label}</option>
				{/each}
			</select>
		</label>

		<div class="relative group">
			<button
				type="button"
				class="text-text-muted hover:text-white/95 transition-colors text-base flex items-center gap-1"
			>
				Інфо
				<svg width="10" height="10" viewBox="0 0 10 10" fill="currentColor">
					<path d="M5 7L1 3h8L5 7z" />
				</svg>
			</button>
			<div
				class="absolute right-0 top-full pt-2 w-44 invisible opacity-0 pointer-events-none group-hover:visible group-hover:opacity-100 group-hover:pointer-events-auto transition-all duration-200 ease-in-out origin-top-right"
			>
				<div
					class="bg-bkg-header border border-gray-700 rounded-lg shadow-xl overflow-hidden text-sm"
				>
					<div class="py-1">
						<a href={resolve('/about')} class="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white/95 transition-colors">Про нас</a>
						<a href={resolve('/privacy')} class="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white/95 transition-colors">Конфіденційність</a>
						<a href={resolve('/contact')} class="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white/95 transition-colors">Контакти</a>
					</div>
					<div class="border-t border-gray-700 px-4 py-2 text-xs text-text-muted">
						© 2025 TrackList
					</div>
				</div>
			</div>
		</div>

		<div class="h-6 w-px bg-gray-700 hidden sm:block"></div>

		{#if user}
			<div class="relative group">
				<a
					href={resolve('/profile')}
					class="flex items-center gap-3 py-1 px-2 rounded hover:bg-white/10 transition-colors"
				>
					<div class="text-right hidden sm:block">
						<span
							class="block text-white/95 text-sm font-semibold group-hover:text-brand-accent transition-colors"
							>{user.username}</span
						>
					</div>
					<div
						class="w-9 h-9 rounded-full bg-gray-600 overflow-hidden border-2 border-transparent group-hover:border-brand-accent transition-all"
					>
						<img
							src={getAvatarUrl(user.username, user.profilePicUrl, 36)}
							alt="Avatar"
							class="w-full h-full object-cover"
						/>
					</div>
				</a>

				<div
					class="absolute right-0 top-full pt-2 w-56 invisible opacity-0 pointer-events-none group-hover:visible group-hover:opacity-100 group-hover:pointer-events-auto transition-all duration-200 ease-in-out transform origin-top-right"
				>
					<div
						class="bg-bkg-header border border-gray-700 rounded-lg shadow-xl overflow-hidden text-sm"
					>
						<div class="py-1">
							<a
								href={resolve('/profile')}
								class="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white/95 transition-colors"
								>Профіль</a
							>
							<a
								href={resolve(`/profile/${user.username}/edit`)}
								class="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white/95 transition-colors"
								>Налаштування</a
							>
						</div>

						<div class="border-t border-gray-700"></div>

						<div class="py-1">
							<a
								href={resolve('/tracking')}
								class="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white/95 transition-colors"
								>Трекінг</a
							>
							<a
								href={resolve('/collections')}
								class="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white/95 transition-colors"
								>Добірки</a
							>
							<a
								href={resolve('/reviews')}
								class="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white/95 transition-colors"
								>Рецензії</a
							>
							<a
								href={resolve('/following')}
								class="block px-4 py-2 text-gray-300 hover:bg-white/10 hover:text-white/95 transition-colors"
								>Підписки</a
							>
						</div>

						{#if user.role === 'Moderator' || user.role === 'Admin'}
							<div class="border-t border-gray-700"></div>
							<div class="py-1">
								<a
									href={resolve('/moderation')}
									class="block px-4 py-2 text-yellow-400 hover:bg-white/10 hover:text-yellow-300 transition-colors"
								>Модерація</a>
								{#if user.role === 'Admin'}
									<a
										href={resolve('/admin')}
										class="block px-4 py-2 text-red-400 hover:bg-white/10 hover:text-red-300 transition-colors"
									>Адміністрування</a>
								{/if}
							</div>
						{/if}

						<div class="border-t border-gray-700"></div>

						<div class="py-1">
							<form method="POST" action="/auth/logout" use:enhance={handleLogout}>
								<button
									type="submit"
									class="w-full text-left px-4 py-2 text-red-400 hover:bg-white/10 hover:text-red-300 transition-colors cursor-pointer"
								>
									Вихід
								</button>
							</form>
						</div>
					</div>
				</div>
			</div>
		{:else}
			<div class="flex items-center gap-4">
				<a
					href={resolve('/auth/login')}
					class="text-white/95 hover:text-brand-accent transition-colors"
				>
					Вхід
				</a>
				<a
					href={resolve('/auth/register')}
					class="bg-brand-accent hover:bg-brand-hover text-white/95 px-5 py-2 rounded-full font-bold transition-transform hover:scale-105 shadow-lg shadow-brand-accent/20"
				>
					Реєстрація
				</a>
			</div>
		{/if}
	</nav>
</header>
