import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { env } from '$env/dynamic/public';
import { buildApiUrl, unwrapResponse } from '$lib/utils/api-helpers';
import type { ProfileTrackingItem } from '$lib/types/trackingTypes';

export const load: PageServerLoad = async ({ fetch, cookies, locals }) => {
	if (!locals.user) redirect(302, '/auth/login');

	const BASE_URL = (env.PUBLIC_API_URL || '/api').replace(/\/$/, '');
	const token = cookies.get('accessToken');
	const username = locals.user.username;

	const url = buildApiUrl(BASE_URL, `profiles/${encodeURIComponent(username)}/tracking`);

	let items: ProfileTrackingItem[] = [];
	try {
		const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
		if (res.ok) {
			const json = await res.json();
			items = unwrapResponse<ProfileTrackingItem[]>(json) ?? [];
		}
	} catch {
		// non-fatal — page shows empty state
	}

	return { items, token: token ?? null };
};
