import { describe, it, expect } from 'vitest';
import { getAvatarUrl } from './avatar';

describe('getAvatarUrl', () => {
	it('returns absolute https URL as-is', () => {
		const url = 'https://cdn.example.com/avatars/alice.png';
		expect(getAvatarUrl('alice', url)).toBe(url);
	});

	it('returns absolute http URL as-is', () => {
		const url = 'http://cdn.example.com/a.png';
		expect(getAvatarUrl('alice', url)).toBe(url);
	});

	it('returns rooted path as-is', () => {
		expect(getAvatarUrl('alice', '/static/a.png')).toBe('/static/a.png');
	});

	it('prefixes bare filename with /api/profiles/avatar/', () => {
		expect(getAvatarUrl('alice', 'abc123.jpg')).toBe('/api/profiles/avatar/abc123.jpg');
	});

	it('URL-encodes special characters in bare filename', () => {
		expect(getAvatarUrl('alice', 'a b.png')).toBe('/api/profiles/avatar/a%20b.png');
	});

	it('falls back to ui-avatars with encoded name when picUrl is null', () => {
		const out = getAvatarUrl('Аліса', null);
		expect(out).toMatch(/^https:\/\/ui-avatars\.com\/api\/\?/);
		expect(out).toMatch(/name=%D0%90/);
		expect(out).toMatch(/size=64/);
	});

	it('honors custom size in fallback', () => {
		const out = getAvatarUrl('alice', undefined, 128);
		expect(out).toMatch(/size=128/);
	});
});
