export function safeRedirectPath(value: string | null, fallback = '/'): string {
	if (!value) return fallback;
	if (!value.startsWith('/') || value.startsWith('//') || value.includes('\\')) return fallback;
	if (/^[a-z][a-z0-9+.-]*:/i.test(value)) return fallback;
	return value;
}
