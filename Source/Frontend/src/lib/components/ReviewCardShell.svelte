<script lang="ts">
	import type { Snippet } from 'svelte';

	interface Props {
		variant?: 'default' | 'compact';
		anchorId?: string;
		class?: string;
		header: Snippet;
		body: Snippet;
		footer?: Snippet;
	}
	let { variant = 'default', anchorId, class: cls = '', header, body, footer }: Props = $props();
</script>

<article
	id={anchorId}
	class="scroll-mt-28 bg-bkg-header rounded-xl border border-gray-700/50 target:ring-2 target:ring-yellow-400/60 transition-shadow {variant === 'compact'
		? 'px-3 py-2'
		: 'p-5'} {cls}"
>
	<div class="flex items-start justify-between gap-3 mb-3">
		{@render header()}
	</div>
	<div>
		{@render body()}
	</div>
	{#if footer}
		<div class="mt-3 flex items-center justify-between gap-3 text-xs text-text-muted">
			{@render footer()}
		</div>
	{/if}
</article>
