import { fail, redirect } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import { setAuthCookies, setupFirstAdmin } from '$lib/server/auth';

export const load: PageServerLoad = async ({ locals }) => {
	if (locals.user) throw redirect(303, '/profile');
	return {};
};

export const actions: Actions = {
	default: async ({ request, cookies, fetch }) => {
		const formData = await request.formData();
		const username = String(formData.get('username') ?? '').trim();
		const email = String(formData.get('email') ?? '').trim();
		const password = String(formData.get('password') ?? '');
		const confirmPassword = String(formData.get('confirm_password') ?? '');
		const setupToken = String(formData.get('setup_token') ?? '').trim();

		if (!username || !email || !password || !confirmPassword) {
			return fail(400, { message: "Всі поля обов'язкові", username, email });
		}
		if (password !== confirmPassword) {
			return fail(400, { message: 'Паролі не співпадають', username, email });
		}

		const result = await setupFirstAdmin(fetch, {
			username,
			email,
			password,
			confirmPassword,
			setupToken: setupToken || undefined,
		});

		if (result.error) {
			return fail(result.status, { message: result.error, username, email });
		}

		if (!result.tokens?.accessToken || !result.tokens.refreshToken) {
			return fail(500, { message: 'Сервер не повернув токени', username, email });
		}

		setAuthCookies(cookies, result.tokens.accessToken, result.tokens.refreshToken);
		throw redirect(303, '/profile');
	},
};
