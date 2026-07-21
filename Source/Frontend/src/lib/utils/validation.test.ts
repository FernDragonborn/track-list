import { describe, it, expect } from 'vitest';
import { required, email, minLength, passwordMatch, validate } from './validation';

describe('required', () => {
	it('rejects empty string', () => {
		expect(required('')).toBe("Це поле є обов'язковим");
	});

	it('rejects whitespace-only', () => {
		expect(required('   ')).toBe("Це поле є обов'язковим");
	});

	it('accepts any non-blank value', () => {
		expect(required('x')).toBeNull();
	});
});

describe('email', () => {
	it('accepts a well-formed address', () => {
		expect(email('a@b.co')).toBeNull();
	});

	it('rejects missing @', () => {
		expect(email('not-an-email')).toBe('Невірний формат email');
	});

	it('rejects missing TLD', () => {
		expect(email('a@b')).toBe('Невірний формат email');
	});

	it('rejects whitespace inside', () => {
		expect(email('a b@c.d')).toBe('Невірний формат email');
	});
});

describe('minLength', () => {
	it('returns null when length is exactly at limit', () => {
		expect(minLength(3)('abc')).toBeNull();
	});

	it('rejects when below limit with default message', () => {
		expect(minLength(5)('abc')).toBe('Мінімальна довжина: 5 символів');
	});

	it('uses custom message when provided', () => {
		expect(minLength(8, 'Закоротко')('a')).toBe('Закоротко');
	});
});

describe('passwordMatch', () => {
	it('accepts identical values', () => {
		expect(passwordMatch('secret', 'secret')).toBeNull();
	});

	it('rejects mismatched values', () => {
		expect(passwordMatch('a', 'b')).toBe('Паролі не співпадають');
	});
});

describe('validate (rule chain)', () => {
	it('returns first error in order', () => {
		const result = validate('', [required, email]);
		expect(result).toBe("Це поле є обов'язковим");
	});

	it('skips earlier rules and returns later error', () => {
		const result = validate('not-an-email', [required, email]);
		expect(result).toBe('Невірний формат email');
	});

	it('returns null when all rules pass', () => {
		expect(validate('a@b.co', [required, email])).toBeNull();
	});

	it('passes confirmValue through to rules that need it', () => {
		expect(validate('p1', [passwordMatch], 'p1')).toBeNull();
		expect(validate('p1', [passwordMatch], 'p2')).toBe('Паролі не співпадають');
	});
});
