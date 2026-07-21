import { describe, it, expect } from 'vitest';
import { mapUserFromClaims } from './claims';

const MS_EMAIL = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress';
const MS_ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const MS_NAME = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';

describe('mapUserFromClaims', () => {
	it('reads standard claims (email/role/unique_name/id)', () => {
		const out = mapUserFromClaims({
			email: 'a@b.co',
			role: 'Admin',
			unique_name: 'alice',
			id: 'uuid-1',
		});
		expect(out).toEqual({ email: 'a@b.co', role: 'Admin', username: 'alice', id: 'uuid-1' });
	});

	it('falls back to MS XML schema URIs when standard claims are absent', () => {
		const out = mapUserFromClaims({
			[MS_EMAIL]: 'a@b.co',
			[MS_ROLE]: 'Moderator',
			[MS_NAME]: 'alice',
			sub: 'uuid-2',
		});
		expect(out).toEqual({ email: 'a@b.co', role: 'Moderator', username: 'alice', id: 'uuid-2' });
	});

	it('prefers standard claim over MS URI when both present', () => {
		const out = mapUserFromClaims({
			email: 'standard@b.co',
			[MS_EMAIL]: 'ms@b.co',
			role: 'User',
			unique_name: 'std',
		});
		expect(out.email).toBe('standard@b.co');
		expect(out.username).toBe('std');
	});

	it('falls back from unique_name → nameid → name → MS URI', () => {
		expect(mapUserFromClaims({ nameid: 'bob' }).username).toBe('bob');
		expect(mapUserFromClaims({ name: 'carol' }).username).toBe('carol');
		expect(mapUserFromClaims({ [MS_NAME]: 'dave' }).username).toBe('dave');
	});

	it('returns defaults for empty claims object', () => {
		const out = mapUserFromClaims({});
		expect(out).toEqual({ email: '', role: 'User', username: 'User', id: undefined });
	});

	it('uses sub as id when id is absent', () => {
		expect(mapUserFromClaims({ sub: 'sub-uuid' }).id).toBe('sub-uuid');
	});

	it('coerces non-string claim values via String()', () => {
		const out = mapUserFromClaims({ email: 123 as unknown as string });
		expect(out.email).toBe('123');
	});
});
