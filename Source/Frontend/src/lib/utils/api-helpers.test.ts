import { describe, it, expect } from 'vitest';
import { buildApiUrl, unwrapResponse, parseErrorMessage } from './api-helpers';

describe('buildApiUrl', () => {
	it('joins base and path with a single slash', () => {
		expect(buildApiUrl('http://api.example.com', 'users')).toBe('http://api.example.com/users');
	});

	it('trims trailing slash from base', () => {
		expect(buildApiUrl('http://api.example.com/', 'users')).toBe('http://api.example.com/users');
	});

	it('trims leading slash from path', () => {
		expect(buildApiUrl('http://api.example.com', '/users')).toBe('http://api.example.com/users');
	});

	it('handles both slashes present', () => {
		expect(buildApiUrl('http://api.example.com/', '/users')).toBe('http://api.example.com/users');
	});

	it('handles relative base /api', () => {
		expect(buildApiUrl('/api', 'feed/personal')).toBe('/api/feed/personal');
	});
});

describe('unwrapResponse', () => {
	it('unwraps { data: X }', () => {
		expect(unwrapResponse({ data: { id: 1, name: 'x' } })).toEqual({ id: 1, name: 'x' });
	});

	it('returns data array when nested under "data"', () => {
		expect(unwrapResponse({ data: [1, 2, 3] })).toEqual([1, 2, 3]);
	});

	it('returns as-is when there is no data property', () => {
		expect(unwrapResponse({ items: [], total: 0 })).toEqual({ items: [], total: 0 });
	});

	it('returns primitives untouched', () => {
		expect(unwrapResponse('plain')).toBe('plain');
		expect(unwrapResponse(42)).toBe(42);
	});

	it('returns null untouched', () => {
		expect(unwrapResponse(null)).toBeNull();
	});
});

describe('parseErrorMessage', () => {
	it('reads JSON.error field', () => {
		expect(parseErrorMessage('{"error":"Bad"}')).toBe('Bad');
	});

	it('reads JSON.message field when error is absent', () => {
		expect(parseErrorMessage('{"message":"Bad"}')).toBe('Bad');
	});

	it('returns raw text when not JSON', () => {
		expect(parseErrorMessage('plain text error')).toBe('plain text error');
	});

	it('returns fallback for empty text', () => {
		expect(parseErrorMessage('')).toBe('Помилка сервера');
	});

	it('uses custom fallback', () => {
		expect(parseErrorMessage('', 'Помилка')).toBe('Помилка');
	});

	it('returns fallback when JSON has neither error nor message', () => {
		expect(parseErrorMessage('{"x":1}')).toBe('Помилка сервера');
	});
});
