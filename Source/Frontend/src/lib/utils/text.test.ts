import { describe, it, expect } from 'vitest';
import { truncate } from './text';

describe('truncate', () => {
	it('returns original when shorter than maxLen', () => {
		expect(truncate('short', 100)).toBe('short');
	});

	it('returns empty string for empty input', () => {
		expect(truncate('', 10)).toBe('');
	});

	it('returns empty string for null-ish input (contract)', () => {
		// runtime null/undefined: function guards with ?? ''
		expect(truncate(null as unknown as string, 10)).toBe('');
	});

	it('with smart=false: cuts at exact maxLen and appends ellipsis', () => {
		const out = truncate('0123456789abcdef', 10, { smart: false });
		expect(out).toBe('0123456789…');
	});

	it('smart (default): cuts at sentence boundary inside last 40% window', () => {
		// 60% of 30 is 18; boundary at idx 20 (". ") is past minBoundary → cut there
		const text = 'First sentence ends. Second sentence begins here.';
		const out = truncate(text, 30);
		expect(out).toBe('First sentence ends.…');
	});

	it('smart: falls back to hard cut when no boundary inside last 40% window', () => {
		// boundary at "abc. def..." position 4, maxLen 20 → minBoundary 12; 4 < 12 → hard cut
		const text = 'abc. defghijklmnopqrstuvwxyz';
		const out = truncate(text, 20);
		expect(out).toBe('abc. defghijklmnopqr…');
	});

	it('trims trailing whitespace before adding ellipsis', () => {
		const out = truncate('hello world      extra', 10, { smart: false });
		expect(out).toBe('hello worl…');
	});
});
