/**
 * Slice `text` to at most `maxLen` characters and append `…`.
 * When `smart` is true (default), try to end on a sentence boundary (`. `, `! `, `? `)
 * inside the last 40% of the slice so the preview doesn't cut mid-thought.
 * `text.length <= maxLen` returns the original string untouched.
 */
export function truncate(text: string, maxLen: number, opts?: { smart?: boolean }): string {
	if (!text || text.length <= maxLen) return text ?? '';
	const slice = text.slice(0, maxLen);
	if (opts?.smart === false) return slice.trimEnd() + '…';
	const lastDot = Math.max(slice.lastIndexOf('. '), slice.lastIndexOf('! '), slice.lastIndexOf('? '));
	const minBoundary = Math.floor(maxLen * 0.6);
	const cut = lastDot > minBoundary ? lastDot + 1 : maxLen;
	return text.slice(0, cut).trimEnd() + '…';
}
