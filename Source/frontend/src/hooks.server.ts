import { redirect, type Handle } from '@sveltejs/kit';
import { env as publicEnv } from '$env/dynamic/public';
import { getSessionUser, refreshAuthToken, setAuthCookies } from '$lib/server/auth';

const BASE_URL = (publicEnv.PUBLIC_API_URL || '/api').replace(/\/$/, '');

export const handle: Handle = async ({ event, resolve }) => {
	const accessToken = event.cookies.get('accessToken');
	const refreshToken = event.cookies.get('refreshToken');

	let user = null;

	if (accessToken) {
		user = await getSessionUser(accessToken, event.fetch);
	}

	// Якщо токен невалідний, але є refresh token - пробуємо оновити
	if (!user && refreshToken) {
		try {
			const response = await refreshAuthToken(refreshToken, event.fetch);

			if (response && response.accessToken) {
				user = await getSessionUser(response.accessToken, event.fetch);

				if (user) {
					setAuthCookies(event.cookies, response.accessToken, response.refreshToken);
				} else {
					event.cookies.delete('accessToken', { path: '/' });
					event.cookies.delete('refreshToken', { path: '/' });
				}
			} else {
				// Refresh returned null — backend rejected the token (deleted account, revoked session)
				event.cookies.delete('accessToken', { path: '/' });
				event.cookies.delete('refreshToken', { path: '/' });
			}
		} catch {
			event.cookies.delete('accessToken', { path: '/' });
			event.cookies.delete('refreshToken', { path: '/' });
		}
	}

	event.locals.user = user;

	const pathname = event.url.pathname;

	// First-run setup: redirect to /setup when Users table is empty.
	// Gated on (!user) and (!_setupDone cookie) so we don't hit /setup/status on every request.
	const isStaticOrApi = pathname.startsWith('/_app') || pathname.startsWith('/api/');
	const setupDoneCookie = event.cookies.get('_setupDone') === '1';
	if (!isStaticOrApi && !user && !setupDoneCookie) {
		try {
			const statusRes = await event.fetch(`${BASE_URL}/setup/status`);
			if (statusRes.ok) {
				const { needsSetup } = await statusRes.json();
				if (needsSetup && pathname !== '/setup') {
					throw redirect(303, '/setup');
				}
				if (!needsSetup) {
					event.cookies.set('_setupDone', '1', { path: '/', maxAge: 60 * 60 * 24 * 365 });
					if (pathname === '/setup') {
						throw redirect(303, '/');
					}
				}
			}
		} catch (e) {
			// Re-throw SvelteKit redirects, swallow everything else (backend may be down)
			if ((e as { status?: number })?.status === 303) throw e;
		}
	}

	// Захист маршрутів
	const isProtectedProfile =
		pathname === '/profile' ||
		/^\/profile\/[^/]+\/edit(\/.*)?$/.test(pathname);
	const otherProtected = ['/settings', '/lists', '/collections', '/reviews', '/following'];
	const isProtectedRoute = isProtectedProfile || otherProtected.some((p) => pathname.startsWith(p));

	if (isProtectedRoute && !user) {
		const from = event.url.pathname + event.url.search;
		throw redirect(303, `/auth/login?redirectTo=${encodeURIComponent(from)}`);
	}

	return await resolve(event);
};
