<script lang="ts">
	interface Props {
		cls?: string;
	}
	let { cls = '' }: Props = $props();
</script>

<div class="tl-shimmer rounded {cls}"></div>

<style>
	.tl-shimmer {
		background: linear-gradient(90deg, #2a2a2a 25%, #3a3a3a 50%, #2a2a2a 75%);
		background-size: 200% 100%;
		animation: tl-shimmer 1.5s infinite;
	}
	@keyframes tl-shimmer {
		0% { background-position: -200% 0; }
		100% { background-position: 200% 0; }
	}
</style>
