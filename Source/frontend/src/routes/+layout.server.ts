import type { LayoutServerLoad } from './$types';
import { api } from '$lib/api';

export const load: LayoutServerLoad = async ({ locals, cookies, depends }) => {
	depends('app:user');
	let profilePicUrl: string | undefined;

	let bio: string | undefined;
	let memberSinceYear: number | undefined;

	if (locals.user) {
		try {
			const token = cookies.get('accessToken');
			const profile = await api.getProfile(locals.user.username, token);
			const p = profile as unknown as Record<string, unknown>;
			profilePicUrl = (p['profilePicUrl'] as string | undefined) || undefined;
			bio = (p['bio'] as string | undefined) || undefined;
			memberSinceYear = (p['memberSinceYear'] as number | undefined) || undefined;
		} catch (e: unknown) {
			const status = (e as { status?: number })?.status;
			if (status === 400 || status === 404) {
				cookies.delete('accessToken', { path: '/' });
				cookies.delete('refreshToken', { path: '/' });
				return { user: null };
			}
			// other errors (network, 5xx): non-fatal — header shows default avatar
		}
	}

	return {
		user: locals.user ? { ...locals.user, profilePicUrl, bio, memberSinceYear } : null
	};
};
