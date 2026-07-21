import { describe, it, expect, vi } from 'vitest';

vi.mock('$app/paths', () => ({ resolve: (p: string) => p }));

import { getMediaTitle, getMediaUrl } from './media';

describe('getMediaTitle', () => {
	it('returns "Без назви" for undefined', () => {
		expect(getMediaTitle(undefined)).toBe('Без назви');
	});

	it('returns "Без назви" for empty array', () => {
		expect(getMediaTitle([])).toBe('Без назви');
	});

	it('prefers uk over en when both have titles', () => {
		const t = [
			{ languageCode: 'en', title: 'Inception' },
			{ languageCode: 'uk', title: 'Початок' },
		];
		expect(getMediaTitle(t)).toBe('Початок');
	});

	it('falls back to en when uk title is missing/empty', () => {
		const t = [
			{ languageCode: 'uk', title: '' },
			{ languageCode: 'en', title: 'Inception' },
		];
		expect(getMediaTitle(t)).toBe('Inception');
	});

	it('falls back to any other language when uk and en are absent', () => {
		const t = [{ languageCode: 'fr', title: 'Origine' }];
		expect(getMediaTitle(t)).toBe('Origine');
	});

	it('returns "Без назви" when all translations have empty titles', () => {
		const t = [
			{ languageCode: 'uk', title: '' },
			{ languageCode: 'en', title: '' },
		];
		expect(getMediaTitle(t)).toBe('Без назви');
	});
});

describe('getMediaUrl', () => {
	it('returns /media/<id> when no externalApiId', () => {
		expect(getMediaUrl('abc-123')).toBe('/media/abc-123');
	});

	it('returns /media/external/<externalApiId> when externalApiId is set', () => {
		expect(getMediaUrl('abc-123', 'Tmdb:movie:10867')).toBe('/media/external/Tmdb:movie:10867');
	});

	it('treats null externalApiId as missing', () => {
		expect(getMediaUrl('abc-123', null)).toBe('/media/abc-123');
	});
});
