<script lang="ts">
	import { untrack } from 'svelte';
	import { enhance } from '$app/forms';
	import { resolve } from '$app/paths';
	import { Validator, RequiredStrategy, MinLengthStrategy, EmailStrategy } from '$lib/utils/validation';
	import SearchableSelect from '$lib/components/SearchableSelect.svelte';
	import type { SubmitFunction } from '@sveltejs/kit';
	import type { ProfileDto } from '$lib/types/profileTypes';

	let { data, form } = $props<{
		data: { profile: ProfileDto };
		form: {
			message?: string;
			passwordMessage?: string;
			passwordSuccess?: boolean;
			avatarUploaded?: boolean;
		} | null;
	}>();

	let username = $state(untrack(() => data.profile.username ?? ''));
	let email = $state(untrack(() => data.profile.email ?? ''));
	let displayName = $state(untrack(() => data.profile.displayName ?? data.profile.username));
	let bio = $state(untrack(() => data.profile.bio ?? ''));
	let profilePicUrl = $state(untrack(() => data.profile.profilePicUrl ?? ''));
	let country = $state(untrack(() => data.profile.country ?? ''));
	let gender = $state(untrack(() => data.profile.gender ?? 'male'));

	let isLoading = $state(false);
	let isPasswordLoading = $state(false);
	let clientErrors = $state({ displayName: '', username: '', email: '' });
	let avatarError = $state(false);

	// Avatar upload state
	let avatarUploading = $state(false);
	let fileInputEl: HTMLInputElement | undefined;

	async function onFileSelect(e: Event) {
		const input = e.target as HTMLInputElement;
		const file = input.files?.[0];
		if (!file) return;

		// Reset URL field — file upload takes precedence
		profilePicUrl = '';
		avatarError = false;

		avatarUploading = true;
		const fd = new FormData();
		fd.append('avatar', file, file.name);

		try {
			const res = await fetch('?/uploadAvatar', { method: 'POST', body: fd });
			const result = await res.json();
			if (result.type === 'success') {
				window.location.reload();
			}
		} finally {
			avatarUploading = false;
			input.value = '';
		}
	}

	const bioMaxLength = 300;

	const GENDER_OPTIONS = [
		{ value: 'male', label: 'Чоловік' },
		{ value: 'female', label: 'Жінка' },
		{ value: 'nonbinary', label: 'Небінарний/а' },
		{ value: 'other', label: 'Інший' },
	];

	const COUNTRIES = [
		'', 'Afghanistan', 'Albania', 'Algeria', 'Argentina', 'Armenia', 'Australia',
		'Austria', 'Azerbaijan', 'Bangladesh', 'Belarus', 'Belgium', 'Bolivia',
		'Bosnia and Herzegovina', 'Brazil', 'Bulgaria', 'Canada', 'Chile', 'China',
		'Colombia', 'Croatia', 'Czech Republic', 'Denmark', 'Ecuador', 'Egypt',
		'Estonia', 'Ethiopia', 'Finland', 'France', 'Georgia', 'Germany', 'Ghana',
		'Greece', 'Hungary', 'India', 'Indonesia', 'Iran', 'Iraq', 'Ireland',
		'Israel', 'Italy', 'Japan', 'Jordan', 'Kazakhstan', 'Kenya', 'Latvia',
		'Lithuania', 'Luxembourg', 'Malaysia', 'Mexico', 'Moldova', 'Morocco',
		'Netherlands', 'New Zealand', 'Nigeria', 'Norway', 'Pakistan', 'Peru',
		'Philippines', 'Poland', 'Portugal', 'Romania', 'Russia', 'Saudi Arabia',
		'Serbia', 'Singapore', 'Slovakia', 'Slovenia', 'South Africa', 'South Korea',
		'Spain', 'Sri Lanka', 'Sweden', 'Switzerland', 'Syria', 'Taiwan', 'Thailand',
		'Tunisia', 'Turkey', 'Ukraine', 'United Arab Emirates', 'United Kingdom',
		'United States', 'Uzbekistan', 'Venezuela', 'Vietnam',
	];

	const fallbackAvatarUrl = $derived(
		`https://ui-avatars.com/api/?name=${encodeURIComponent(displayName || data.profile.username)}&background=ff3d5e&color=fff&size=96`,
	);

	const submitHandler: SubmitFunction = ({ cancel }) => {
		const errName = Validator.validate(displayName, [RequiredStrategy, MinLengthStrategy(2)]);
		const errUsername = Validator.validate(username, [RequiredStrategy, MinLengthStrategy(3)]);
		const errEmail = email ? Validator.validate(email, [EmailStrategy]) : null;
		clientErrors.displayName = errName || '';
		clientErrors.username = errUsername || '';
		clientErrors.email = errEmail || '';
		if (errName || errUsername || errEmail) { cancel(); return; }

		isLoading = true;
		return async ({ update }) => {
			isLoading = false;
			await update();
		};
	};

	const passwordHandler: SubmitFunction = () => {
		isPasswordLoading = true;
		return async ({ update }) => {
			isPasswordLoading = false;
			await update();
		};
	};
