/**
 * Svelte action: fire callback when the element scrolls into view.
 * rootMargin pre-loads ~600px below viewport for smooth infinite scroll.
 */
export function onEnterView(node: HTMLElement, callback: () => void) {
	const observer = new IntersectionObserver(
		(entries) => {
			if (entries[0].isIntersecting) callback();
		},
		{ rootMargin: '300px 0px 600px 0px' },
	);
	observer.observe(node);
	return { destroy: () => observer.disconnect() };
}
