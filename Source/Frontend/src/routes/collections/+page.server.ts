import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { api } from '$lib/api';
import type { CollectionResponseDto } from '$lib/types/collectionTypes';

export const load: PageServerLoad = async ({ locals, cookies }) => {
	if (!locals.user) redirect(302, '/auth/login');

	const token = cookies.get('accessToken');
	let collections: CollectionResponseDto[] = [];

	try {
		const paged = await api.getUserCollections(locals.user.id as string, token);
		collections = paged?.items ?? [];
	} catch {
		// non-fatal — show empty state
	}

	return { collections, token: token ?? null };
};
