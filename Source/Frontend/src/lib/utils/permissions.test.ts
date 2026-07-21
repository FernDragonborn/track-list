import { describe, it, expect } from 'vitest';
import { canModerate, isOwner } from './permissions';

describe('permissions.canModerate', () => {
	it('returns true for Admin', () => {
		expect(canModerate('Admin')).toBe(true);
	});

	it('returns true for Moderator', () => {
		expect(canModerate('Moderator')).toBe(true);
	});

	it('returns false for User', () => {
		expect(canModerate('User')).toBe(false);
	});

	it('returns false for null', () => {
		expect(canModerate(null)).toBe(false);
	});

	it('is case-sensitive (lowercase admin is not Admin)', () => {
		expect(canModerate('admin')).toBe(false);
		expect(canModerate('moderator')).toBe(false);
	});
});

describe('permissions.isOwner', () => {
	it('returns true when usernames match', () => {
		expect(isOwner('alice', 'alice')).toBe(true);
	});

	it('returns false when usernames differ', () => {
		expect(isOwner('alice', 'bob')).toBe(false);
	});

	it('returns false when currentUsername is null', () => {
		expect(isOwner('alice', null)).toBe(false);
	});

	it('treats empty content owner string as a real value (matches empty current)', () => {
		expect(isOwner('', '')).toBe(true);
	});

	it('is case-sensitive', () => {
		expect(isOwner('Alice', 'alice')).toBe(false);
	});
});
