import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { api } from '$lib/api';
import { refreshAuthToken, setAuthCookies } from '$lib/server/auth';
import { extractError } from '$lib/utils/errors';

export const load: PageServerLoad = async ({ params, locals, cookies }) => {
	const token = cookies.get('accessToken');
	const profile = await api.getProfile(params.username, token);
	if (!locals.user || locals.user.id !== profile.id) {
		throw redirect(303, `/profile/${encodeURIComponent(params.username)}`);
	}
	return { profile };
};

export const actions: Actions = {
	save: async ({ request, cookies, locals, params }) => {
		const formData = await request.formData();
		const username = (formData.get('username') as string)?.trim();
		const email = (formData.get('email') as string)?.trim();
		const displayName = formData.get('displayName') as string;
		const bio = formData.get('bio') as string;
		const profilePicUrl = formData.get('profilePicUrl') as string;
		const country = formData.get('country') as string;
		const gender = formData.get('gender') as string;

		if (!displayName?.trim()) {
			return fail(400, { message: "Ім'я не може бути порожнім", displayName, bio, profilePicUrl });
		}

		const token = cookies.get('accessToken');
		let newUsername = username || params.username;

		try {
			const current = (await api.getProfile(params.username, token)) as unknown as Record<string, unknown>;
			newUsername = username || (current['username'] as string) || params.username;
			await api.put<void>(
				'profiles/me',
				{
					username: username || current['username'],
					email: email || current['email'],
					role: current['role'] ?? locals.user!.role,
					gender: gender || current['gender'] || 'male',
					country: country || current['country'] || '',
					profilePicUrl: profilePicUrl || null,
					displayName: displayName || null,
					bio: bio || null,
				},
				token,
			);
		} catch (e) {
			const message = extractError(e, 'Помилка збереження профілю');
			return fail(500, { message, displayName, bio, profilePicUrl });
		}

		// Username changed → JWT still carries old username; refresh tokens so
		// locals.user reflects the new username on the next request.
		const refreshToken = cookies.get('refreshToken');
		if (refreshToken) {
			const newTokens = await refreshAuthToken(refreshToken);
			if (newTokens) setAuthCookies(cookies, newTokens.accessToken, newTokens.refreshToken);
		}

		throw redirect(303, `/profile/${encodeURIComponent(newUsername)}`);
	},

	changePassword: async ({ request, cookies }) => {
		const formData = await request.formData();
		const currentPassword = formData.get('currentPassword') as string;
		const newPassword = formData.get('newPassword') as string;
		const confirmPassword = formData.get('confirmPassword') as string;

		if (!currentPassword || !newPassword || !confirmPassword) {
			return fail(400, { passwordMessage: 'Заповніть усі поля пароля' });
		}
		if (newPassword !== confirmPassword) {
			return fail(400, { passwordMessage: 'Новий пароль та підтвердження не збігаються' });
		}
		if (newPassword.length < 8) {
			return fail(400, { passwordMessage: 'Пароль має містити щонайменше 8 символів' });
		}

		try {
			const token = cookies.get('accessToken');
			await api.put<void>(
				'profiles/me/password',
				{
					currentPassword,
					newPassword,
					newPasswordConfirmation: confirmPassword,
				},
				token,
			);
		} catch (e) {
			const message = extractError(e, 'Помилка зміни пароля');
			return fail(500, { passwordMessage: message });
		}

		return { passwordSuccess: true };
	},

	uploadAvatar: async ({ request, cookies, locals }) => {
		if (!locals.user) return fail(401, { message: 'Unauthorized' });
		const token = cookies.get('accessToken');
		const fd = await request.formData();
		const file = fd.get('avatar') as File | null;

		if (!file || file.size === 0) {
			return fail(400, { message: 'Файл не обрано' });
		}

		const allowed = ['image/jpeg', 'image/png'];
		if (!allowed.includes(file.type)) {
			return fail(400, { message: 'Дозволені лише JPG та PNG' });
		}

		const email = locals.user.email as string;
		const apiForm = new FormData();
		apiForm.append('Email', email);
		apiForm.append('ProfilePic', file, file.name);

		try {
			await api.postFormData<void>('profiles/me/redactpfp', apiForm, token);
		} catch (e) {
			const message = extractError(e, 'Помилка завантаження аватара');
			return fail(500, { message });
		}

		return { avatarUploaded: true };
	},
};
