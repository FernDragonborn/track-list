import type { PageServerLoad } from './$types';
import { env } from '$env/dynamic/public';
import { buildApiUrl, unwrapResponse } from '$lib/utils/api-helpers';
import type { MediaEntity } from '$lib/types/searchTypes';
import type { PagedResponse } from '$lib/types/reviewTypes';

export const load: PageServerLoad = async ({ fetch, url }) => {
	const BASE_URL = (env.PUBLIC_API_URL || '/api').replace(/\/$/, '');
	const type = url.searchParams.get('type') ?? '';
	const yearFromStr = url.searchParams.get('yearFrom') ?? '';
	const yearToStr = url.searchParams.get('yearTo') ?? '';
	const sortBy = url.searchParams.get('sortBy') ?? '';
	const genresStr = url.searchParams.get('genres') ?? '';
	const yearFrom = yearFromStr ? parseInt(yearFromStr, 10) : undefined;
	const yearTo = yearToStr ? parseInt(yearToStr, 10) : undefined;
	const genres = genresStr
		? genresStr.split(',').map((s) => parseInt(s.trim(), 10)).filter((n) => !isNaN(n))
		: [];

	const params = new URLSearchParams({ pageNumber: '1', pageSize: '20' });
	if (type) params.set('type', type);
	if (yearFrom) params.set('yearFrom', String(yearFrom));
	if (yearTo) params.set('yearTo', String(yearTo));
	if (sortBy) params.set('sortBy', sortBy);
	if (genres.length > 0) params.set('genres', genres.join(','));
	const apiUrl = buildApiUrl(BASE_URL, `media/catalog?${params.toString()}`);

	let items: MediaEntity[] = [];
	let totalCount = 0;

	try {
		const res = await fetch(apiUrl);
		if (res.ok) {
			const json = await res.json();
			const paged = unwrapResponse<PagedResponse<MediaEntity>>(json);
			items = paged?.items ?? [];
			totalCount = paged?.totalCount ?? 0;
		}
	} catch {
		// non-fatal — show empty catalog
	}

	return {
		items,
		totalCount,
		activeType: type,
		activeYearFrom: yearFrom,
		activeYearTo: yearTo,
		activeSort: sortBy || 'added',
		activeGenres: genres,
	};
};
