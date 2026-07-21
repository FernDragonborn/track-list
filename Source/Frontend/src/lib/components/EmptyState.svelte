<script lang="ts">
	interface Props {
		message: string;
		dashed?: boolean;
	}
	let { message, dashed = true }: Props = $props();
</script>

<div
	class="py-16 text-center text-text-muted {dashed
		? 'border border-dashed border-gray-700 rounded-lg'
		: ''}"
>
	{message}
</div>
