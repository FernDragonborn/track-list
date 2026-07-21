import { fail } from '@sveltejs/kit';
import type { Actions } from './$types';
import { loginUser, setAuthCookies } from '$lib/server/auth';

export const actions: Actions = {
	login: async ({ request, cookies, fetch }) => {
		const formData = await request.formData();
		const email = formData.get('email') as string;
		const password = formData.get('password') as string;

		if (!email || !password) {
			return fail(400, { email, message: "Всі поля обов'язкові" });
		}

		try {
			const result = await loginUser(fetch, { email, password });

			if (result.error) {
				return fail(result.status, { email, message: result.error });
			}

			const accessToken = result.tokens?.accessToken;
			const refreshToken = result.tokens?.refreshToken;
			if (accessToken && refreshToken) {
				setAuthCookies(cookies, accessToken, refreshToken);
				return { success: true };
			}

			return fail(500, { message: 'Сервер не повернув токени' });
		} catch (error) {
			console.error('Login error:', error);
			return fail(500, { message: "Помилка з'єднання з сервером" });
		}
	},
};
