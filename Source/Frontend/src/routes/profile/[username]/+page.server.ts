import type { PageServerLoad } from './$types';
import { api } from '$lib/api';
import type { ProfileTrackingItem } from '$lib/types/trackingTypes';
import type { CollectionResponseDto } from '$lib/types/collectionTypes';

export const load: PageServerLoad = async ({ params, locals, cookies }) => {
	const token = cookies.get('accessToken');
	const profile = await api.getProfile(params.username, token);
	const isOwnProfile = locals.user?.username === params.username;

	let tracking: ProfileTrackingItem[] = [];
	try {
		tracking = await api.getProfileTracking(params.username, token);
	} catch {
		// non-fatal — tracking tab shows empty state
	}

	let collections: CollectionResponseDto[] = [];
	if (profile.id) {
		try {
			const paged = await api.getUserCollections(profile.id, token);
			collections = paged?.items ?? [];
		} catch {
			// non-fatal — collections tab shows empty state
		}
	}

	return { profile, isOwnProfile, token, tracking, collections, userId: locals.user?.id };
};
