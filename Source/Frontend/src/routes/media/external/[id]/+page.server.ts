import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { env } from '$env/dynamic/public';
import { buildApiUrl, unwrapResponse, parseErrorMessage } from '$lib/utils/api-helpers';
import type { MediaDetails, ReviewItem } from '$lib/types/searchTypes';
import type { TrackingStats } from '$lib/types/trackingTypes';
import type { CollectionResponseDto } from '$lib/types/collectionTypes';

export const load: PageServerLoad = async ({ params, fetch, cookies, locals }) => {
	const BASE_URL = (env.PUBLIC_API_URL || '/api').replace(/\/$/, '');
	const token = cookies.get('accessToken');

	const mediaUrl = buildApiUrl(BASE_URL, `media/${encodeURIComponent(params.id)}`);

	const headers: Record<string, string> = {};
	if (token) headers['Authorization'] = `Bearer ${token}`;

	const res = await fetch(mediaUrl, { headers });

	if (res.status === 404) {
		throw error(404, 'Медіа не знайдено');
	}
	if (!res.ok) {
		const text = await res.text();
		throw error(res.status, parseErrorMessage(text));
	}

	const json = await res.json();
	const media = unwrapResponse<MediaDetails>(json);

	// Fetch reviews separately once we have the internal media id
	let reviews: ReviewItem[] = [];
	if (media.id) {
		const reviewsUrl = buildApiUrl(BASE_URL, `media/${encodeURIComponent(media.id)}/reviews?pageNumber=1&pageSize=10`);
		try {
			const reviewsRes = await fetch(reviewsUrl, { headers });
			if (reviewsRes.ok) {
				const reviewsJson = await reviewsRes.json();
				const paged = unwrapResponse<{ items: ReviewItem[] }>(reviewsJson);
				reviews = paged?.items ?? [];
			}
		} catch {
			// non-fatal — show page without reviews
		}
	}

	let trackingStats: TrackingStats = { planned: 0, watching: 0, completed: 0, dropped: 0 };
	let inCollections: CollectionResponseDto[] = [];
	if (media.id) {
		const statsUrl = buildApiUrl(BASE_URL, `trackingstatus/media/${encodeURIComponent(media.id)}/stats`);
		const collectionsUrl = buildApiUrl(BASE_URL, `collections/containing/${encodeURIComponent(media.id)}`);
		const membershipsUrl = token && locals.user?.id ? buildApiUrl(BASE_URL, `collections/memberships/${encodeURIComponent(media.id)}`) : null;
		const userCollectionsUrl = token && locals.user?.id ? buildApiUrl(BASE_URL, `collections/user/${encodeURIComponent(locals.user.id as string)}?pageNumber=1&pageSize=100`) : null;
		try {
			const parallelFetches: Promise<Response>[] = [
				fetch(statsUrl),
				fetch(collectionsUrl),
				...(membershipsUrl ? [fetch(membershipsUrl, { headers })] : []),
				...(userCollectionsUrl ? [fetch(userCollectionsUrl, { headers })] : []),
			];
			const [statsRes, collectionsRes, membershipsRes, userColRes] = await Promise.all(parallelFetches);
			if (statsRes.ok) {
				const statsJson = await statsRes.json();
				trackingStats = unwrapResponse<TrackingStats>(statsJson) ?? trackingStats;
			}
			if (collectionsRes.ok) {
				const collectionsJson = await collectionsRes.json();
				inCollections = unwrapResponse<CollectionResponseDto[]>(collectionsJson) ?? [];
			}
			if (membershipsRes?.ok && userColRes?.ok) {
				const memberships = unwrapResponse<{ collectionId: string }[]>(await membershipsRes.json()) ?? [];
				const memberIds = new Set(memberships.map((m) => m.collectionId));
				const userCols = unwrapResponse<{ items: CollectionResponseDto[] }>(await userColRes.json())?.items ?? [];
				const privateOwn = userCols.filter((c) => memberIds.has(c.id) && !inCollections.some((x) => x.id === c.id));
				inCollections = [...inCollections, ...privateOwn];
			}
		} catch {
			// non-fatal
		}
	}

	return {
		media,
		reviews,
		trackingStats,
		inCollections,
		token: token ?? null,
		username: locals.user?.username ?? null,
		userRole: locals.user?.role ?? null,
		currentUserId: (locals.user?.id as string | undefined) ?? null,
	};
};
