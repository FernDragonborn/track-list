<script lang="ts">
	import { enhance } from '$app/forms';
	import PasswordInput from '$lib/components/PasswordInput.svelte';

	let { form } = $props();

	let username = $state('admin');
	let email = $state('');
	let password = $state('');
	let confirmPassword = $state('');
	let setupToken = $state('');
	let isLoading = $state(false);

	import type { SubmitFunction } from '@sveltejs/kit';

	const submitHandler: SubmitFunction = () => {
		isLoading = true;
		return async ({ update }) => {
			isLoading = false;
			await update();
		};
	};
</script>

<div class="flex min-h-[80vh] items-center justify-center">
	<div class="w-full max-w-md rounded-xl border border-gray-800 bg-bkg-header p-8 shadow-2xl">
		<h1 class="mb-6 text-center text-3xl font-black tracking-wide text-white/95">
			Перший адміністратор
		</h1>

		{#if form?.message}
			<div class="mb-4 rounded border border-red-500/50 bg-red-500/10 p-3 text-center text-sm text-red-200">
				{form.message}
			</div>
		{/if}

		<form method="POST" use:enhance={submitHandler} class="space-y-5" novalidate>
			<div>
				<label for="username" class="mb-1 block text-sm font-medium text-text-muted">Нікнейм</label>
				<input
					id="username"
					name="username"
					type="text"
					bind:value={username}
					required
					class="w-full rounded-lg border border-gray-700 bg-bkg-main px-4 py-3 text-white/95 outline-none transition-all focus:border-brand-accent focus:ring-1 focus:ring-brand-accent"
				/>
			</div>

			<div>
				<label for="email" class="mb-1 block text-sm font-medium text-text-muted">Email</label>
				<input
					id="email"
					name="email"
					type="email"
					bind:value={email}
					required
					class="w-full rounded-lg border border-gray-700 bg-bkg-main px-4 py-3 text-white/95 outline-none transition-all focus:border-brand-accent focus:ring-1 focus:ring-brand-accent"
				/>
			</div>

			<div>
				<label for="password" class="mb-1 block text-sm font-medium text-text-muted">Пароль</label>
				<PasswordInput id="password" name="password" bind:value={password} required />
			</div>

			<div>
				<label for="confirm_password" class="mb-1 block text-sm font-medium text-text-muted">
					Підтвердити пароль
				</label>
				<PasswordInput
					id="confirm_password"
					name="confirm_password"
					bind:value={confirmPassword}
					required
				/>
			</div>

			<div>
				<label for="setup_token" class="mb-1 block text-sm font-medium text-text-muted">
					Setup token
				</label>
				<input
					id="setup_token"
					name="setup_token"
					type="password"
					bind:value={setupToken}
					autocomplete="one-time-code"
					class="w-full rounded-lg border border-gray-700 bg-bkg-main px-4 py-3 text-white/95 outline-none transition-all focus:border-brand-accent focus:ring-1 focus:ring-brand-accent"
				/>
			</div>

			<button
				type="submit"
				disabled={isLoading}
				class="w-full rounded-lg bg-brand-accent px-4 py-3 font-bold text-white transition-colors hover:bg-brand-accent-hover disabled:cursor-not-allowed disabled:opacity-50"
			>
				{isLoading ? 'Створення...' : 'Створити адміністратора'}
			</button>
		</form>
	</div>
</div>
