import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { api } from '$lib/api';
import type { ProfileDto } from '$lib/types/profileTypes';

export const load: PageServerLoad = async ({ locals, cookies }) => {
	const token = cookies.get('accessToken');

	if (!locals.user) {
		throw redirect(302, '/auth/login');
	}

	let following: ProfileDto[] = [];
	try {
		following = await api.getFollowing(locals.user.username, token);
	} catch {
		// return empty list on error
	}

	return { following, token: token ?? null };
};
