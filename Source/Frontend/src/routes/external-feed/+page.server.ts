import type { PageServerLoad } from './$types';
import { api } from '$lib/api';

export const load: PageServerLoad = async () => {
	const page = await api.getExternalFeed();
	return { initialItems: page.items, initialCursor: page.nextCursor ?? null };
};
