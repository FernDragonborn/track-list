import { redirect } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import { env } from '$env/dynamic/public';
import { buildApiUrl, unwrapResponse } from '$lib/utils/api-helpers';
import type { FeedItemDto, PagedResponse } from '$lib/types/reviewTypes';

export const load: PageServerLoad = async ({ fetch, cookies, locals, url: reqUrl }) => {
	if (!locals.user) redirect(302, '/auth/login');

	const BASE_URL = (env.PUBLIC_API_URL || '/api').replace(/\/$/, '');
	const token = cookies.get('accessToken');

	const sortBy = reqUrl.searchParams.get('sortBy') ?? 'newest';
	const url = buildApiUrl(BASE_URL, `feed/my?pageNumber=1&pageSize=10&sortBy=${encodeURIComponent(sortBy)}`);

	let items: FeedItemDto[] = [];
	let totalCount = 0;

	try {
		const res = await fetch(url, {
			headers: { Authorization: `Bearer ${token}` },
		});
		if (res.ok) {
			const json = await res.json();
			const paged = unwrapResponse<PagedResponse<FeedItemDto>>(json);
			items = paged?.items ?? [];
			totalCount = paged?.totalCount ?? 0;
		}
	} catch {
		// non-fatal
	}

	return {
		feedItems: items,
		totalCount,
		token: token ?? null,
	};
};
