import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { env } from '$env/dynamic/public';
import { buildApiUrl, unwrapResponse, parseErrorMessage } from '$lib/utils/api-helpers';
import type { MediaDetails } from '$lib/types/searchTypes';
import type { ReviewItem } from '$lib/types/searchTypes';
import type { TrackingStats } from '$lib/types/trackingTypes';
import type { CollectionResponseDto } from '$lib/types/collectionTypes';

export const load: PageServerLoad = async ({ params, fetch, cookies, locals }) => {
	const BASE_URL = (env.PUBLIC_API_URL || '/api').replace(/\/$/, '');
	const token = cookies.get('accessToken');

	const mediaUrl = buildApiUrl(BASE_URL, `media/${encodeURIComponent(params.id)}`);
	const reviewsUrl = buildApiUrl(BASE_URL, `media/${encodeURIComponent(params.id)}/reviews?pageNumber=1&pageSize=10`);

	const mediaHeaders: Record<string, string> = {};
	const reviewsHeaders: Record<string, string> = {};
	if (token) {
		mediaHeaders['Authorization'] = `Bearer ${token}`;
		reviewsHeaders['Authorization'] = `Bearer ${token}`;
	}

	// Fetch media first — need its database GUID (media.id) for the stats endpoint
	const mediaRes = await fetch(mediaUrl, { headers: mediaHeaders });

	if (mediaRes.status === 404) {
		throw error(404, 'Медіа не знайдено');
	}
	if (!mediaRes.ok) {
		const text = await mediaRes.text();
		throw error(mediaRes.status, parseErrorMessage(text));
	}

	const mediaJson = await mediaRes.json();
	const media = unwrapResponse<MediaDetails>(mediaJson);

	// Fetch reviews, stats, and collections in parallel
	const statsUrl = buildApiUrl(BASE_URL, `trackingstatus/media/${encodeURIComponent(media.id)}/stats`);
	const collectionsUrl = buildApiUrl(BASE_URL, `collections/containing/${encodeURIComponent(media.id)}`);
	const membershipsUrl = token ? buildApiUrl(BASE_URL, `collections/memberships/${encodeURIComponent(media.id)}`) : null;
	const userCollectionsUrl = token && locals.user?.id ? buildApiUrl(BASE_URL, `collections/user/${encodeURIComponent(locals.user.id as string)}?pageNumber=1&pageSize=100`) : null;

	const parallelFetches: Promise<Response>[] = [
		fetch(reviewsUrl, { headers: reviewsHeaders }),
		fetch(statsUrl),
		fetch(collectionsUrl),
		...(membershipsUrl ? [fetch(membershipsUrl, { headers: mediaHeaders })] : []),
		...(userCollectionsUrl ? [fetch(userCollectionsUrl, { headers: mediaHeaders })] : []),
	];
	const [reviewsRes, statsRes, collectionsRes, membershipsRes, userColRes] = await Promise.all(parallelFetches);

	let reviews: ReviewItem[] = [];
	if (reviewsRes.ok) {
		const reviewsJson = await reviewsRes.json();
		const paged = unwrapResponse<{ items: ReviewItem[] }>(reviewsJson);
		reviews = paged?.items ?? [];
	}

	let trackingStats: TrackingStats = { planned: 0, watching: 0, completed: 0, dropped: 0 };
	if (statsRes.ok) {
		const statsJson = await statsRes.json();
		trackingStats = unwrapResponse<TrackingStats>(statsJson) ?? trackingStats;
	}

	let inCollections: CollectionResponseDto[] = [];
	if (collectionsRes.ok) {
		const collectionsJson = await collectionsRes.json();
		inCollections = unwrapResponse<CollectionResponseDto[]>(collectionsJson) ?? [];
	}

	// Merge user's own private collections that contain this media
	if (membershipsRes?.ok && userColRes?.ok) {
		const memberships = unwrapResponse<{ collectionId: string }[]>(await membershipsRes.json()) ?? [];
		const memberIds = new Set(memberships.map((m) => m.collectionId));
		const userCols = unwrapResponse<{ items: CollectionResponseDto[] }>(await userColRes.json())?.items ?? [];
		const privateOwn = userCols.filter((c) => memberIds.has(c.id) && !inCollections.some((x) => x.id === c.id));
		inCollections = [...inCollections, ...privateOwn];
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
