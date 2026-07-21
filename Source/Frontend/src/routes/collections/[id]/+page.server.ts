import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { api } from '$lib/api';

export const load: PageServerLoad = async ({ params, locals, cookies }) => {
	const token = cookies.get('accessToken');

	let collection;
	try {
		collection = await api.getCollectionDetail(params.id, token);
	} catch {
		throw error(404, 'Добірку не знайдено');
	}

	return {
		collection,
		token: token ?? null,
		currentUserId: (locals.user?.id as string | undefined) ?? null,
	};
};
