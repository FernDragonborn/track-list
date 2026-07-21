import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { api } from '$lib/api';

export const load: PageServerLoad = async ({ locals, cookies }) => {
	if (!locals.user) redirect(302, '/auth/login');
	if (locals.user.role !== 'Admin') redirect(302, '/');

	const token = cookies.get('accessToken') ?? '';

	const defaultFrom = new Date(Date.now() - 30 * 24 * 60 * 60 * 1000);
	const defaultTo   = new Date();

	const [usersResult, statsResult] = await Promise.allSettled([
		api.getAdminUsers(token, 1, 20, undefined, 'createdAt', true),
		api.getAdminStats(token, defaultFrom, defaultTo),
	]);

	const initialUsers = usersResult.status === 'fulfilled' ? (usersResult.value.items ?? []) : [];
	const initialTotalCount = usersResult.status === 'fulfilled' ? (usersResult.value.totalCount ?? 0) : 0;
	const initialStats = statsResult.status === 'fulfilled' ? statsResult.value : null;

	return { token, initialUsers, initialTotalCount, initialStats };
};
