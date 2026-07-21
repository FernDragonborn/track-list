/**
 * Extract human-readable error message from API error.
 * Handles SvelteKit HttpError shape ({ status, body: { message } | string }) and plain Error.
 */
export function extractError(e: unknown, fallback = 'Помилка'): string {
	if (e && typeof e === 'object') {
		const obj = e as Record<string, unknown>;
		if (typeof obj.body === 'string' && obj.body) return obj.body;
		if (obj.body && typeof obj.body === 'object') {
			const body = obj.body as Record<string, unknown>;
			if (typeof body.message === 'string' && body.message) return body.message;
			if (typeof body.error === 'string' && body.error) return body.error;
		}
		if (typeof obj.message === 'string' && obj.message) return obj.message;
	}
	if (e instanceof Error && e.message) return e.message;
	return fallback;
}
