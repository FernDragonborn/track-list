/**
 * Format ISO date string to Ukrainian locale date.
 */
export function formatDate(iso: string): string {
	return new Date(iso).toLocaleDateString('uk-UA');
}
