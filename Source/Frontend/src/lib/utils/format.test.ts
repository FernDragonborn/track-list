import { describe, it, expect } from 'vitest';
import { formatDate } from './format';

describe('formatDate', () => {
	it('formats an ISO date string in uk-UA locale (dd.mm.yyyy)', () => {
		// 1 Jan 2026 — uk-UA renders as "01.01.2026"
		expect(formatDate('2026-01-01T00:00:00Z')).toBe('01.01.2026');
	});

	it('formats a date-only ISO string', () => {
		expect(formatDate('2026-06-09')).toBe('09.06.2026');
	});

	it('returns "Invalid Date" for unparseable input (documents the contract)', () => {
		expect(formatDate('not a date')).toBe('Invalid Date');
	});
});
