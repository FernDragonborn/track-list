import { describe, it, expect } from 'vitest';
import { extractError } from './errors';

describe('extractError', () => {
	it('reads body.message from SvelteKit HttpError-shaped object', () => {
		expect(extractError({ status: 400, body: { message: 'Bad request' } })).toBe('Bad request');
	});

	it('reads body.error when body.message is absent', () => {
		expect(extractError({ status: 404, body: { error: 'Not found' } })).toBe('Not found');
	});

	it('reads string body as the message', () => {
		expect(extractError({ status: 500, body: 'Boom' })).toBe('Boom');
	});

	it('reads top-level message when body is absent', () => {
		expect(extractError({ message: 'top-level' })).toBe('top-level');
	});

	it('reads message from a native Error instance', () => {
		expect(extractError(new Error('native'))).toBe('native');
	});

	it('returns default fallback for unknown shapes', () => {
		expect(extractError(42)).toBe('Помилка');
		expect(extractError(null)).toBe('Помилка');
		expect(extractError(undefined)).toBe('Помилка');
	});

	it('uses custom fallback when provided', () => {
		expect(extractError({}, 'Сталося щось')).toBe('Сталося щось');
	});

	it('prefers body.message over top-level message', () => {
		expect(
			extractError({ message: 'outer', body: { message: 'inner' } }),
		).toBe('inner');
	});
});
