import type { PageServerLoad } from './$types';
import { error } from '@sveltejs/kit';
import { api } from '$lib/api';

export const load: PageServerLoad = async ({ params, url }) => {
	const source = url.searchParams.get('source') ?? 'letterboxd';
	try {
		const profile = await api.getExternalReviewerProfile(params.handle, source, 10);
		return { profile };
	} catch (e: unknown) {
		const status = (e as { status?: number })?.status;
		if (status === 404) {
			throw error(404, 'Зовнішнього критика не знайдено');
		}
		throw e;
	}
};
