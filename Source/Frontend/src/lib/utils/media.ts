import { resolve } from '$app/paths';
import type { MediaTranslation } from '$lib/types/searchTypes';

/**
 * Extract best title from translations array: uk → en → any → fallback.
 */
export function getMediaTitle(
	translations: Pick<MediaTranslation, 'languageCode' | 'title'>[] | undefined,
): string {
	if (!translations?.length) return 'Без назви';
	const uk = translations.find((t) => t.languageCode === 'uk' && t.title)?.title;
	const en = translations.find((t) => t.languageCode === 'en' && t.title)?.title;
	return uk || en || translations.find((t) => t.title)?.title || 'Без назви';
}

/**
 * Build resolved media URL — external or internal.
 */
export function getMediaUrl(
	id: string,
	externalApiId?: string | null,
): string {
	return resolve(
		externalApiId ? `/media/external/${externalApiId}` : `/media/${id}`,
	);
}
