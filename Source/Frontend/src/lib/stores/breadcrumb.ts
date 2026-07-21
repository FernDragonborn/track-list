import { writable } from 'svelte/store';

/**
 * Per-route override for the LAST breadcrumb label. Pages that know a friendlier
 * label (e.g. media title for /media/external/Tmdb:movie:550) call set() on mount
 * and clear() on unmount.
 */
export const lastCrumbLabel = writable<string | null>(null);

/**
 * Path-keyed labels for ANY breadcrumb segment, not just the last one.
 * Lets a nested route (e.g. /collections/{id}/settings) resolve the {id}
 * segment to a human name. Set on mount, clear on unmount.
 */
export const crumbLabels = writable<Record<string, string>>({});

export function setCrumbLabel(href: string, label: string) {
	crumbLabels.update((m) => ({ ...m, [href]: label }));
}

export function clearCrumbLabel(href: string) {
	crumbLabels.update((m) => {
		const n = { ...m };
		delete n[href];
		return n;
	});
}

/**
 * Previous URL pathname, set by a root-level afterNavigate hook. Lets breadcrumbs
 * inject a context crumb (e.g. "Каталог" when arriving at a media page from /catalog).
 */
export const previousPath = writable<string | null>(null);
