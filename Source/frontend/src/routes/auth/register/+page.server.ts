import { fail } from '@sveltejs/kit';
import type { Actions } from './$types';
import { registerUser, setAuthCookies } from '$lib/server/auth';

export const actions: Actions = {
	default: async ({ request, cookies, fetch }) => {
		const formData = await request.formData();
		const username = formData.get('username') as string;
		const email = formData.get('email') as string;
		const password = formData.get('password') as string;
		const confirmPassword = formData.get('confirm_password') as string;

		if (password !== confirmPassword) {
			return fail(400, { message: 'Паролі не співпадають', username, email });
		}

		try {
			const result = await registerUser(fetch, { username, email, password, confirmPassword });

			if (result.error) {
				return fail(result.status, { message: result.error, username, email });
			}

			const accessToken = result.tokens?.accessToken;
			const refreshToken = result.tokens?.refreshToken;
			if (accessToken && refreshToken) {
				setAuthCookies(cookies, accessToken, refreshToken);
				return { success: true };
			}

			return fail(500, { message: 'Сервер не повернув токени' });
		} catch (error) {
			console.error('[register] Unexpected error:', error instanceof Error ? error.message : error);
			const message = error instanceof Error ? error.message : 'Помилка сервера';
			return fail(500, { message });
		}
	},
};
