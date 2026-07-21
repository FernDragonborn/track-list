import type { PageServerLoad } from './$types';
import { env } from '$env/dynamic/public';
import { buildApiUrl, unwrapResponse } from '$lib/utils/api-helpers';

interface PublicStats {
	users: number;
	media: number;
	movies: number;
	series: number;
	reviews: number;
	reviewsWithText: number;
	comments: number;
	avgRating: number | null;
	computedAt: string;
}

export const load: PageServerLoad = async ({ fetch }) => {
	const BASE_URL = (env.PUBLIC_API_URL || '/api').replace(/\/$/, '');
	let stats: PublicStats | null = null;
	try {
		const res = await fetch(buildApiUrl(BASE_URL, 'stats/public'));
		if (res.ok) stats = unwrapResponse<PublicStats>(await res.json());
	} catch {
		// non-fatal — page works without stats
	}
	return { stats };
};
