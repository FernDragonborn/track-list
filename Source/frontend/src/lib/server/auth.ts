import { env } from '$env/dynamic/private';
import { env as publicEnv } from '$env/dynamic/public';
import type { Cookies } from '@sveltejs/kit';
import type { TokensResponse } from '$lib/types/userTypes';

const BASE_URL = (publicEnv.PUBLIC_API_URL || '/api').replace(/\/$/, '');

export type SessionUser = {
	username: string;
	email: string;
	role: string;
	id?: string;
};

export const refreshAuthToken = async (
	refreshToken: string,
	fetchFn: typeof fetch = fetch,
): Promise<{ accessToken: string; refreshToken: string } | null> => {
	try {
		const response = await fetchFn(`${BASE_URL}/auth/renewToken`, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ refreshToken }),
		});

		if (response.ok) {
			const data = await response.json();
			return data.data;
		}
	} catch (e) {
		console.error('Refresh token failed', e);
	}
	return null;
};

export const getSessionUser = async (
	accessToken: string,
	fetchFn: typeof fetch = fetch,
): Promise<SessionUser | null> => {
	try {
		const response = await fetchFn(`${BASE_URL}/auth/session`, {
			headers: { Authorization: `Bearer ${accessToken}` },
		});

		if (!response.ok) return null;
		const data = await response.json().catch(() => ({}));
		const raw = (data.data ?? data) as Partial<SessionUser>;
		if (!raw.username) return null;

		return {
			username: String(raw.username),
			email: raw.email ? String(raw.email) : '',
			role: raw.role ? String(raw.role) : 'User',
			id: raw.id ? String(raw.id) : undefined,
		};
	} catch {
		return null;
	}
};

export type AuthResult =
	| { tokens: TokensResponse; error?: never; status?: never }
	| { error: string; status: number; tokens?: never };

export const loginUser = async (
	fetchFn: typeof fetch,
	credentials: { email: string; password: string },
): Promise<AuthResult> => {
	const identifier = credentials.email;
	const isEmail = identifier.includes('@');
	const res = await fetchFn(`${BASE_URL}/auth/login`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify({
			email: isEmail ? identifier : '',
			username: isEmail ? '' : identifier,
			password: credentials.password,
		}),
	});

	const data = await res.json();

	if (!res.ok) {
		const error = data.error || data.message || 'Невірний логін або пароль';
		return { error, status: res.status };
	}

	const tokens = (data.data ?? data) as TokensResponse;
	return { tokens };
};

export const registerUser = async (
	fetchFn: typeof fetch,
	credentials: { username: string; email: string; password: string; confirmPassword: string },
): Promise<AuthResult> => {
	const res = await fetchFn(`${BASE_URL}/profiles/register`, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(credentials),
	});

	const data = await res.json().catch(() => ({}));

	if (!res.ok) {
		const error = data.error || data.message || data.title || 'Помилка реєстрації';
		return { error, status: res.status };
	}

	const tokens = (data.data ?? data) as TokensResponse;
	return { tokens };
};

export const setupFirstAdmin = async (
	fetchFn: typeof fetch,
	credentials: {
		username: string;
		email: string;
		password: string;
		confirmPassword: string;
		setupToken?: string;
	},
): Promise<AuthResult> => {
	const headers: Record<string, string> = { 'Content-Type': 'application/json' };
	if (credentials.setupToken) headers['X-Setup-Token'] = credentials.setupToken;

	const res = await fetchFn(`${BASE_URL}/setup/admin`, {
		method: 'POST',
		headers,
		body: JSON.stringify(credentials),
	});

	const data = await res.json().catch(() => ({}));
	if (!res.ok) {
		const error = data.error || data.message || data.title || 'Setup failed';
		return { error, status: res.status };
	}

	if (!data.data) return { error: data.message || 'Admin created. Please log in.', status: 200 };

	const tokens = data.data as TokensResponse;
	return { tokens };
};

export const setAuthCookies = (cookies: Cookies, accessToken: string, refreshToken?: string) => {
	const secure = env.NODE_ENV === 'production';

	cookies.set('accessToken', accessToken, {
		path: '/',
		httpOnly: true,
		secure,
		sameSite: 'lax',
		maxAge: 60 * 15, // 15 хвилин
	});

	if (refreshToken) {
		cookies.set('refreshToken', refreshToken, {
			path: '/',
			httpOnly: true,
			secure,
			sameSite: 'lax',
			maxAge: 60 * 60 * 24 * 14, // 14 днів
		});
	}
};
