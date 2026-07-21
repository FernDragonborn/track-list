import { describe, it, expect } from 'vitest';
import { safeRedirectPath } from './redirect';

describe('safeRedirectPath', () => {
	it('passes through a simple absolute-on-site path', () => {
		expect(safeRedirectPath('/profile/alice')).toBe('/profile/alice');
	});

	it('preserves query string and hash on a same-site path', () => {
		expect(safeRedirectPath('/media/123?review=abc#review-abc')).toBe(
			'/media/123?review=abc#review-abc',
		);
	});

	it('rejects protocol-relative URL (//evil.com)', () => {
		expect(safeRedirectPath('//evil.com/steal')).toBe('/');
	});

	it('rejects backslash-prefixed path (Windows-style)', () => {
		expect(safeRedirectPath('/foo\\bar')).toBe('/');
	});

	it('rejects absolute https URL', () => {
		expect(safeRedirectPath('https://evil.com/phish')).toBe('/');
	});

	it('rejects javascript: scheme', () => {
		expect(safeRedirectPath('javascript:alert(1)')).toBe('/');
	});

	it('rejects data: scheme', () => {
		expect(safeRedirectPath('data:text/html,<script>alert(1)</script>')).toBe('/');
	});

	it('returns custom fallback when input is null', () => {
		expect(safeRedirectPath(null, '/home')).toBe('/home');
	});

	it('rejects relative path without leading slash', () => {
		expect(safeRedirectPath('foo/bar')).toBe('/');
	});

	it('rejects empty string', () => {
		expect(safeRedirectPath('')).toBe('/');
	});
});
