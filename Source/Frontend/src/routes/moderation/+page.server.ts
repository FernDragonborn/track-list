import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { api } from '$lib/api';
import type { ReportItem, PendingTranslationItem } from '$lib/types/moderationTypes';
import type { PagedResponse } from '$lib/types/reviewTypes';

export const load: PageServerLoad = async ({ locals, cookies }) => {
	if (!locals.user) redirect(302, '/auth/login');
	if (locals.user.role !== 'Moderator' && locals.user.role !== 'Admin') redirect(302, '/');

	const token = cookies.get('accessToken') ?? '';

	let reports: ReportItem[] = [];
	let translations: PendingTranslationItem[] = [];

	await Promise.allSettled([
		api.getReports(token, 'Pending').then((r) => { reports = r; }),
		api.getPendingTranslations(token).then((r: PagedResponse<PendingTranslationItem>) => { translations = r?.items ?? []; }),
	]);

	return { reports, translations, token, isAdmin: locals.user.role === 'Admin' };
};
