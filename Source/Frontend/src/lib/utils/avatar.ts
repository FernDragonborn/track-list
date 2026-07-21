/**
 * Build avatar URL with ui-avatars.com fallback.
 * If picUrl is a bare filename (no protocol, no slash), treat it as a BE-stored
 * upload and prefix with the API avatar route.
 */
export function getAvatarUrl(
	name: string,
	picUrl?: string | null,
	size = 64,
): string {
	if (picUrl) {
		const isAbsolute = /^https?:\/\//i.test(picUrl);
		const isRooted = picUrl.startsWith('/');
		if (isAbsolute || isRooted) return picUrl;
		// Bare filename from BE upload — point at avatar serving endpoint
		return `/api/profiles/avatar/${encodeURIComponent(picUrl)}`;
	}
	return `https://ui-avatars.com/api/?name=${encodeURIComponent(name)}&background=ff3d5e&color=fff&size=${size}`;
}
