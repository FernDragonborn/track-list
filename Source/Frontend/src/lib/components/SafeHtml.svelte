<script lang="ts">
	import DOMPurify from 'isomorphic-dompurify';

	interface Props {
		/** Raw HTML (already markdown-parsed if needed) — will be sanitized before render. */
		content: string | null | undefined;
		/** Optional wrapper class. */
		class?: string;
		/** Wrapper element — defaults to div; use 'span' for inline. */
		as?: 'div' | 'span';
	}
	let { content, class: cls = '', as = 'div' }: Props = $props();

	const safe = $derived(DOMPurify.sanitize(content ?? ''));
</script>

{#if as === 'span'}
	<!-- eslint-disable-next-line svelte/no-at-html-tags -- sanitized via DOMPurify above -->
	<span class={cls}>{@html safe}</span>
{:else}
	<!-- eslint-disable-next-line svelte/no-at-html-tags -- sanitized via DOMPurify above -->
	<div class={cls}>{@html safe}</div>
{/if}
