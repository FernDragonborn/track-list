import { redirect } from '@sveltejs/kit';
import type { Actions } from './$types';

export const actions: Actions = {
	default: async ({ cookies, fetch }) => {
		const accessToken = cookies.get('accessToken');
		if (accessToken) {
			try {
				await fetch('/api/auth/logout', {
					method: 'POST',
					headers: { Authorization: `Bearer ${accessToken}` },
				});
			} catch {
				// Cookies are cleared locally even if the server-side session is already gone.
			}
		}

		cookies.delete('accessToken', { path: '/' });
		cookies.delete('refreshToken', { path: '/' });
		
		throw redirect(303, '/auth/login');
	}
};
