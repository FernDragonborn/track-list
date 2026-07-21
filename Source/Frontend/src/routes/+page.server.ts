import type { PageServerLoad } from './$types';
import { env } from '$env/dynamic/public';
import { buildApiUrl, unwrapResponse } from '$lib/utils/api-helpers';
import type { FeedItemDto, PagedResponse } from '$lib/types/reviewTypes';

export const load: PageServerLoad = async ({ fetch, cookies, locals, url: pageUrl }) => {
	const BASE_URL = (env.PUBLIC_API_URL || '/api').replace(/\/$/, '');
	const token = cookies.get('accessToken');
	const isLoggedIn = !!locals.user;

	const headers: Record<string, string> = {};
	if (token) headers['Authorization'] = `Bearer ${token}`;

	// Tab from ?tab= query string. Falls back to personal for logged-in, global otherwise.
	const tabParam = pageUrl.searchParams.get('tab');
	const activeTab: 'personal' | 'global' =
		tabParam === 'personal' ? 'personal' :
		tabParam === 'global' ? 'global' :
		(isLoggedIn ? 'personal' : 'global');

	// Persisted across sessions via the `feedHideShort` cookie (set client-side when the toggle is flipped).
	const hideShortPref = cookies.get('feedHideShort') === '1';

	const isPersonal = activeTab === 'personal' && isLoggedIn;
	const endpoint = isPersonal ? 'feed/personal' : 'feed/global';
	const showShortParam = isPersonal ? `&showShort=${!hideShortPref}` : '';
	const url = buildApiUrl(BASE_URL, `${endpoint}?pageNumber=1&pageSize=10${showShortParam}`);

	let items: FeedItemDto[] = [];
	let totalCount = 0;

	try {
		const res = await fetch(url, { headers });
		if (res.ok) {
			const json = await res.json();
			const paged = unwrapResponse<PagedResponse<FeedItemDto>>(json);
			items = paged?.items ?? [];
			totalCount = paged?.totalCount ?? 0;
		}
	} catch {
		// non-fatal — show empty feed
	}

	return {
		feedItems: items,
		totalCount,
		initialTab: activeTab,
		token: token ?? null,
		username: locals.user?.username ?? null,
		hideShortPref,
	};
};