</script>

<div class="min-h-[80vh] flex items-center justify-center px-4 py-12">
	<div class="w-full max-w-lg space-y-6">

		<!-- Profile info card -->
		<div class="bg-bkg-header/80 backdrop-blur-sm border border-gray-800 rounded-2xl shadow-2xl p-8">
			<h1 class="text-2xl font-black text-white/95 mb-6">Редагувати профіль</h1>

			<form method="POST" action="?/save" use:enhance={submitHandler} class="space-y-5" novalidate>
				<!-- Нікнейм -->
				<div>
					<label for="username" class="block text-sm font-medium text-text-muted mb-1">Нікнейм</label>
					<input
						id="username"
						name="username"
						type="text"
						bind:value={username}
						oninput={() => (clientErrors.username = '')}
						onblur={() =>
							(clientErrors.username =
								Validator.validate(username, [RequiredStrategy, MinLengthStrategy(3)]) || '')}
						required
						class="w-full bg-bkg-main text-white/95 px-4 py-3 rounded-lg border {clientErrors.username
							? 'border-red-500'
							: 'border-gray-700'} focus:border-brand-accent focus:ring-1 focus:ring-brand-accent outline-none transition-all"
					/>
					{#if clientErrors.username}
						<p class="text-red-400 text-xs mt-1">{clientErrors.username}</p>
					{/if}
				</div>

				<!-- Email -->
				<div>
					<label for="email" class="block text-sm font-medium text-text-muted mb-1">Email</label>
					<input
						id="email"
						name="email"
						type="email"
						bind:value={email}
						oninput={() => (clientErrors.email = '')}
						onblur={() =>
							(clientErrors.email = email ? Validator.validate(email, [EmailStrategy]) || '' : '')}
						class="w-full bg-bkg-main text-white/95 px-4 py-3 rounded-lg border {clientErrors.email
							? 'border-red-500'
							: 'border-gray-700'} focus:border-brand-accent focus:ring-1 focus:ring-brand-accent outline-none transition-all placeholder-gray-600"
					/>
					{#if clientErrors.email}
						<p class="text-red-400 text-xs mt-1">{clientErrors.email}</p>
					{/if}
				</div>

				<!-- Ім'я -->
				<div>
					<label for="displayName" class="block text-sm font-medium text-text-muted mb-1">Ім'я</label>
					<input
						id="displayName"
						name="displayName"
						type="text"
						bind:value={displayName}
						oninput={() => (clientErrors.displayName = '')}
						onblur={() =>
							(clientErrors.displayName =
								Validator.validate(displayName, [RequiredStrategy, MinLengthStrategy(2)]) || '')}
						required
						placeholder="Ваше ім'я"
						class="w-full bg-bkg-main text-white/95 px-4 py-3 rounded-lg border {clientErrors.displayName
							? 'border-red-500'
							: 'border-gray-700'} focus:border-brand-accent focus:ring-1 focus:ring-brand-accent outline-none transition-all placeholder-gray-600"
					/>
					{#if clientErrors.displayName}
						<p class="text-red-400 text-xs mt-1">{clientErrors.displayName}</p>
					{/if}
				</div>

				<!-- Біо -->
				<div>
					<div class="flex justify-between items-center mb-1">
						<label for="bio" class="block text-sm font-medium text-text-muted">Біо</label>
						<span class="text-xs text-text-muted">{bio.length}/{bioMaxLength}</span>
					</div>
					<textarea
						id="bio"
						name="bio"
						bind:value={bio}
						maxlength={bioMaxLength}
						rows={4}
						placeholder="Розкажіть про себе..."
						class="w-full bg-bkg-main text-white/95 px-4 py-3 rounded-lg border border-gray-700 focus:border-brand-accent focus:ring-1 focus:ring-brand-accent outline-none transition-all placeholder-gray-600 resize-none"
					></textarea>
				</div>

				<!-- Країна -->
				<div>
					<label for="country" class="block text-sm font-medium text-text-muted mb-1">Країна</label>
					<SearchableSelect
						id="country"
						name="country"
						bind:value={country}
						placeholder="— Не вказано —"
						options={COUNTRIES.slice(1).map((c) => ({ value: c, label: c }))}
					/>
				</div>

				<!-- Стать -->
				<div>
					<label for="gender" class="block text-sm font-medium text-text-muted mb-1">Стать</label>
					<SearchableSelect
						id="gender"
						name="gender"
						bind:value={gender}
						placeholder=""
						options={GENDER_OPTIONS}
					/>
				</div>

				<!-- Аватар -->
				<div>
					<p class="block text-sm font-medium text-text-muted mb-2">Аватар</p>

					<!-- Preview -->
					<div class="flex justify-center mb-4">
						{#key profilePicUrl}
							<img
								src={avatarError || !profilePicUrl ? fallbackAvatarUrl : profilePicUrl}
								alt="Аватар"
								width="96"
								height="96"
								class="w-24 h-24 rounded-full object-cover border-2 border-brand-accent shadow-lg shadow-brand-accent/20"
								onerror={() => (avatarError = true)}
							/>
						{/key}
					</div>

					<!-- Option 1: Upload file -->
					<div class="space-y-2 mb-4">
						<p class="text-xs text-text-muted uppercase tracking-wider">Завантажити файл</p>
						<button
							type="button"
							onclick={() => fileInputEl?.click()}
							disabled={avatarUploading}
							class="w-full bg-gray-700 hover:bg-gray-600 disabled:opacity-50 text-white/95 px-4 py-2.5 rounded-lg text-sm font-semibold transition-colors"
						>
							{avatarUploading ? 'Завантаження...' : 'Обрати файл (JPG, PNG)'}
						</button>
						<input
							bind:this={fileInputEl}
							type="file"
							accept=".jpg,.jpeg,.png"
							onchange={onFileSelect}
							class="hidden"
						/>
					</div>

					<!-- Divider -->
					<div class="flex items-center gap-2 mb-4">
						<div class="flex-1 h-px bg-gray-700"></div>
						<span class="text-xs text-text-muted">або</span>
						<div class="flex-1 h-px bg-gray-700"></div>
					</div>

					<!-- Option 2: DiceBear preset styles -->
					<div class="space-y-2 mb-4">
						<p class="text-xs text-text-muted uppercase tracking-wider">Стиль DiceBear</p>
						<div class="grid grid-cols-6 gap-2">
							{#each ['avataaars','lorelei','bottts','identicon','shapes','personas'] as style (style)}
								{@const seed = (data.profile.id ?? data.profile.username ?? 'me').replace(/-/g, '')}
								{@const url = `https://api.dicebear.com/7.x/${style}/png?seed=${encodeURIComponent(seed)}&size=200`}
								<button
									type="button"
									onclick={() => { profilePicUrl = url; avatarError = false; }}
									class="aspect-square rounded-lg overflow-hidden border-2 transition-all hover:scale-105 {profilePicUrl === url ? 'border-brand-accent ring-2 ring-brand-accent/40' : 'border-gray-700 hover:border-gray-500'}"
									title={style}
								>
									<img src={url} alt={style} class="w-full h-full object-cover" loading="lazy" />
								</button>
							{/each}
						</div>
						<p class="text-[11px] text-text-muted">Клік обирає стиль. Збереже при натисканні «Зберегти».</p>
					</div>

					<!-- Divider -->
					<div class="flex items-center gap-2 mb-4">
						<div class="flex-1 h-px bg-gray-700"></div>
						<span class="text-xs text-text-muted">або</span>
						<div class="flex-1 h-px bg-gray-700"></div>
					</div>

					<!-- Option 3: URL -->
					<div class="space-y-2">
						<label for="profilePicUrl" class="text-xs text-text-muted uppercase tracking-wider block">Посилання на зображення</label>
						<input
							id="profilePicUrl"
							name="profilePicUrl"
							type="text"
							bind:value={profilePicUrl}
							oninput={() => (avatarError = false)}
							placeholder="https://example.com/avatar.jpg"
							class="w-full bg-bkg-main text-white/95 px-4 py-3 rounded-lg border border-gray-700 focus:border-brand-accent focus:ring-1 focus:ring-brand-accent outline-none transition-all placeholder-gray-600 text-sm"
						/>
						<p class="text-[11px] text-text-muted">URL зберігається при натисканні «Зберегти» нижче.</p>
					</div>

					{#if form?.avatarUploaded}
						<p class="text-green-400 text-xs mt-3 text-center">Аватар оновлено</p>
					{/if}
				</div>

				{#if form?.message}
					<div class="p-3 rounded bg-red-500/10 border border-red-500/50 text-red-200 text-sm text-center">
						{form.message}
					</div>
				{/if}

				<!-- Buttons -->
				<div class="flex gap-3 pt-2">
					<button
						type="submit"
						disabled={isLoading}
						class="flex-1 bg-brand-accent hover:bg-brand-hover text-white/95 font-bold py-3 rounded-lg transition-all transform active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-brand-accent/20"
					>
						{#if isLoading}
							<span class="inline-block w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin mr-2"></span>
							Збереження...
						{:else}
							Зберегти
						{/if}
					</button>
					<a
						href={resolve(`/profile/${data.profile.username}`)}
						class="flex-1 text-center py-3 rounded-lg border border-gray-600 text-text-muted hover:border-brand-accent hover:text-white transition-all font-semibold"
					>
						Скасувати
					</a>
				</div>
			</form>
		</div>

		<!-- Password change card -->
		<div class="bg-bkg-header/80 backdrop-blur-sm border border-gray-800 rounded-2xl shadow-2xl p-8">
			<h2 class="text-xl font-black text-white/95 mb-6">Змінити пароль</h2>

			{#if form?.passwordSuccess}
				<div class="mb-4 p-3 rounded bg-green-500/10 border border-green-500/50 text-green-300 text-sm text-center">
					Пароль успішно змінено
				</div>
			{/if}

			{#if form?.passwordMessage}
				<div class="mb-4 p-3 rounded bg-red-500/10 border border-red-500/50 text-red-200 text-sm text-center">
					{form.passwordMessage}
				</div>
			{/if}

			<form method="POST" action="?/changePassword" use:enhance={passwordHandler} class="space-y-5" novalidate>
				<div>
					<label for="currentPassword" class="block text-sm font-medium text-text-muted mb-1">Поточний пароль</label>
					<input
						id="currentPassword"
						name="currentPassword"
						type="password"
						required
						placeholder="••••••••"
						class="w-full bg-bkg-main text-white/95 px-4 py-3 rounded-lg border border-gray-700 focus:border-brand-accent focus:ring-1 focus:ring-brand-accent outline-none transition-all placeholder-gray-600"
					/>
				</div>

				<div>
					<label for="newPassword" class="block text-sm font-medium text-text-muted mb-1">Новий пароль</label>
					<input
						id="newPassword"
						name="newPassword"
						type="password"
						required
						placeholder="••••••••"
						class="w-full bg-bkg-main text-white/95 px-4 py-3 rounded-lg border border-gray-700 focus:border-brand-accent focus:ring-1 focus:ring-brand-accent outline-none transition-all placeholder-gray-600"
					/>
				</div>

				<div>
					<label for="confirmPassword" class="block text-sm font-medium text-text-muted mb-1">Підтвердіть новий пароль</label>
					<input
						id="confirmPassword"
						name="confirmPassword"
						type="password"
						required
						placeholder="••••••••"
						class="w-full bg-bkg-main text-white/95 px-4 py-3 rounded-lg border border-gray-700 focus:border-brand-accent focus:ring-1 focus:ring-brand-accent outline-none transition-all placeholder-gray-600"
					/>
				</div>

				<button
					type="submit"
					disabled={isPasswordLoading}
					class="w-full bg-white hover:bg-gray-200 text-black font-bold py-3 rounded-lg transition-all transform active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed"
				>
					{#if isPasswordLoading}
						<span class="inline-block w-4 h-4 border-2 border-black border-t-transparent rounded-full animate-spin mr-2"></span>
						Збереження...
					{:else}
						Змінити пароль
					{/if}
				</button>
			</form>
		</div>

	</div>
</div>

